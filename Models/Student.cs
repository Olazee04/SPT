using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Drawing;

namespace SPT.Models
{
    public class Student
    {
        [Key]
        public int Id { get; set; }

        public string? UserId { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }

        // ===== Personal Information =====
        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Phone]
        [Display(Name = "Phone Number")]
        public string Phone { get; set; } = string.Empty;

        [Required]
        public string Address { get; set; } = string.Empty;

        [Display(Name = "Profile Picture")]
        public string? ProfilePicture { get; set; }

        // ===== Academic Information =====
        // We set this automatically in Controller, so allow null in form
        public int? CohortId { get; set; }

        [ForeignKey("CohortId")]
        public Cohort? Cohort { get; set; }

        [Required]
        public int TrackId { get; set; }

        [ForeignKey("TrackId")]
        public Track? Track { get; set; }

        public int? MentorId { get; set; } // Optional (Assign Later)

        [ForeignKey("MentorId")]
        public Mentor? Mentor { get; set; }

        [Required]
        public DateTime DateJoined { get; set; } = DateTime.UtcNow;

        public int TargetHoursPerWeek { get; set; } = 25; // Default constant

        public string EnrollmentStatus { get; set; } = "Active";

        // ===== Optional URLs =====
        [Url]
        [Display(Name = "GitHub Profile")]
        public string? GitHubUrl { get; set; }

        [Url]
        [Display(Name = "Portfolio URL")]
        public string? PortfolioUrl { get; set; }

        // ===== Emergency Contact =====
        [Required]
        [Display(Name = "Emergency Contact Name")]
        public string EmergencyContactName { get; set; } = string.Empty;

        [Required]
        [Phone]
        [Display(Name = "Emergency Contact Phone")]
        public string EmergencyContactPhone { get; set; } = string.Empty;

        [Display(Name = "Emergency Contact Address")]
        public string? EmergencyContactAddress { get; set; }

        // ===== Timestamps =====
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // ===== Navigation Properties =====
        public ICollection<ProgressLog> ProgressLogs { get; set; } = new List<ProgressLog>();
        public ICollection<StudentReflection> Reflections { get; set; } = new List<StudentReflection>();
        public ICollection<MentorReview> MentorReviews { get; set; } = new List<MentorReview>();
        public ICollection<ModuleCompletion> ModuleCompletions { get; set; } = new List<ModuleCompletion>();
    }
}