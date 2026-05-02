using System.Text.Json.Serialization;

namespace SystemeAideBasculement.Models
{
    public class SabProfileRow
    {
        [JsonPropertyName("index")]
        public int Index { get; init; }

        [JsonPropertyName("profile")]
        public string Profile { get; init; } = "";

        [JsonPropertyName("displayname")]
        public string DisplayName { get; set; } = "";

        [JsonPropertyName("ccp")]
        public Endpoint CCP { get; set; } = new();

        [JsonPropertyName("ccr")]
        public Endpoint CCR { get; set; } = new();

        public SabProfileRow Clone() => new()
        {
            Index = Index,
            Profile = Profile,
            DisplayName = DisplayName,
            CCP = CCP.Clone(),
            CCR = CCR.Clone()
        };
    }
}
