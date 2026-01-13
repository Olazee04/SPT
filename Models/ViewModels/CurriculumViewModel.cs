namespace SPT.Models
{
    public class CurriculumViewModel
    {
        public int ModuleId { get; set; }
        public string ModuleCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int RequiredHours { get; set; }
        public string Difficulty { get; set; } = "Beginner";

        public bool IsCompleted { get; set; } // Passed the Quiz?
        public bool IsLocked { get; set; }    // Is previous module done?
        public int? QuizScore { get; set; }   // If they took it
    }
}