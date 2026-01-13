using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SPT.Models
{
    public class StudentReflection
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }
        [ForeignKey("StudentId")]
        public Student Student { get; set; } = null!;

        public DateTime Date { get; set; } = DateTime.UtcNow;

        [Required]
        [Display(Name = "What I Learned Today")]
        public string WhatILearned { get; set; } = string.Empty;

        [Display(Name = "My Struggles / Blockers")]
        public string? Struggles { get; set; }

        [Display(Name = "Goal for Tomorrow")]
        public string? GoalTomorrow { get; set; }

        [Display(Name = "Mood")]
        public string Mood { get; set; } = "😐"; // Stores Emoji: 😀, 😐, 😫

        [Range(1, 5)]
        [Display(Name = "Understanding Rating (1-5)")]
        public int UnderstandingRating { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}