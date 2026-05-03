using Microsoft.AspNetCore.Http;
using System.Text.Json.Serialization;

namespace SystemeAideBasculement.Models
{
    public class Endpoint
    {
        [JsonPropertyName("poste")]
        public EndpointField PiccNames { get; set; } = new();

        // Used ONLY in SabPexRow (derived state)
        // Must never be written in SabProfileRow context
        [JsonPropertyName("picc")]
        public EndpointField ProfileNames { get; set; } = new();

        [JsonPropertyName("cra")]
        public EndpointField CRA { get; set; } = new();

        [JsonPropertyName("reu")]
        public EndpointField REU { get; set; } = new();

        [JsonPropertyName("sgcz")]
        public EndpointField  SGCZ { get; set; } = new();

        [JsonPropertyName("sti")]
        public EndpointField STI { get; set; } = new();


        public Endpoint()
        {
            // Relationship fields → empty + Disconnected
            PiccNames.Status = EndpointStatus.Disconnected;
            ProfileNames.Status = EndpointStatus.Disconnected;

            // Monitoring fields → NA + NotMonitored
            CRA.Status = EndpointStatus.NotMonitored;
            REU.Status = EndpointStatus.NotMonitored;
            SGCZ.Status = EndpointStatus.NotMonitored;
            STI.Status = EndpointStatus.NotMonitored;
        }

        public Endpoint Clone() => new()
        {
            PiccNames = PiccNames.Clone(),
            ProfileNames = ProfileNames.Clone(),
            CRA = CRA.Clone(),
            REU = REU.Clone(),
            SGCZ = SGCZ.Clone(),
            STI = STI.Clone()
        };
    }
}
