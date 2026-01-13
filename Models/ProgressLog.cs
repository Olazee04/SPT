using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SPT.Models
{
    public class ProgressLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }
        [ForeignKey("StudentId")]
        public Student Student { get; set; } = null!;

        [Required]
        public int ModuleId { get; set; }
        [ForeignKey("ModuleId")]
        public SyllabusModule Module { get; set; } = null!;

        [Required]
        public DateTime Date { get; set; }

        [Required]
        [Display(Name = "Work Location")]
        public string Location { get; set; } = "Remote"; // Default to Remote


        [Required]
        [Range(0, 24)]
        public decimal Hours { get; set; }

        public string? LessonCovered { get; set; }
        public bool PracticeDone { get; set; }
        public int? QuizScore { get; set; }
        public int? MiniProjectProgress { get; set; }
        public string? Blocker { get; set; }
        public string? NextGoal { get; set; }
        public string? EvidenceLink { get; set; }

        public string LoggedBy { get; set; } = string.Empty;
        public string LoggedByUserId { get; set; } = string.Empty;

        public bool IsApproved { get; set; } = false;

        // ✅ This property is here, so it will work once the duplicate is gone
        public string? VerifiedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}