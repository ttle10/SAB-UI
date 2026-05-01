namespace SystemeAideBasculement.Services
{
    using System;
    using System.Text.Json;
    using SystemeAideBasculement.Controllers;
    using SystemeAideBasculement.Models;
    using static System.Runtime.InteropServices.JavaScript.JSType;

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

        public List<SabProfileRow> Profiles { get; private set; } = [];
        
        public List<SabPexRow> Pexs { get; private set; } = [];

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
                var pr = await LoadAsync<SabProfileRow>("config/sabProfiles.json", JsonOptions);
                if (pr == null)
                {
                    _logger.LogError("Failed to deserialize sabProfiles initial configuration.");
                    return;
                }
                Profiles = pr.OrderBy(p => p.Index).ToList();
            }
            catch (JsonException)
            {
                _logger.LogError("Failed to deserialize sabProfiles initial configuration.");
                return;
            }

            try
            {
                var px = await LoadAsync<SabPexRow>("config/sabPexs.json", JsonOptions);
                if (Pexs == null)
                {
                    _logger.LogError("Failed to deserialize sabPexs initial configuration.");
                    return;
                }
                Pexs = px.OrderBy(p => p.Index).ToList();
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
            var sabDataNotif = new SabDataNotification();
            List<SabPexRow> pexs = new List<SabPexRow>();

            foreach (var notif in notifications)
            {
                if (notif == null) continue;

                var profile = UpdateProfileCache(notif);
                if (profile != null)
                {
                    sabDataNotif.Profiles.Add(profile);
                }
            }

            var pexTransforms = ToSabPexRows(notifications);
            foreach (var pexTrans in pexTransforms)
            {
                if (pexTrans == null) continue;

                var pex = UpdatePexCache(pexTrans);
                if (pex != null)
                {
                    sabDataNotif.Pexs.Add(pex);
                }
            }

            if (!sabDataNotif.IsEmpty)
                Notify();

            return sabDataNotif;
        }

        private SabProfileRow? UpdateProfileCache(ProfileConnectionNotificationModel notif)
        {
            var profile = Profiles.FirstOrDefault(p => p.Profile.Equals(notif.ProfileName, StringComparison.OrdinalIgnoreCase));
            if (profile == null) return null;

            bool isModified = false;
            var csvPiccNames = ConvertToCSV(notif.HostNames);
            if (_controlCenterFacilities.IsCCPFacility(notif.Site))
            {   
                if (!profile.CCP.PiccNames.Equals(csvPiccNames, StringComparison.OrdinalIgnoreCase))
                {
                    profile.CCP.PiccNames = csvPiccNames;
                    isModified = true;
                }
            }
            else if (_controlCenterFacilities.IsCCRFacility(notif.Site))
            {
                if (!profile.CCR.PiccNames.Equals(csvPiccNames, StringComparison.OrdinalIgnoreCase))
                {
                    profile.CCR.PiccNames = csvPiccNames;
                    isModified = true;
                }
            }

            return isModified ? profile.Clone() : null;
        }

        private static string ConvertToCSV(List<string> hostNames)
        {
            if (hostNames == null || hostNames.Count == 0)
            {
                return EndpointValue.None;
            }
            var orderedValues = hostNames.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            return string.Join(", ", orderedValues);
        }

        private SabPexRow? UpdatePexCache(SabPexTransform pexTransform)
        {
            SabPexRow? pex = null;
            bool isModified = false;
            var csvProfileNames = ConvertToCSV(pexTransform.ProfileNames);

            if (_controlCenterFacilities.IsCCPFacility(pexTransform.Site))
            {
                pex = Pexs.FirstOrDefault(p => p.CCPHostname.Equals(pexTransform.Hostname, StringComparison.OrdinalIgnoreCase));
                if (pex == null) return null;

                if (!pex.CCP.ProfileNames.Equals(csvProfileNames, StringComparison.OrdinalIgnoreCase))
                {
                    pex.CCP.ProfileNames = csvProfileNames;
                    isModified = true;
                }

            }
            else if (_controlCenterFacilities.IsCCRFacility(pexTransform.Site))
            {
                pex = Pexs.FirstOrDefault(p => p.CCRHostname.Equals(pexTransform.Hostname, StringComparison.OrdinalIgnoreCase));
                if (pex == null) return null;
                
                if (!pex.CCR.ProfileNames.Equals(csvProfileNames, StringComparison.OrdinalIgnoreCase))
                {
                    pex.CCR.ProfileNames = csvProfileNames;
                    isModified = true;
                }   
            }

            return isModified ? pex?.Clone() : null;
        }

        private static List<SabPexTransform> ToSabPexRows(List<ProfileConnectionNotificationModel> notifications)
        {
            return notifications
                .SelectMany(n => n.HostNames.Select(host => new
                {
                    Hostname = host,
                    ProfileName = n.ProfileName,
                    Site = n.Site
                }))
                .GroupBy(x => new { x.Hostname, x.Site })
                .Select(g => new SabPexTransform
                {
                    Hostname = g.Key.Hostname,
                    Site = g.Key.Site,
                    ProfileNames = g.Select(x => x.ProfileName)
                                   .Distinct(StringComparer.OrdinalIgnoreCase)
                                   .ToList()
                })
                .ToList();
        }

        private async Task<List<T>> LoadAsync<T>(string path, JsonSerializerOptions options)
        {
            var fullPath = Path.Combine(_env.WebRootPath, path);
            var json = await File.ReadAllTextAsync(fullPath);
            return JsonSerializer.Deserialize<List<T>>(json, options) ?? [];
        }

        private static string AppendValue(string valSource, string value)
        {
            if (string.IsNullOrEmpty(valSource))
            {
                valSource = valSource  +  EndpointValue.None;
            }
            else if (!string.IsNullOrEmpty(value))
            {
                valSource += $", {value}";
            }
            return valSource;
        }

        private void Notify() => OnStateChanged?.Invoke();
    }
}
