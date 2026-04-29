namespace SystemeAideBasculement.Models
{
    public class ProfileConnectionNotificationModel
    {
        public string ProfileName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // ex: "connecté", "déconnecté"
        public List<string> HostNames { get; set; } = new();
        public string Site { get; set; } = string.Empty; // "CCR" ou "CCP"
    }

}
