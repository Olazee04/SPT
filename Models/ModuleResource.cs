using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SPT.Models
{
    public class ModuleResource
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ModuleId { get; set; }
        [ForeignKey("ModuleId")]
        public SyllabusModule Module { get; set; } = null!;

        [Required]
        public string Title { get; set; } = string.Empty; // e.g. "C# Tutorial Playlist"

        [Required]
        [Url]
        public string Url { get; set; } = string.Empty;

        public string Type { get; set; } = "Video"; // Video, Article, eBook, GitRepo

        public bool IsActive { get; set; } = true;
    }
}