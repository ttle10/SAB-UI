using System.IO;
using System.Text.Json.Serialization;
using SystemeAideBasculement.Controllers;

namespace SystemeAideBasculement.Models
{
    public enum FacilityCode
    {
        CCP = 0,
        CCR = 1,
    }

    public interface IControlCenterFacility
    {
        string Name { get; set; }
        FacilityCode Code { get; set; }
        bool IsFacility(string facilityName);
    }

    public class ControlCenterFacility : IControlCenterFacility
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("facilityCode")]
        public FacilityCode Code { get; set; } = FacilityCode.CCP;

        public bool IsFacility(string facilityName)
        {
            return string.Equals(facilityName, Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class CCPFacility: ControlCenterFacility
    {
        public CCPFacility()
        {
            Code = FacilityCode.CCP;
        }
    }

    public class CCRFacility: ControlCenterFacility
    {
        public CCRFacility()
        {
            Code = FacilityCode.CCR;
        }
    }

    public class ControlCenterFacilities
    {
        private readonly IWebHostEnvironment _env;

        private const string ConfFile = "ControlCenterFacility.json";
        private string ConfFilePath;
        private CCPFacility _ccpFacility;
        private CCRFacility _ccrFacility;
        private readonly ILogger<NotificationsController> _logger;

        public IControlCenterFacility CCPFacility { get => _ccpFacility; }

        public IControlCenterFacility CCRFacility { get => _ccrFacility; }

        public bool IsReady { get; private set; } = false;

        public ControlCenterFacilities(IWebHostEnvironment env, 
                                       ILogger<NotificationsController> logger)
        {
            _env = env;
            _logger = logger;
            _ccpFacility = new CCPFacility
            {
                Code = FacilityCode.CCP
            };
            _ccrFacility = new CCRFacility
            {
                Code = FacilityCode.CCR
            };

            ConfFilePath = Path.Combine(_env.WebRootPath,$"config/{ConfFile}");
        }

        internal void SetMoqCCPFacility(string name)
        {
            _ccpFacility.Name = name;
        }

        internal void SetMoqCCRFacility(string name)
        {
            _ccrFacility.Name = name;
        }

        public async Task<bool> LoadData()
        {
            try
            {
                var json = File.ReadAllText(ConfFilePath);
                var facilities = await JsonHelper.LoadListAsync<ControlCenterFacility>(ConfFilePath);
                if (facilities != null)
                {
                    foreach (var item in facilities)
                    {
                        if (item.Code == _ccpFacility.Code)
                        { 
                            _ccpFacility.Name = item.Name;
                        }
                        else if (item.Code == _ccrFacility.Code)
                        {
                            _ccrFacility.Name = item.Name;
                        }
                    }
                    IsReady = !string.IsNullOrEmpty(_ccpFacility.Name) && !string.IsNullOrEmpty(_ccrFacility.Name);
                    return IsReady;
                }
                else
                {
                    _logger.LogError("[SabUI:ControlCenterFacilities:LoadData]: Failed to deserialize control center facilities configuration.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SabUI:ControlCenterFacilities:LoadData]: Error loading control center facilities configuration.");
                return false;
            }
        }
    }
}
