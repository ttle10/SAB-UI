namespace SystemeAideBasculement.Models
{
    internal sealed class PexSiteState
    {
        public HashSet<string> ProfileNames { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public EndpointStatus Status =>
            ProfileNames.Count == 0
                ? EndpointStatus.Disconnected
                : EndpointStatus.Connected;
    }

    internal sealed class PexIndexEntry
    {
        public int RowIndex { get; init; }
        public string CCPHostname { get; init; } = string.Empty;
        public string CCRHostname { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;

        public PexSiteState CCP { get; } = new();
        public PexSiteState CCR { get; } = new();
    }
}
