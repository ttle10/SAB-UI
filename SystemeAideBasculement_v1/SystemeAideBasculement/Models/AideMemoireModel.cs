using System.ComponentModel.DataAnnotations;

namespace SystemeAideBasculement.Models
{
    public class AideMemoireModel
    {
        [Key]
        public int Id { get; set; }

        public string? Step { get; set; } = string.Empty;
        
        public string? Initials { get; set; } = string.Empty;

        public bool IsCompleted { get; set; } = false;

        public int DisplayOrder { get; set; } = 0;

        public bool IsDeleted { get; set; } = false;

        public static int StepMaxLength
        {
            get {
                return 500;
            }
        }
    }

}
