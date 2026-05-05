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
                    _logger.LogError("Failed to deserialize sabProfiles initial configuration.");
                    return;
                }

                _profiles = profiles
                        .OrderBy(p => p.Index)
                        .Select(p => p.Clone())
                        .ToImmutableList();

            }
            catch (JsonException ex)
            {
                _logger.LogError($"[LoadInitialStateAsync] Failed to deserialize sabProfiles initial configuration: {ex}");
                return;
            }

            try
            {
                var pexs = await JsonHelper.LoadListAsync<SabPexRow>(Path.Combine(_env.WebRootPath, "config/sabPexs.json"));
                if (pexs == null)
                {
                    _logger.LogError("Failed to deserialize sabPexs initial configuration.");
                    return;
                }
                _pexs = pexs
                        .OrderBy(p => p.Index)
                        .Select(p => p.Clone())
                        .ToImmutableList();
            }
            catch (JsonException ex)
            {
                _logger.LogError($"[LoadInitialStateAsync] Failed to deserialize sabPexs initial configuration: {ex}");
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
                var proflieUpdatedData = UpdateProfileCache(notif);
                if (proflieUpdatedData != null)
                {
                    updatedProfiles.Add(proflieUpdatedData.NewProfile);
                    var updatePexs = UpdatePexCache(proflieUpdatedData, notif.Site);
                    if (updatePexs != null)
                        updatedPexs.AddRange(updatePexs);
                }
            }

            var retDataNotif = new SabDataNotification();

            // Raise state change only if something actually changed
            if (updatedProfiles.Count > 0 ||
                updatedPexs.Count > 0)
            {
                //Version++; // single authoritative increment
                //  Build notification
                retDataNotif.Profiles = updatedProfiles;
                retDataNotif.Pexs = updatedPexs;
            }
            else
            {
                _logger.LogDebug("Update cache processed but no state changes detected.");
            }

            return retDataNotif;
        }

        internal SabProfileUpdatedData? UpdateProfileCache(ProfileConnectionNotificationModel notif)
        {
            var oldProfiles = _profiles;

            var index = oldProfiles.FindIndex(p =>
                string.Equals(p.Profile, notif.ProfileName, StringComparison.OrdinalIgnoreCase));

            if (index < 0)
                return null;

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
                _logger.LogWarning("Unknown site '{Site}' in UpdateProfileCache", notif.Site);
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

            var proflieUpdatedData = new SabProfileUpdatedData
            {
                OldProfile = oldProfile,
                NewProfile = updatedProfile
            };
            _profiles = oldProfiles.SetItem(index, updatedProfile);
            return proflieUpdatedData;
        }

        private static string ConvertToCSV(List<string> hostNames)
        {
            if (hostNames == null || hostNames.Count == 0)
            {
                return EndpointValue.None;
            }

            var sorted = hostNames.OrderBy(h => h, StringComparer.OrdinalIgnoreCase);
            return string.Join(", ", sorted);
        }

        internal List<SabPexRow> UpdatePexCache(SabProfileUpdatedData profilUpdatedData, string site)
        {
            var updatedRows = new List<SabPexRow>();

            bool isCcp = _controlCenterFacilities.CCPFacility.IsFacility(site);
            bool isCcr = _controlCenterFacilities.CCRFacility.IsFacility(site);

            if (!isCcp && !isCcr)
            {
                _logger.LogWarning("Unknown site '{Site}' in UpdatePexCache", site);
                return updatedRows;
            }

            var piccFieldOld = isCcp
                ? profilUpdatedData.OldProfile.CCP.PiccNames
                : profilUpdatedData.OldProfile.CCR.PiccNames;

            var piccFieldNew = isCcp
                ? profilUpdatedData.NewProfile.CCP.PiccNames
                : profilUpdatedData.NewProfile.CCR.PiccNames;

            var hostnameStr = piccFieldOld.Value;
            if (string.IsNullOrEmpty(hostnameStr))
            {
                if (!string.IsNullOrEmpty(piccFieldNew.Value))
                {
                    hostnameStr = piccFieldNew.Value;
                }
            }
            var hostnames = hostnameStr
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var hostname in hostnames)
            {
                int index = _pexs.FindIndex(p =>
                    isCcp
                        ? string.Equals(p.CCPHostname, hostname, StringComparison.OrdinalIgnoreCase)
                        : string.Equals(p.CCRHostname, hostname, StringComparison.OrdinalIgnoreCase));

                if (index < 0)
                    continue;

                var oldRow = _pexs[index];
                var newRow = oldRow.Clone();

                var endpoint = isCcp ? newRow.CCP : newRow.CCR;

                if (piccFieldNew.Status == EndpointStatus.Connected)
                {
                    var existingProfiles = endpoint.ProfileNames.Value;

                    endpoint.ProfileNames.Value =
                        string.IsNullOrWhiteSpace(existingProfiles)
                            ? profilUpdatedData.NewProfile.Profile
                            : string.Join(", ",
                                existingProfiles
                                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                    .Concat(new[] { profilUpdatedData.NewProfile.Profile })
                                    .Distinct(StringComparer.OrdinalIgnoreCase));

                    endpoint.ProfileNames.Status = EndpointStatus.Connected;
                }
                else
                {
                    var remaining = endpoint.ProfileNames.Value
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(p => !string.Equals(p, profilUpdatedData.NewProfile.Profile, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    endpoint.ProfileNames.Value =
                        remaining.Count == 0 ? EndpointValue.None : string.Join(", ", remaining);

                    endpoint.ProfileNames.Status =
                        remaining.Count == 0 ? EndpointStatus.Disconnected : EndpointStatus.Connected;
                }

                _pexs = _pexs.SetItem(index, newRow);
                updatedRows.Add(newRow);
            }

            return updatedRows;
        }

        private static EndpointStatus GetStatus(ProfileConnectionNotificationModel notication)
        {
            switch (notication.Status)
            {
                case ProfileConnectionStatus.Connected:
                    return EndpointStatus.Connected;
                case ProfileConnectionStatus.Disconnected:
                    return EndpointStatus.Disconnected;
                default:
                    return EndpointStatus.Unknown;
            }
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
                    "Processing {Count} profile notification(s) from sender(s): {Senders}",
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
                    "Unhandled error while processing profile notifications.");
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
                await _hubContext.Clients.All.SendAsync(
                    "ProfileUpdated",
                    notification,
                    _shutdownToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification to clients.");
            }
            finally
            {
                Interlocked.Exchange(ref _notifying, 0);
            }
        }
    }
}
