using SystemeAideBasculement.Controllers;

namespace SystemeAideBasculement.Models
{
    public class ControlCenterFacility
    {
        public string Name { get; set; } = string.Empty;
        public int FacilityCode { get; set; } = 0;
    }

    public class CCPFacility: ControlCenterFacility
    {
    }

    public class CCRFacility: ControlCenterFacility
    {
    }

    public class ControlCenterFacilities
    {
        private CCPFacility _ccpFacility;
        private CCRFacility _ccrFacility;
        private readonly ILogger<NotificationsController> _logger;

        public ControlCenterFacilities(ILogger<NotificationsController> logger)
        {
            _logger = logger;
            _ccpFacility = new CCPFacility
            {
                FacilityCode = 0
            };
            _ccrFacility = new CCRFacility
            {
                FacilityCode = 1
            };
        }

        public bool LoadData()
        {
            try
            {
                var json = File.ReadAllText("config/controlCenterFacilities.json");
                var facilities = System.Text.Json.JsonSerializer.Deserialize<List<ControlCenterFacility>>(json);
                if (facilities != null)
                {
                    foreach (var item in facilities)
                    {
                        if (item.FacilityCode == _ccpFacility.FacilityCode)
                        { 
                            _ccpFacility.Name = item.Name;
                        }
                        else if (item.FacilityCode == _ccrFacility.FacilityCode)
                        {
                            _ccrFacility.Name = item.Name;
                        }
                    }
                    return !string.IsNullOrEmpty(_ccpFacility.Name) && !string.IsNullOrEmpty(_ccrFacility.Name);
                }
                else
                {
                    _logger.LogError("Failed to deserialize control center facilities configuration.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading control center facilities configuration.");
                return false;
            }
        }

        public bool IsCCPFacility(string facilityName)
        {
            return facilityName.Equals(_ccpFacility.Name, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsCCRFacility(string facilityName)
        {
            return facilityName.Equals(_ccrFacility.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
