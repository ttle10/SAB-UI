namespace SystemeAideBasculement.Models
{
    public class SabPexRow
    {
        public int DisplayIndex { get; set; } = 0;
        public string Pex { get; set; } = "";

        public string HostName { get; set; } = "";

        public Endpoint CCP { get; set; } = new();
        public Endpoint CCR { get; set; } = new();
    }
}
