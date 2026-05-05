using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace SystemeAideBasculement.Models
{
    public enum ProfileConnectionStatus
    {
        [EnumMember(Value = "Connected")]
        Connected = 0,

        [EnumMember(Value = "Disconnected")]
        Disconnected = 1
        // NOTE: Server may send 2 (Unknown)
        // We intentionally do NOT expose it as a valid state
    }

    public class ProfileConnectionNotificationModel
    {
        [JsonPropertyName("Name")]
        public string ProfileName { get; set; } = string.Empty;

        [JsonPropertyName("HostNames")]
        public List<string> HostNames { get; set; } = new();

        [JsonPropertyName("Status")]
        public ProfileConnectionStatus Status { get; set; } = ProfileConnectionStatus.Disconnected;

        [JsonPropertyName("SiteName")]
        public string Site { get; set; } = string.Empty;

        // Optional fields (available for future use / logging)
        [JsonPropertyName("Localization")]
        public string Localization { get; set; } = string.Empty;

        [JsonPropertyName("SubLocalization")]
        public string SubLocalization { get; set; } = string.Empty;

        public bool IsConnected()
        {
            return Status == ProfileConnectionStatus.Connected;
        }

        public string GetKey()
            => $"{ProfileName}:{Site}:{Status}";

    }
}
