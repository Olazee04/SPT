using System.ComponentModel.DataAnnotations;

namespace SPT.Models
{
    public class Announcement
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public bool IsRead { get; set; }
        public string? TargetPage { get; set; } // Dashboard, Curriculum, Support
        public string Audience { get; set; } = "All"; // "All", "Students", "Mentors"

        public string PostedBy { get; set; } = string.Empty; // Admin Name

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}