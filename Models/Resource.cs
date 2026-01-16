using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SPT.Models
{
    public class Resource
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Required]
        public string Url { get; set; } = string.Empty; // Could be external link or internal /uploads/ path

        public string Type { get; set; } = "Link"; // "Video", "PDF", "Link"

        // Link to a specific Track (so Fullstack students don't see Data Science PDFs)
        public int TrackId { get; set; }

        [ForeignKey("TrackId")]
        public Track? Track { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}