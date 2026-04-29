namespace SystemeAideBasculement.Services
{
    using System;
    using System.Text.Json;
    using SystemeAideBasculement.Controllers;
    using SystemeAideBasculement.Models;

    public class SabStateCache
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<NotificationsController> _logger;

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
        }

        public async Task LoadInitialStateAsync()
        {
            IsReady = false;
            try
            {
                Profiles = await LoadAsync<SabProfileRow>("config/sabProfiles.json", JsonOptions);
                if (Profiles == null)
                {
                    _logger.LogError("Failed to deserialize sabProfiles initial configuration.");
                }
            }
            catch (JsonException)
            {
                _logger.LogError("Failed to deserialize sabProfiles initial configuration.");
            }

            try
            {
                Pexs = await LoadAsync<SabPexRow>("config/sabPexs.json", JsonOptions);
                if (Pexs == null)
                {
                    _logger.LogError("Failed to deserialize sabPexs initial configuration.");
                }
            }
            catch (JsonException)
            {
                _logger.LogError("Failed to deserialize sabPexs initial configuration.");
            }
            IsReady = true;
        }

        /* === UPDATE METHODS CALLED BY NOTIFICATIONS === */

        public void Update(ProfileConnectionNotificationModel n)
        {
        }

        private void UpdateProfile(ProfileConnectionNotificationModel n)
        {
            var profile = Profiles.FirstOrDefault(p => p.Profile == n.ProfileName);
            if (profile == null) return;

            var endpoint = n.Site == "CCP" ? profile.CCP : profile.CCR;
           // Apply(endpoint, n);

            Notify();
        }

        private void UpdatePex(ProfileConnectionNotificationModel n)
        {
            //var pex = Pexs.FirstOrDefault(p => p.Pex == n.Pex);
            //if (pex == null) return;

            //var endpoint = n.Site == "CCP" ? pex.CCP : pex.CCR;
            //Apply(endpoint, n);

            Notify();
        }

        private async Task<List<T>> LoadAsync<T>(string path, JsonSerializerOptions options)
        {
            var fullPath = Path.Combine(_env.WebRootPath, path);
            var json = await File.ReadAllTextAsync(fullPath);
            return JsonSerializer.Deserialize<List<T>>(json, options) ?? [];
        }

        //private static void Apply(Endpoint endpoint, IConnectionStatus n)
        //{
        //    endpoint.PiccNames  = n.Poste;
        //    endpoint.CRA = n.CRA;
        //    endpoint.REU = n.REU;
        //    endpoint.SGCZ = n.SGCZ;
        //    endpoint.STI = n.STI;
        //}

        private void Notify() => OnStateChanged?.Invoke();
    }
}
