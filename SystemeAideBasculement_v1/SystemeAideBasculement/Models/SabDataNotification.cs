namespace SystemeAideBasculement.Models
{
    public class SabDataNotification
    {
        public List<SabProfileRow> Profiles { get; private set; } = [];

        public List<SabPexRow> Pexs { get; private set; } = [];

        public bool IsEmpty => Profiles.Count == 0 && Pexs.Count == 0;
    }
}
