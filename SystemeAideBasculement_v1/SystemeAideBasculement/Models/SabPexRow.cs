using System.Text.Json.Serialization;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SystemeAideBasculement.Models
{
    public class SabPexRow
    {
        [JsonPropertyName("index")]
        public int Index { get; set; } = 0;

        [JsonPropertyName("ccphostname")]
        public string CCPHostname { get; set; } = "";

        [JsonPropertyName("ccrhostname")]
        public string CCRHostname { get; set; } = "";

        [JsonPropertyName("displayname")]
        public string DisplayName { get; set; } = "";

        [JsonPropertyName("ccp")]
        public Endpoint CCP { get; set; } = new();

        [JsonPropertyName("ccr")]
        public Endpoint CCR { get; set; } = new();

        public SabPexRow Clone() => new()
        {
            Index = Index,
            CCPHostname = CCPHostname,
            CCRHostname = CCRHostname,
            DisplayName = DisplayName,
            CCP = CCP.Clone(),
            CCR = CCR.Clone()
        };
    }

    public class SabPexTransform
    {
        public string Hostname { get; set; } = "";
        public List<string> ProfileNames { get; set; } = [];
        public string Site { get; set; } = string.Empty; // "CCR" ou "CCP"
    }
}
