using System;
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
        [JsonPropertyName("ProfileName")]
        public string ProfileName { get; set; } = string.Empty;

        [JsonPropertyName("HostNames")]
        public List<string> HostNames { get; set; } = new();

        [JsonPropertyName("Status")]
        public string Status { get; set; } = ProfileConnectionStatus.Disconnected.ToString();

        [JsonPropertyName("Site")]
        public string Site { get; set; } = string.Empty;

        // Optional fields (available for future use / logging)
        [JsonPropertyName("Localization")]
        public string Localization { get; set; } = string.Empty;

        [JsonPropertyName("SubLocalization")]
        public string SubLocalization { get; set; } = string.Empty;

        public bool IsConnected()
        {
            return string.Equals(Status, ProfileConnectionStatus.Connected.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public bool IsDisconnected()
        {
            return string.Equals(Status, ProfileConnectionStatus.Disconnected.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public string GetKey()
            => $"{ProfileName}:{Site}:{Status}";
    }
}
