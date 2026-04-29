using Microsoft.AspNetCore.Http;

namespace SystemeAideBasculement.Models
{
    static public class EndpointValue
    {
        public const string NotMonitored = "NA";

        public const string None  = "Aucun";
    }

    public class Endpoint
    {
        public string PiccNames { get; set; } = EndpointValue.None;
        public string ProfileNames { get; set; } = EndpointValue.None;
        public string CRA { get; set; } = EndpointValue.NotMonitored;
        public string REU { get; set; } = EndpointValue.NotMonitored;
        public string SGCZ { get; set; } = EndpointValue.NotMonitored;
        public string STI { get; set; } = EndpointValue.NotMonitored;
    }
}
