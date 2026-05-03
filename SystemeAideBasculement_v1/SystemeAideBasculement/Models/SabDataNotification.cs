namespace SystemeAideBasculement.Models
{
    public class SabDataNotification
    {
        public List<SabProfileRow> Profiles { get; set; } = new List<SabProfileRow>();

        public List<SabPexRow> Pexs { get;  set; } = new List<SabPexRow>();

        public bool IsEmpty => Profiles.Count == 0 && Pexs.Count == 0;
    }
}
