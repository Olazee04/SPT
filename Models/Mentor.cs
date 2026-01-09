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

        public int? TrackId { get; set; }

        [ForeignKey(nameof(TrackId))]
        public Track? Track { get; set; }

        public ICollection<Student> Students { get; set; } = new List<Student>();
        public ICollection<MentorReview> Reviews { get; set; } = new List<MentorReview>();
    }

    // =========================
    // SYLLABUS MODULE
    // =========================
    public class SyllabusModule
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Module Code")]
        public string ModuleCode { get; set; } = string.Empty; // CM1, CM2, etc.

        [Required]
        [Display(Name = "Module Name")]
        public string ModuleName { get; set; } = string.Empty;

        [Required]
        public int TrackId { get; set; }

        [ForeignKey(nameof(TrackId))]
        public Track Track { get; set; } = null!;

        [Display(Name = "Topics")]
        public string? Topics { get; set; } // Can be JSON or plain text

        [Display(Name = "Required Hours")]
        public int RequiredHours { get; set; }

        [Display(Name = "Has Quiz")]
        public bool HasQuiz { get; set; }

        [Display(Name = "Has Project")]
        public bool HasProject { get; set; }

        [Display(Name = "Weight Percentage")]
        public decimal WeightPercentage { get; set; }

        [Display(Name = "Difficulty Level")]
        public string DifficultyLevel { get; set; } = "Beginner"; // Beginner/Intermediate/Advanced

        [Display(Name = "Prerequisite Module")]
        public int? PrerequisiteModuleId { get; set; }

        [ForeignKey(nameof(PrerequisiteModuleId))]
        public SyllabusModule? PrerequisiteModule { get; set; }

        [Display(Name = "Display Order")]
        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<ProgressLog> ProgressLogs { get; set; } = new List<ProgressLog>();
        public ICollection<ModuleCompletion> ModuleCompletions { get; set; } = new List<ModuleCompletion>();
    }

    // =========================
    // PROGRESS LOG (CORE TABLE)
    // =========================
    public class ProgressLog
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

        [Required]
        public DateTime Date { get; set; }

        [Required]
        [Range(0, 24)]
        public decimal Hours { get; set; }

        [Display(Name = "Lesson Covered")]
        public string? LessonCovered { get; set; }

        [Display(Name = "Practice Done")]
        public bool PracticeDone { get; set; }

        [Display(Name = "Mini Project Progress (%)")]
        [Range(0, 100)]
        public int? MiniProjectProgress { get; set; }

        [Display(Name = "Quiz Score (%)")]
        [Range(0, 100)]
        public int? QuizScore { get; set; }

        [Display(Name = "Project Milestone")]
        public string? ProjectMilestone { get; set; }

        [Display(Name = "Blocker")]
        public string? Blocker { get; set; }

        [Display(Name = "Next Goal")]
        public string? NextGoal { get; set; }

        [Display(Name = "Evidence Link")]
        [Url]
        public string? EvidenceLink { get; set; }

        [Required]
        [Display(Name = "Logged By")]
        public string LoggedBy { get; set; } = string.Empty; // Admin or Mentor

        [Required]
        public string LoggedByUserId { get; set; } = string.Empty;

        [Display(Name = "Approved")]
        public bool IsApproved { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    // =========================
    // STUDENT REFLECTION
    // =========================
    public class StudentReflection
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey(nameof(StudentId))]
        public Student Student { get; set; } = null!;

        [Required]
        public DateTime Date { get; set; }

        [Required]
        [Display(Name = "What I Learned Today")]
        public string WhatILearned { get; set; } = string.Empty;

        [Required]
        [Display(Name = "My Struggle")]
        public string MyStruggle { get; set; } = string.Empty;

        [Required]
        [Display(Name = "My Goal Tomorrow")]
        public string MyGoalTomorrow { get; set; } = string.Empty;

        [Display(Name = "Mood")]
        public string Mood { get; set; } = "😐"; // 😊 😐 😞

        [Display(Name = "Self-Rated Understanding (1-5)")]
        [Range(1, 5)]
        public int? SelfRatedUnderstanding { get; set; }

        [Display(Name = "Evidence Link")]
        [Url]
        public string? EvidenceLink { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
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

    // =========================
    // AUDIT LOG
    // =========================
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string TableName { get; set; } = string.Empty;

        [Required]
        public int RecordId { get; set; }

        [Required]
        public string FieldName { get; set; } = string.Empty;

        public string? OldValue { get; set; }

        public string? NewValue { get; set; }

        [Required]
        public string EditedBy { get; set; } = string.Empty;

        [Required]
        public string EditedByUserId { get; set; } = string.Empty;

        public DateTime EditedAt { get; set; } = DateTime.UtcNow;

        public string? IPAddress { get; set; }
    }
}