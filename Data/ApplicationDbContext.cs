using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SPT.Models;

namespace SPT.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>

    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // ===== Core Tables =====
        public DbSet<Student> Students { get; set; }
        public DbSet<Cohort> Cohorts { get; set; }
        public DbSet<Track> Tracks { get; set; }
        public DbSet<Mentor> Mentors { get; set; }

        public DbSet<ProgressLog> ProgressLogs { get; set; }
        public DbSet<MentorReview> MentorReviews { get; set; }
        public DbSet<ModuleCompletion> ModuleCompletions { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<QuizQuestion> QuizQuestions { get; set; }
        public DbSet<QuizOption> QuizOptions { get; set; }
        public DbSet<StudentReflection> StudentReflections { get; set; }
        public DbSet<SupportTicket> SupportTickets { get; set; }
        public DbSet<Announcement> Announcements { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<ModuleResource> ModuleResources { get; set; }
        public DbSet<SyllabusModule> SyllabusModules { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ===== Student Configuration =====
            builder.Entity<Student>()
                 .HasOne(s => s.User)
                 .WithOne()
                 .HasForeignKey<Student>(s => s.UserId)
                 .OnDelete(DeleteBehavior.Cascade);


            builder.Entity<Student>()
                .HasOne(s => s.Cohort)
                .WithMany(c => c.Students)
                .HasForeignKey(s => s.CohortId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Student>()
                .HasOne(s => s.Track)
                .WithMany(t => t.Students)
                .HasForeignKey(s => s.TrackId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Student>()
                 .HasOne(s => s.Mentor)
                 .WithMany(m => m.Students)
                 .HasForeignKey(s => s.MentorId)
                 .OnDelete(DeleteBehavior.NoAction);


            // ===== Mentor Configuration =====
            builder.Entity<Mentor>()
                  .HasOne(m => m.User)
                  .WithOne()
                  .HasForeignKey<Mentor>(m => m.UserId)
                  .OnDelete(DeleteBehavior.Cascade);


            // ===== Progress Log Configuration =====
            builder.Entity<ProgressLog>()
                .HasOne(p => p.Student)
                .WithMany(s => s.ProgressLogs)
                .HasForeignKey(p => p.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ProgressLog>()
                .HasOne(p => p.Module)
                .WithMany(m => m.ProgressLogs)
                .HasForeignKey(p => p.ModuleId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ProgressLog>()
                .Property(p => p.Hours)
                .HasColumnType("decimal(5,2)");

            // ===== Student Reflection Configuration =====
            builder.Entity<StudentReflection>()
                .HasOne(r => r.Student)
                .WithMany(s => s.Reflections)
                .HasForeignKey(r => r.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            // ===== Mentor Review Configuration =====
            builder.Entity<MentorReview>()
                .HasOne(r => r.Student)
                .WithMany(s => s.MentorReviews)
                .HasForeignKey(r => r.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<MentorReview>()
                .HasOne(r => r.Mentor)
                .WithMany(m => m.Reviews)
                .HasForeignKey(r => r.MentorId)
                .OnDelete(DeleteBehavior.Restrict);

            // ===== Module Completion Configuration =====
            builder.Entity<ModuleCompletion>()
                .HasOne(c => c.Student)
                .WithMany(s => s.ModuleCompletions)
                .HasForeignKey(c => c.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ModuleCompletion>()
                .HasOne(c => c.Module)
                .WithMany(m => m.ModuleCompletions)
                .HasForeignKey(c => c.ModuleId)
                .OnDelete(DeleteBehavior.Restrict);

            // ===== Syllabus Module Configuration =====
            builder.Entity<SyllabusModule>()
                .HasOne(m => m.Track)
                .WithMany(t => t.Modules)
                .HasForeignKey(m => m.TrackId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SyllabusModule>()
                .HasOne(m => m.PrerequisiteModule)
                .WithMany()
                .HasForeignKey(m => m.PrerequisiteModuleId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SyllabusModule>()
                .Property(m => m.WeightPercentage)
                .HasColumnType("decimal(5,2)");

            // ===== Indexes for Performance =====
            builder.Entity<Student>()
                .HasIndex(s => s.Email)
                .IsUnique();

            builder.Entity<Student>()
                .HasIndex(s => s.EnrollmentStatus);

            builder.Entity<ProgressLog>()
                .HasIndex(p => p.Date);

            builder.Entity<ProgressLog>()
                .HasIndex(p => new { p.StudentId, p.Date });

            builder.Entity<AuditLog>()
                .HasIndex(a => a.EditedAt);

            builder.Entity<AuditLog>()
                .HasIndex(a => new { a.TableName, a.RecordId });
        }
    }
}
