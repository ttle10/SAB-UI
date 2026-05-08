namespace SystemeAideBasculement.Services
{
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Options;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Immutable;
    using System.IO;
    using System.Text.Json;
    using SystemeAideBasculement.Controllers;
    using SystemeAideBasculement.Hubs;
    using SystemeAideBasculement.Models;
//    using static SystemeAideBasculement.Models.SabProfileRow;

    // Invariants:
    // - Profiles are the source of truth
    // PEX invariants:
    // - CCPHostname, CCRHostname, DisplayName, Index are reference data
    // - They are never modified after LoadInitialStateAsync
    // - CCPHostname is the lookup key for CCP updates
    // - CCRHostname is the lookup key for CCR updates
    // - PEX rows are incrementally enriched from profile updates
    // - Immutable replace-on-write only
    public class SabStateCache
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<NotificationsController> _logger;

        private ControlCenterFacilities _controlCenterFacilities;

        private ImmutableList<SabProfileRow> _profiles = ImmutableList<SabProfileRow>.Empty;
        private ImmutableList<SabPexRow> _pexs = ImmutableList<SabPexRow>.Empty;

        // Index for quick lookup during updates (not serialized, built on LoadInitialStateAsync and maintained on updates)
        private readonly Dictionary<string, PexIndexEntry> _pexByCcpHostname =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, PexIndexEntry> _pexByCcrHostname =
            new(StringComparer.OrdinalIgnoreCase);

        private ImmutableDictionary<int, PexIndexEntry> _pexEntriesByRowIndex =
            ImmutableDictionary<int, PexIndexEntry>.Empty;

        public IReadOnlyList<SabProfileRow> Profiles => _profiles;
        public IReadOnlyList<SabPexRow> Pexs => _pexs;

        // Pending notifications received while an update is in progress
        private readonly SabNotificationOptions _options;
        private readonly CancellationToken _shutdownToken;

        // Keeps ONLY the latest update per logical key
        private readonly ConcurrentDictionary<string, IncomingNotification>
            _pendingProfileNotifications = new();

        // Ensures a single update process at a time
        private readonly SemaphoreSlim _processGate = new(1, 1);

        // Cooldown between update runs
        private readonly TimeSpan UpdateCooldown = TimeSpan.FromSeconds(5);

        private int _notifying = 0;


        // Test‑only hooks (internal)
        internal ImmutableList<SabProfileRow> ProfilesInternal
        {
            get => _profiles;
            set => _profiles = value;
        }

        internal ImmutableList<SabPexRow> PexsInternal
        {
            get => _pexs;
            set => _pexs = value;
        }

        internal ControlCenterFacilities ControlCenterFacilities
        {
            get => _controlCenterFacilities;
            set => _controlCenterFacilities = value;
        }

        public bool IsReady { get; private set; } = false;

        public SabStateCache(IWebHostEnvironment env,
                             IHubContext<NotificationHub> hubContext,
                             ILogger<NotificationsController> logger,
                             IOptions<SabNotificationOptions> options,
                             IHostApplicationLifetime lifetime
                            )
        {
            _env = env;
            _hubContext = hubContext;
            _logger = logger;
            _options = options.Value;
            UpdateCooldown = _options.DebounceInterval;

            _controlCenterFacilities = new ControlCenterFacilities(_env, logger);

            // Used for graceful shutdown
            _shutdownToken = lifetime.ApplicationStopping;
        }

        public async Task LoadInitialStateAsync()
        {
            IsReady = false;

            try
            {
                var profiles = await JsonHelper.LoadListAsync<SabProfileRow>(Path.Combine(_env.WebRootPath, "config/sabProfiles.json"));
                if (profiles == null)
                {
                    _logger.LogError("[SabUI:SabStateCache:LoadInitialStateAsync]: Failed to deserialize sabProfiles initial configuration.");
                    return;
                }

                _profiles = profiles
                        .OrderBy(p => p.Index)
                        .Select(p => p.Clone())
                        .ToImmutableList();

            }
            catch (JsonException ex)
            {
                _logger.LogError($"[SabUI:SabStateCache:LoadInitialStateAsync]: Failed to deserialize sabProfiles initial configuration: {ex}");
                return;
            }

            try
            {
                var pexs = await JsonHelper.LoadListAsync<SabPexRow>(Path.Combine(_env.WebRootPath, "config/sabPexs.json"));
                if (pexs == null)
                {
                    _logger.LogError("[SabUI:SabStateCache:LoadInitialStateAsync]: Failed to deserialize sabPexs initial configuration.");
                    return;
                }
                _pexs = pexs
                        .OrderBy(p => p.Index)
                        .Select(p => p.Clone())
                        .ToImmutableList();
                RebuildPexIndex();
            }
            catch (JsonException ex)
            {
                _logger.LogError($"[SabUI:SabStateCache:LoadInitialStateAsync]: Failed to deserialize sabPexs initial configuration: {ex}");
                return;
            }

            IsReady = await _controlCenterFacilities.LoadData();
        }

        /* === METHOD CALLED BY NOTIFICATIONS === */

        public void EnqueueProfileNotification(
                                            List<ProfileConnectionNotificationModel> notifications,
                                            string senderId)
        {

            var now = DateTime.UtcNow;

            foreach (var n in notifications)
            {
                var incoming = new IncomingNotification(n, senderId, now);

                // Latest wins → outdated update is dropped here
                _pendingProfileNotifications[n.GetKey()] = incoming;
            }

            // Try to start processing (safe – semaphore protected)
            _ = TryProcessProfileNotificationsAsync();

        }

        public SabDataNotification Update(List<ProfileConnectionNotificationModel> notifications)
        {

            if (notifications == null || notifications.Count == 0)
                return SabDataNotification.Empty;

            var updatedProfiles = new List<SabProfileRow>();
            var updatedPexs = new List<SabPexRow>();

            // Apply profile updates
            foreach (var notif in notifications)
            {
                var proflieUpdate = UpdateProfileCache(notif);
                if (proflieUpdate != null)
                {
                    updatedProfiles.Add(proflieUpdate);
                    var updatePexs = UpdatePexCache(notif);
                    if (updatePexs != null)
                        updatedPexs.AddRange(updatePexs);
                }
            }

            var retDataNotif = new SabDataNotification();

            if (updatedProfiles.Count > 0 ||
                updatedPexs.Count > 0)
            {
                //  Build notification
                retDataNotif.Profiles = updatedProfiles;
                retDataNotif.Pexs = updatedPexs;
                _logger.LogTrace("[SabUI:SabStateCache:Update]: Update cache processed with state changes: Profiles [{Profiles}], Pexs [{Pexs}].", updatedProfiles.Count, updatedPexs.Count);
                var prettyJson = JsonSerializer.Serialize(
                     retDataNotif.Pexs,
                     new JsonSerializerOptions
                     {
                         WriteIndented = true
                     });
                _logger.LogInformation("[SabUI:SabStateCache:Update]: After Update:\n{Payload}", prettyJson);
            }
            else
            {
                _logger.LogTrace("[SabUI:SabStateCache:Update]: Update cache processed but no state changes detected.");
            }

            return retDataNotif;
        }

        internal SabProfileRow? UpdateProfileCache(ProfileConnectionNotificationModel notif)
        {
            var oldProfiles = _profiles;

            var index = oldProfiles.FindIndex(p =>
                string.Equals(p.Profile, notif.ProfileName, StringComparison.OrdinalIgnoreCase));

            if (index < 0)
            {
                _logger.LogWarning($"[SabUI:SabStateCache:UpdateProfileCache]: Not supported Profile '{notif.ProfileName}'");
                return null;
            }

            var oldProfile = oldProfiles[index];
            var updatedProfile = oldProfile.Clone();
            bool isDirty = false;

            Endpoint? endpoint = null;

            if (_controlCenterFacilities.CCPFacility.IsFacility(notif.Site))
                endpoint = updatedProfile.CCP;
            else if (_controlCenterFacilities.CCRFacility.IsFacility(notif.Site))
                endpoint = updatedProfile.CCR;
            else
            {
                _logger.LogWarning("[SabUI:SabStateCache:UpdateProfileCache]: Unknown site '{Site}' in UpdateProfileCache", notif.Site);
                return null;
            }

            var csvPiccNames = ConvertToCSV(notif.HostNames);
            var notifStatus = GetStatus(notif);

            if (!string.Equals(endpoint.PiccNames.Value, csvPiccNames, StringComparison.OrdinalIgnoreCase))
            {
                endpoint.PiccNames.Value = csvPiccNames;
                isDirty = true;
            }

            if (endpoint.PiccNames.Status != notifStatus)
            {
                endpoint.PiccNames.Status = notifStatus;
                isDirty = true;
            }

            if (!isDirty)
                return null;

            _profiles = oldProfiles.SetItem(index, updatedProfile);
            return updatedProfile;
        }

        private static string ConvertToCSV(List<string> hostNames)
        {
            if (hostNames == null || hostNames.Count == 0)
            {
                return string.Empty;
            }

            var sorted = hostNames.OrderBy(h => h, StringComparer.OrdinalIgnoreCase);
            return string.Join(", ", sorted);
        }

        internal List<SabPexRow> UpdatePexCache(ProfileConnectionNotificationModel notificationUpdate)
        {
            var changedRowIndexes = new HashSet<int>();

            bool isCcp = _controlCenterFacilities.CCPFacility.IsFacility(notificationUpdate.Site);
            bool isCcr = _controlCenterFacilities.CCRFacility.IsFacility(notificationUpdate.Site);

            if (!isCcp && !isCcr)
            {
                _logger.LogWarning("[SabUI:SabStateCache:UpdatePexCache]: Unknown site '{Site}' in UpdatePexCache", notificationUpdate.Site);
                return new List<SabPexRow>();
            }

            if (notificationUpdate.IsConnected())
            {
                var lookup = isCcp ? _pexByCcpHostname : _pexByCcrHostname;
                var distinctHosts = notificationUpdate.HostNames
                    .Where(h => !string.IsNullOrWhiteSpace(h))
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (var host in distinctHosts)
                {
                    if (!lookup.TryGetValue(host, out var entry))
                        continue;

                    var siteState = isCcp ? entry.CCP : entry.CCR;
                    if (siteState.ProfileNames.Add(notificationUpdate.ProfileName))
                    {
                        changedRowIndexes.Add(entry.RowIndex);
                    }
                }
            }
            else if (notificationUpdate.IsDisconnected())
            {
                foreach (var pair in _pexEntriesByRowIndex)
                {
                    var entry = pair.Value;
                    var siteState = isCcp ? entry.CCP : entry.CCR;

                    if (siteState.ProfileNames.Remove(notificationUpdate.ProfileName))
                    {
                        changedRowIndexes.Add(entry.RowIndex);
                    }
                }
            }
            else
                {
                _logger.LogWarning("[SabUI:SabStateCache:UpdatePexCache]: Unsupported connection status in UpdatePexCache for Profile '{Profile}'", notificationUpdate.ProfileName);
                return new List<SabPexRow>();
            }

            if (changedRowIndexes.Count == 0)
                return new List<SabPexRow>();

            var updatedRows = new List<SabPexRow>();

            foreach (var rowIndex in changedRowIndexes.OrderBy(i => i))
            {
                var oldRow = _pexs[rowIndex];
                var entry = _pexEntriesByRowIndex[rowIndex];

                var newRow = oldRow.Clone();

                newRow.CCP.ProfileNames.Value = JoinCsv(entry.CCP.ProfileNames);
                newRow.CCP.ProfileNames.Status = entry.CCP.Status;

                newRow.CCR.ProfileNames.Value = JoinCsv(entry.CCR.ProfileNames);
                newRow.CCR.ProfileNames.Status = entry.CCR.Status;

                _pexs = _pexs.SetItem(rowIndex, newRow);
                updatedRows.Add(newRow);
            }

            return updatedRows;
        }

        private static List<string> SplitCsv(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                string.Equals(value, EndpointValue.None, StringComparison.OrdinalIgnoreCase))
            {
                return new List<string>();
            }

            return value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string JoinCsv(IEnumerable<string> values)
        {
            return string.Join(", ",
                values
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        }

        private static EndpointStatus GetStatus(ProfileConnectionNotificationModel notication)
        {
            if (notication.IsConnected())
                return EndpointStatus.Connected;
            if (notication.IsDisconnected())
                return EndpointStatus.Disconnected; 

            return EndpointStatus.Unknown;
        }

        private async Task TryProcessProfileNotificationsAsync()
        {
            // Ensure only ONE processing loop
            if (!await _processGate.WaitAsync(0))
                return;

            try
            {
                if (_pendingProfileNotifications.IsEmpty)
                    return;

                var batch = _pendingProfileNotifications.Values.ToList();
                _pendingProfileNotifications.Clear();

                var distinctSenders = batch
                    .Select(b => b.SenderId)
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                _logger.LogInformation(
                    "[SabUI:SabStateCache:TryProcessProfileNotificationsAsync]: Processing {Count} profile notification(s) from sender(s): {Senders}",
                    batch.Count,
                    string.Join(", ", distinctSenders));

                var result = Update(batch.Select(b => b.Notification).ToList());

                if (!result.IsEmpty)
                    await Notify(result);

                // Cooldown before next run
                await Task.Delay(UpdateCooldown);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[SabUI:SabStateCache:TryProcessProfileNotificationsAsync]: Unhandled error while processing profile notifications.");
            }
            finally
            {
                _processGate.Release();

                // New updates arrived during processing/cooldown → run again
                if (!_pendingProfileNotifications.IsEmpty)
                    _ = TryProcessProfileNotificationsAsync();
            }
        }

        private async Task Notify(SabDataNotification notification)
        {
            // Ensure single execution (atomic)
            if (Interlocked.Exchange(ref _notifying, 1) == 1)
                return;

            try
            {
                _logger.LogTrace("[SabUI:SabStateCache:Notify]: Notify Client to refresh.");
                await _hubContext.Clients.All.SendAsync(
                    "ProfileUpdated",
                    notification,
                    _shutdownToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SabUI:SabStateCache:Notify]: Failed to send status change to clients.");
            }
            finally
            {
                Interlocked.Exchange(ref _notifying, 0);
            }
        }

        private void RebuildPexIndex()
        {
            _pexByCcpHostname.Clear();
            _pexByCcrHostname.Clear();

            var builder = ImmutableDictionary.CreateBuilder<int, PexIndexEntry>();

            for (int i = 0; i < _pexs.Count; i++)
            {
                var row = _pexs[i];

                var entry = new PexIndexEntry
                {
                    RowIndex = i,
                    CCPHostname = row.CCPHostname,
                    CCRHostname = row.CCRHostname,
                    DisplayName = row.DisplayName
                };

                foreach (var name in SplitCsv(row.CCP.ProfileNames.Value))
                    entry.CCP.ProfileNames.Add(name);

                foreach (var name in SplitCsv(row.CCR.ProfileNames.Value))
                    entry.CCR.ProfileNames.Add(name);

                builder[i] = entry;

                if (!string.IsNullOrWhiteSpace(row.CCPHostname))
                    _pexByCcpHostname[row.CCPHostname] = entry;

                if (!string.IsNullOrWhiteSpace(row.CCRHostname))
                    _pexByCcrHostname[row.CCRHostname] = entry;
            }

            _pexEntriesByRowIndex = builder.ToImmutable();
        }
    }
}
