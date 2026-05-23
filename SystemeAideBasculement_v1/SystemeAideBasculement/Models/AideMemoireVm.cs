namespace SystemeAideBasculement.Models
{
    public class AideMemoireVm
    {
        public AideMemoireVm()
        { 
        }

        public AideMemoireVm(AideMemoireVm vm)
        {
            Id = vm.Id;
            Step = vm.Step;
            Initials = vm.Initials;
            IsCompleted = vm.IsCompleted;
        }
        public int Id { get; set; }
        public string? Step { get; set; } = string.Empty;
        public string? Initials { get; set; }
        public bool IsCompleted { get; set; }
    }

    public static class AideMemoireMapping
    {
        public static AideMemoireVm ToVm(this AideMemoireModel e) => new()
        {
            Id = e.Id,
            Step = e.Step,
            Initials = e.Initials,
            IsCompleted = e.IsCompleted
        };
    }
}
