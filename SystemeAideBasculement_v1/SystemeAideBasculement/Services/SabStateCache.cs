namespace SystemeAideBasculement.Services
{
    using Microsoft.Extensions.Options;
    using Microsoft.VisualBasic;
    using System;
    using System.Collections.Immutable;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using SystemeAideBasculement.Controllers;
    using SystemeAideBasculement.Models;

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
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<NotificationsController> _logger;

        private ControlCenterFacilities _controlCenterFacilities;

        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters =
                {
                    new JsonStringEnumConverter()
                }
            };


        private ImmutableList<SabProfileRow> _profiles = ImmutableList<SabProfileRow>.Empty;
        private ImmutableList<SabPexRow> _pexs = ImmutableList<SabPexRow>.Empty;

        public IReadOnlyList<SabProfileRow> Profiles => _profiles;
        public IReadOnlyList<SabPexRow> Pexs => _pexs;

        // Pending notifications received while an update is in progress
        private readonly SabNotificationOptions _options;
        private readonly CancellationToken _shutdownToken;

        private readonly object _lock = new();

        private Timer? _debounceTimer;
        private readonly List<IncomingNotification> _pendingNotifications = new();


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

        public event Action? OnStateChanged;

        public SabStateCache(IWebHostEnvironment env,
                             ILogger<NotificationsController> logger,
                             IOptions<SabNotificationOptions> options,
                             IHostApplicationLifetime lifetime
                            )
        {
            _env = env;
            _logger = logger;
            _options = options.Value;

            _controlCenterFacilities = new ControlCenterFacilities(logger);

            // Used for graceful shutdown
            _shutdownToken = lifetime.ApplicationStopping;
        }

        public async Task LoadInitialStateAsync()
        {
            IsReady = false;
            try
            {
                var profiles = await LoadAsync<SabProfileRow>("config/sabProfiles.json", JsonOptions);
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
                var pexs = await LoadAsync<SabPexRow>("config/sabPexs.json", JsonOptions);
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

            IsReady = _controlCenterFacilities.LoadData();
        }

        /* === METHOD CALLED BY NOTIFICATIONS === */
        public void EnqueueProfileNotification(
                                            List<ProfileConnectionNotificationModel> notifications,
                                            string senderId)
        {
            lock (_lock)
            {

                //Ignore new notifications during shutdown
                if (_shutdownToken.IsCancellationRequested)
                {
                    _logger.LogDebug(
                        "Ignoring {Count} notification(s) from {Sender} during shutdown",
                        notifications.Count,
                        senderId);
                    return;
                }

                if (notifications == null || notifications.Count == 0)
                    return;


                _logger.LogDebug(
                    "Enqueued {Count} notification(s) from sender {Sender}. Pending={Pending}",
                    notifications.Count,
                    senderId,
                    _pendingNotifications.Count);

                // Enqueue all notifications
                foreach (var notification in notifications)
                {
                    _pendingNotifications.Add(
                        new IncomingNotification(notification, senderId, DateTime.UtcNow));
                }

                // Safety: process immediately if batch is too large
                if (_pendingNotifications.Count >= _options.MaxBatchSize)
                {
                    _logger.LogWarning(
                        "Max batch size reached ({Count}). Forcing cache update.",
                        _pendingNotifications.Count);

                    TriggerProcessing();
                    return;
                }

                // Start debounce timer only once
                if (_debounceTimer == null)
                {
                    _debounceTimer = new Timer(
                        ProcessPendingNotifications,
                        null,
                        _options.DebounceInterval,
                        Timeout.InfiniteTimeSpan);
                }
            }
        }

        public SabDataNotification Update(List<ProfileConnectionNotificationModel> notifications)
        {

            if (notifications == null || notifications.Count == 0)
                return new SabDataNotification();

            var updatedProfiles = new List<SabProfileRow>();
            var updatedPexs = new List<SabPexRow>();


            // Apply profile updates
            foreach (var notif in notifications)
            {
                var updated = UpdateProfileCache(notif);
                if (updated != null)
                {
                    updatedProfiles.Add(updated);
                    var updatePexs = UpdatePexCache(updated, notif.Site);
                    if (updatePexs != null)
                        updatedPexs.AddRange(updatePexs);
                }
            }

            // If no profile changed → nothing else can change
            if (updatedProfiles.Count == 0)
                return new SabDataNotification();

            Notify();
            //  Build notification
            return new SabDataNotification
            {
                Profiles = updatedProfiles,
                Pexs = updatedPexs
            };
        }

        internal SabProfileRow? UpdateProfileCache(ProfileConnectionNotificationModel notif)
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
                return null; // or log warning

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
                return EndpointValue.None;
            }

            var sorted = hostNames.OrderBy(h => h, StringComparer.OrdinalIgnoreCase);
            return string.Join(", ", sorted);
        }

        internal List<SabPexRow> UpdatePexCache(SabProfileRow updatedProfile, string site)
        {
            var updatedRows = new List<SabPexRow>();

            bool isCcp = _controlCenterFacilities.CCPFacility.IsFacility(site);
            bool isCcr = _controlCenterFacilities.CCRFacility.IsFacility(site);

            if (!isCcp && !isCcr)
            {
                _logger.LogWarning("Unknown site '{Site}' in UpdatePexCache", site);
                return updatedRows;
            }

            var piccField = isCcp
                ? updatedProfile.CCP.PiccNames
                : updatedProfile.CCR.PiccNames;

            var hostnames = piccField.Value
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

                if (piccField.Status == EndpointStatus.Connected)
                {
                    var existingProfiles = endpoint.ProfileNames.Value;

                    endpoint.ProfileNames.Value =
                        string.IsNullOrWhiteSpace(existingProfiles)
                            ? updatedProfile.Profile
                            : string.Join(", ",
                                existingProfiles
                                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                    .Concat(new[] { updatedProfile.Profile })
                                    .Distinct(StringComparer.OrdinalIgnoreCase));

                    endpoint.ProfileNames.Status = EndpointStatus.Connected;
                }
                else
                {
                    var remaining = endpoint.ProfileNames.Value
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(p => !string.Equals(p, updatedProfile.Profile, StringComparison.OrdinalIgnoreCase))
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

        private async Task<List<T>> LoadAsync<T>(string path, JsonSerializerOptions options)
        {
            var fullPath = Path.Combine(_env.WebRootPath, path);
            var json = await File.ReadAllTextAsync(fullPath);
            return JsonSerializer.Deserialize<List<T>>(json, options) ?? [];
        }

        private void TriggerProcessing()
        {
            // Prevent running during shutdown
            if (_shutdownToken.IsCancellationRequested)
            {
                _logger.LogDebug("TriggerProcessing skipped during shutdown");
                return;
            }

            // Cancel any pending debounce timer
            _debounceTimer?.Dispose();
            _debounceTimer = null;

            // Process immediately on the current ThreadPool thread
            // (same logic as timer callback)
            ProcessPendingNotifications(state: null);
        }

        private void ProcessPendingNotifications(object? state)
        {
            List<IncomingNotification> batch;

            // Do not process during shutdown
            if (_shutdownToken.IsCancellationRequested)
            {
                _logger.LogDebug("ProcessPendingNotifications skipped because application is stopping.");
                return;
            }

            lock (_lock)
            {
                if (_pendingNotifications.Count == 0)
                {
                    // Nothing to process, ensure timer is cleaned up
                    _debounceTimer?.Dispose();
                    _debounceTimer = null;
                    return;
                }

                //  Copy pending notifications and clear buffer atomically
                batch = new List<IncomingNotification>(_pendingNotifications);
                _pendingNotifications.Clear();

                // Dispose timer – new debounce cycle may start after this
                _debounceTimer?.Dispose();
                _debounceTimer = null;
            }

            try
            {
                var distinctSenders = batch
                    .Select(n => n.SenderId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _logger.LogInformation(
                    "Processing {Count} notification(s) from sender(s): {Senders}",
                    batch.Count,
                    string.Join(", ", distinctSenders));

                // Perform a single cache update using existing logic
                var result = Update(batch.Select(b => b.Notification).ToList());

                // Raise state change only if something actually changed
                if ((result.Profiles?.Count ?? 0) > 0 ||
                    (result.Pexs?.Count ?? 0) > 0)
                {
                    Notify();
                }
                else
                {
                    _logger.LogDebug("Batch processed but no state changes detected.");
                }
            }
            catch (Exception ex)
            {
                // Never let background processing crash the app
                _logger.LogError(ex, "Unhandled error while processing notification batch.");
            }
        }

        private void Notify() => OnStateChanged?.Invoke();
    }
}
