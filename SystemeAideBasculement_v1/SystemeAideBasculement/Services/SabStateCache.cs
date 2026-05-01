namespace SystemeAideBasculement.Services
{
    using System;
    using System.Text.Json;
    using System.Collections.Immutable;
    using SystemeAideBasculement.Controllers;
    using SystemeAideBasculement.Models;
    

    public class SabStateCache
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<NotificationsController> _logger;

        private ControlCenterFacilities _controlCenterFacilities;

        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
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
            catch (JsonException)
            {
                _logger.LogError("Failed to deserialize sabProfiles initial configuration.");
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
            catch (JsonException)
            {
                _logger.LogError("Failed to deserialize sabPexs initial configuration.");
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


            // Apply profile updates
            foreach (var notif in notifications)
            {
                var updated = UpdateProfileCache(notif);
                if (updated != null)
                {
                    updatedProfiles.Add(updated);
                }
            }


            // If no profile changed → nothing else can change
            if (updatedProfiles.Count == 0)
                return new SabDataNotification();

            // Profiles snapshot has changed at this point
            var newProfilesSnapshot = _profiles;


            // Recompute PEX snapshot from profiles (derived state)
            var oldPexs = _pexs;
            var newPexs = ToSabPexRows(newProfilesSnapshot);


            // 5️⃣ Build notification
            return new SabDataNotification
            {
                Profiles = updatedProfiles,
                Pexs = newPexs.ToList()
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

            if (_controlCenterFacilities.IsCCPFacility(notif.Site))
            {   
                if (string.Equals(updatedProfile.CCP.PiccNames, csvPiccNames, StringComparison.OrdinalIgnoreCase))
                    return null;

                    updatedProfile.CCP.PiccNames = csvPiccNames;
            }
            else if (_controlCenterFacilities.IsCCRFacility(notif.Site))
            {
                if (string.Equals(updatedProfile.CCR.PiccNames, csvPiccNames, StringComparison.OrdinalIgnoreCase))
                    return null;

                    updatedProfile.CCR.PiccNames = csvPiccNames;
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


        private static ImmutableList<SabPexRow> ToSabPexRows(
            ImmutableList<SabProfileRow> profiles)
        {
            return profiles
                .SelectMany(profile =>
                {
                    var rows = new List<SabPexRow>();

                    if (profile.CCP.PiccNames != EndpointValue.None)
                    {
                        foreach (var host in profile.CCP.PiccNames.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        {
                            rows.Add(new SabPexRow
                            {
                                CCPHostname = host.Trim(),
                                CCP = new Endpoint
                                {
                                    ProfileNames = profile.Profile
                                }
                            });
                        }
                    }

                    if (profile.CCR.PiccNames != EndpointValue.None)
                    {
                        foreach (var host in profile.CCR.PiccNames.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        {
                            rows.Add(new SabPexRow
                            {
                                CCRHostname = host.Trim(),
                                CCR = new Endpoint
                                {
                                    ProfileNames = profile.Profile
                                }
                            });
                        }
                    }

                    return rows;
                })
                .GroupBy(r => new { r.CCPHostname, r.CCRHostname })
                .Select((g, index) =>
                {
                    var row = new SabPexRow
                    {
                        Index = index + 1
                    };

                    foreach (var item in g)
                    {
                        if (!string.IsNullOrEmpty(item.CCPHostname))
                        {
                            row.CCPHostname = item.CCPHostname;
                            row.CCP.ProfileNames = item.CCP.ProfileNames;
                        }

                        if (!string.IsNullOrEmpty(item.CCRHostname))
                        {
                            row.CCRHostname = item.CCRHostname;
                            row.CCR.ProfileNames = item.CCR.ProfileNames;
                        }
                    }

                    return row;
                })
                .ToImmutableList();
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
