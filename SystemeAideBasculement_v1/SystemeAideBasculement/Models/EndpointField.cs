namespace SystemeAideBasculement.Models
{
    public enum EndpointStatus
    {
        Connected,
        Disconnected,
        NotMonitored,
        Unknown
    }

    static public class EndpointValue
    {
        public const string NotMonitored = "NA";

        public const string None = "Aucun";
    }

    public sealed class EndpointField
    {
        public string Value { get; set; } = EndpointValue.NotMonitored;
        public EndpointStatus Status { get; set; } = EndpointStatus.NotMonitored;

        public EndpointField Clone() => new()
        {
            Value = Value,
            Status = Status
        };
    }
}
