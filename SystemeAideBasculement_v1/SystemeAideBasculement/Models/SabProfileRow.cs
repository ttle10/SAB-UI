namespace SystemeAideBasculement.Models
{
    public class SabProfileRow
    {
        public int Index { get; set; } = 0;
        public string Profile { get; set; } = "";
        public string DisplayName { get; set; } = "";

        public Endpoint CCP { get; set; } = new();
        public Endpoint CCR { get; set; } = new();
    }
}
