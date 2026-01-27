using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SPT.Models
{
    public class Capstone
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey(nameof(StudentId))]
        public Student Student { get; set; } = null!;

        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        public string? RepositoryUrl { get; set; }
        public string? LiveDemoUrl { get; set; }

        // Mentor side
        public string? MentorFeedback { get; set; }

        public CapstoneStatus Status { get; set; } = CapstoneStatus.Pending;

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }
    }

    public enum CapstoneStatus
    {
        Pending,
        Approved,
        Rejected
    }
}
