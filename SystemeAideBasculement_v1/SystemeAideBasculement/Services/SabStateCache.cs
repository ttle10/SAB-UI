namespace SystemeAideBasculement.Services
{
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

        public bool IsReady { get; private set; } = false;

        public event Action? OnStateChanged;

        public SabStateCache(IWebHostEnvironment env,
                             ILogger<NotificationsController> logger)
        {
            _env = env;
            _logger = logger;
            _controlCenterFacilities = new ControlCenterFacilities(logger);
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

        /* === UPDATE METHODS CALLED BY NOTIFICATIONS === */

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

        private SabProfileRow? UpdateProfileCache(ProfileConnectionNotificationModel notif)
        {
            var csvPiccNames = ConvertToCSV(notif.HostNames);

            var oldProfiles = _profiles;

            var index = oldProfiles.FindIndex(p => string.Equals(p.Profile, notif.ProfileName, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                return null;

            var oldProfile = oldProfiles[index];
            var updatedProfile = oldProfile.Clone();

            if (_controlCenterFacilities.CCPFacility.IsFacility(notif.Site))
            {   
                if (string.Equals(updatedProfile.CCP.PiccNames.Value, csvPiccNames, StringComparison.OrdinalIgnoreCase))
                    return null;
                var notifStatus = GetStatus(notif);
                if (updatedProfile.CCP.PiccNames.Status == notifStatus)
                    return null;

                updatedProfile.CCP.PiccNames.Value = csvPiccNames;
                updatedProfile.CCP.PiccNames.Status = notifStatus;
            }
            else if (_controlCenterFacilities.CCRFacility.IsFacility(notif.Site))
            {
                if (string.Equals(updatedProfile.CCR.PiccNames.Value, csvPiccNames, StringComparison.OrdinalIgnoreCase))
                    return null;
                var notifStatus = GetStatus(notif);
                if (updatedProfile.CCR.PiccNames.Status == notifStatus)
                    return null;

                updatedProfile.CCR.PiccNames.Value = csvPiccNames;
                updatedProfile.CCR.PiccNames.Status = notifStatus;  
            }

            // Replace atomically
            _profiles = oldProfiles.SetItem(index, updatedProfile);

            return updatedProfile;
        }

        private static string ConvertToCSV(List<string> hostNames)
        {
            if (hostNames == null || hostNames.Count == 0)
            {
                return EndpointValue.None;
            }
            return string.Join(", ", hostNames);
        }

        private List<SabPexRow> UpdatePexCache(SabProfileRow updatedProfile, string site)
        {
            var updatedRows = new List<SabPexRow>();

            // Select the correct PiccNames field based on site
            var piccField = _controlCenterFacilities.CCPFacility.IsFacility(site)
                ? updatedProfile.CCP.PiccNames
                : updatedProfile.CCR.PiccNames;

            // Determine connected hostnames
            var hostnames = piccField.Value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var hostname in hostnames)
            {
                //KEY LOOKUP RULE (STRICT)
                int index = _pexs.FindIndex(p =>
                    _controlCenterFacilities.CCPFacility.IsFacility(site)
                        ? string.Equals(p.CCPHostname, hostname, StringComparison.OrdinalIgnoreCase)
                        : string.Equals(p.CCRHostname, hostname, StringComparison.OrdinalIgnoreCase));

                if (index < 0)
                    continue; // no matching PEX row → ignore safely

                var oldRow = _pexs[index];
                var newRow = oldRow.Clone(); // immutable safety

                // Update ONLY the endpoint corresponding to the site
                var endpoint = _controlCenterFacilities.CCPFacility.IsFacility(site)
                    ? newRow.CCP
                    : newRow.CCR;

                // Merge profile name (avoid duplicates)
                endpoint.ProfileNames.Value =
                    endpoint.ProfileNames.Value == EndpointValue.None
                        ? updatedProfile.Profile
                        : string.Join(", ",
                            endpoint.ProfileNames.Value
                                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                .Concat(new[] { updatedProfile.Profile })
                                .Distinct(StringComparer.OrdinalIgnoreCase));

                endpoint.ProfileNames.Status = EndpointStatus.Connected;

                // Replace atomically
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

        private void Notify() => OnStateChanged?.Invoke();
    }
}
