using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SPT.Models
{
    public class SyllabusModule
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Module Code")]
        public string ModuleCode { get; set; } = string.Empty; // e.g. "C#-01"

        [Required]
        [Display(Name = "Module Name")]
        public string ModuleName { get; set; } = string.Empty;

        [Required]
        public int TrackId { get; set; }

        [ForeignKey("TrackId")]
        public Track Track { get; set; } = null!;

        [Display(Name = "Topics")]
        public string? Topics { get; set; }

        [Display(Name = "Required Hours")]
        public int RequiredHours { get; set; }

        public bool HasQuiz { get; set; }

        public bool HasProject { get; set; }

        // 🆕 NEW: Triggers the 0-100% Slider in Progress Log
        public bool IsMiniProject { get; set; }

        [Display(Name = "Weight Percentage")]
        public decimal WeightPercentage { get; set; }

        [Display(Name = "Difficulty Level")]
        public string DifficultyLevel { get; set; } = "Beginner";

        [Display(Name = "Prerequisite Module")]
        public int? PrerequisiteModuleId { get; set; }

        [ForeignKey("PrerequisiteModuleId")]
        public SyllabusModule? PrerequisiteModule { get; set; }

        [Display(Name = "Display Order")]
        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; } = true;

        // =========================
        // NAVIGATION PROPERTIES (Links to other tables)
        // =========================
        public ICollection<ProgressLog> ProgressLogs { get; set; } = new List<ProgressLog>();
        public ICollection<ModuleCompletion> ModuleCompletions { get; set; } = new List<ModuleCompletion>();

        // 🆕 NEW: Links to the Resource Library (Videos/PDFs)
        public ICollection<ModuleResource> Resources { get; set; } = new List<ModuleResource>();

        // 🆕 NEW: Links to the Quiz Questions
        public ICollection<QuizQuestion> Questions { get; set; } = new List<QuizQuestion>();
    }
}