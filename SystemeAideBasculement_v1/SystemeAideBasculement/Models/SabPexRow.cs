namespace SystemeAideBasculement.Models
{
    public class SabPexRow
    {
        public int Index { get; set; } = 0;
        public string CCPHostname { get; set; } = "";
        public string CCRHostname { get; set; } = "";
        public string DisplayName { get; set; } = "";

        public Endpoint CCP { get; set; } = new();
        public Endpoint CCR { get; set; } = new();
    }
}
