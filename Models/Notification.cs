using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SPT.Models
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty; // Who receives it
        [ForeignKey("UserId")]

        public ApplicationUser User { get; set; }

        [Required]
        public string Title { get; set; } = "Notification";

        [Required]
        public string Message { get; set; } = string.Empty;

        public string? Url { get; set; } // Link to click (e.g. "/Student/Dashboard")

        public bool IsRead { get; set; } = false;

        public string Type { get; set; } = "Info"; // Info, Alert, Success

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

