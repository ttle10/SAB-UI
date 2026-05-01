namespace SystemeAideBasculement.Models
{
    public class SabDataNotification
    {
        public List<SabProfileRow> Profiles { get; set; } = [];

        public List<SabPexRow> Pexs { get;  set; } = [];

        public bool IsEmpty => Profiles.Count == 0 && Pexs.Count == 0;
    }
}
