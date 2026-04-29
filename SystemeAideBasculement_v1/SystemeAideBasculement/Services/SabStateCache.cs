namespace SystemeAideBasculement.Services
{
    using System.Text.Json;
    using SystemeAideBasculement.Models;

    public class SabStateCache
    {
        private readonly IWebHostEnvironment _env;

        public List<SabProfileRow> Profiles { get; private set; } = [];
        public List<SabPexRow> Pexs { get; private set; } = [];

        public event Action? OnStateChanged;

        public SabStateCache(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task LoadInitialStateAsync()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            Profiles = await LoadAsync<SabProfileRow>("config/sabProfiles.json", options);
            Pexs = await LoadAsync<SabPexRow>("config/sabPexs.json", options);
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
