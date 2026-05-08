using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace SystemeAideBasculement.Models
{
    public enum EndpointStatus
    {
        [EnumMember(Value = "Connected")]
        Connected,

        [EnumMember(Value = "Disconnected")]
        Disconnected,

        [EnumMember(Value = "Notmonitored")]
        NotMonitored,

        [EnumMember(Value = "Unknown")]
        Unknown
    }


    static public class EndpointValue
    {
        public const string None = "Aucun";
    }

    public sealed class EndpointField
    {
        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public EndpointStatus Status { get; set; } = EndpointStatus.Disconnected;

        // Return an ordred list from CSV Value content.
        public List<string> GetValueList()
        {
                return CSVHelper.SplitCsvOrdered(this.Value);
        }

        public EndpointField Clone() => new()
        {
            Value = this.Value,
            Status = this.Status
        };
    }
}
