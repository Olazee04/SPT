using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SPT.Models
{
    public class QuizQuestion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ModuleId { get; set; }
        [ForeignKey("ModuleId")]
        public SyllabusModule Module { get; set; } = null!;

        [Required]
        public string QuestionText { get; set; } = string.Empty;

        // Navigation Property for Options
        public ICollection<QuizOption> Options { get; set; } = new List<QuizOption>();
    }
}