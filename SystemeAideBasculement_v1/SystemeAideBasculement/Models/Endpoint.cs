using Microsoft.AspNetCore.Http;
using System.Text.Json.Serialization;

namespace SystemeAideBasculement.Models
{
    static public class EndpointValue
    {
        public const string NotMonitored = "NA";

        public const string None  = "Aucun";
    }

    public class Endpoint
    {
        [JsonPropertyName("poste")]
        public string PiccNames { get; set; } = EndpointValue.None;

        public string ProfileNames { get; set; } = EndpointValue.None;

        [JsonPropertyName("cra")]
        public string CRA { get; set; } = EndpointValue.NotMonitored;

        [JsonPropertyName("reu")]
        public string REU { get; set; } = EndpointValue.NotMonitored;

        [JsonPropertyName("sgcz")]
        public string SGCZ { get; set; } = EndpointValue.NotMonitored;

        [JsonPropertyName("sti")]
        public string STI { get; set; } = EndpointValue.NotMonitored;

        public Endpoint Clone() => new()
        {
            PiccNames = PiccNames,
            ProfileNames = ProfileNames,
            CRA = CRA,
            REU = REU,
            SGCZ = SGCZ,
            STI = STI
        };
    }
}
