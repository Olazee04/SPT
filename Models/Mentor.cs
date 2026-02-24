using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SPT.Models
{
    // =========================
    // COHORT
    // =========================
    public class Cohort
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Cohort Name")]
        public string Name { get; set; } = string.Empty; // e.g., "FS-2025A"

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<Student> Students { get; set; } = new List<Student>();
    }

    // =========================
    // TRACK
    // =========================

    public class Track
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Track Name")]
        public string Name { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty; // e.g., "FS", "BE"

        [Display(Name = "Description")]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<Student> Students { get; set; } = new List<Student>();
        public ICollection<SyllabusModule> Modules { get; set; } = new List<SyllabusModule>();
    }

    // =========================
    // MENTOR
    // =========================
    public class Mentor
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public ApplicationUser User { get; set; } = null!;

        [Required]
        public string FullName { get; set; } = string.Empty;
        public string? Address { get; set; }

        public string? Phone { get; set; }

        public string? ProfilePicture { get; set; }

        public string? NextOfKin { get; set; }

        public string? NextOfKinPhone { get; set; }
        public string? NextOfKinAddress { get; set; }


        [Required]

        [Display(Name = "Date Joined")]

        public DateTime DateJoined { get; set; } = DateTime.UtcNow;
        public bool DarkMode { get; set; } = false;
        public int? TrackId { get; set; }

        [ForeignKey(nameof(TrackId))]
        public Track? Track { get; set; }

        public string? Specialization { get; set; } // e.g. "Backend", "Data Science"
        public string? Bio { get; set; }

        public ICollection<Student> Students { get; set; } = new List<Student>();
        public ICollection<MentorReview> Reviews { get; set; } = new List<MentorReview>();
    }


    // =========================
    // MENTOR REVIEW
    // =========================
    public class MentorReview
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey(nameof(StudentId))]
        public Student Student { get; set; } = null!;

        [Required]
        public int MentorId { get; set; }

        [ForeignKey(nameof(MentorId))]
        public Mentor Mentor { get; set; } = null!;

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public string Comments { get; set; } = string.Empty;

        [Required]
        [Range(0, 10)]
        public int Score { get; set; }

        [Display(Name = "Action Items")]
        public string? ActionItems { get; set; }

        [Display(Name = "Review Type")]
        public string ReviewType { get; set; } = "Weekly"; // Weekly/Monthly

        [Display(Name = "Visible to Student")]
        public bool IsVisibleToStudent { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // =========================
    // MODULE COMPLETION
    // =========================
    public class ModuleCompletion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey(nameof(StudentId))]
        public Student Student { get; set; } = null!;

        [Required]
        public int ModuleId { get; set; }

        [ForeignKey(nameof(ModuleId))]
        public SyllabusModule Module { get; set; } = null!;

        public bool IsCompleted { get; set; }

        [Display(Name = "Quiz Completed")]
        public bool QuizCompleted { get; set; }

        [Display(Name = "Project Completed")]
        public bool ProjectCompleted { get; set; }

        [Display(Name = "Completion Date")]
        public DateTime? CompletionDate { get; set; }

        [Display(Name = "Verified By")]
        public string? VerifiedBy { get; set; } // Admin or Mentor

        public string? VerifiedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}