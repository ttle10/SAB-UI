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
            Index = this.Index,
            CCPHostname = this.CCPHostname,
            CCRHostname = this.CCRHostname,
            DisplayName = this.DisplayName,
            CCP = this.CCP.Clone(),
            CCR = this.CCR.Clone()
        };
    }
}
