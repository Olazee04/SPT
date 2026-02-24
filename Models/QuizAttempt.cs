using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SPT.Models
{
    public class QuizAttempt
    {
        [Key]
        public int Id { get; set; }

        public int StudentId { get; set; }
        [ForeignKey("StudentId")]
        public Student Student { get; set; } = null!;

        public int ModuleId { get; set; }
        [ForeignKey("ModuleId")]
        public SyllabusModule Module { get; set; } = null!;

        public int Score { get; set; } // percentage 0-100

        public bool Passed { get; set; }

        public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
    }
}