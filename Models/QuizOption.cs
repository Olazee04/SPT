using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SPT.Models
{
    public class QuizOption
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int QuestionId { get; set; }
        [ForeignKey("QuestionId")]
        public QuizQuestion Question { get; set; } = null!;

        [Required]
        public string OptionText { get; set; } = string.Empty;

        public bool IsCorrect { get; set; }
    }
}