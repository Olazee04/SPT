// ============================================================
// NEW FILE: Services/InactivityReminderService.cs
// ============================================================

using Microsoft.EntityFrameworkCore;
using SPT.Data;

namespace SPT.Services
{
    public class InactivityReminderService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<InactivityReminderService> _logger;

        public InactivityReminderService(IServiceProvider services, ILogger<InactivityReminderService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Run once per day at 9am UTC
                    var now = DateTime.UtcNow;
                    var nextRun = now.Date.AddDays(1).AddHours(9);
                    var delay = nextRun - now;

                    // If it's already past 9am today, schedule for tomorrow
                    if (delay.TotalMilliseconds <= 0)
                        delay = TimeSpan.FromHours(24);

                    await Task.Delay(delay, stoppingToken);

                    await SendRemindersAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in InactivityReminderService");
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }
        }

        private async Task SendRemindersAsync()
        {
            using var scope = _services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailNotif = scope.ServiceProvider.GetRequiredService<EmailNotificationService>();
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<SPT.Models.ApplicationUser>>();

            var threeDaysAgo = DateTime.UtcNow.Date.AddDays(-3);

            // ── Students inactive for 3+ days ─────────────────────────────────
            var activeStudents = await context.Students
                .Include(s => s.ProgressLogs)
                .Where(s => s.EnrollmentStatus == "Active")
                .ToListAsync();

            foreach (var student in activeStudents)
            {
                var lastLog = student.ProgressLogs
                    .OrderByDescending(l => l.Date)
                    .FirstOrDefault();

                DateTime lastActivity = lastLog?.Date.Date ?? student.DateJoined.Date;
                int daysSince = (DateTime.UtcNow.Date - lastActivity).Days;

                if (daysSince >= 3 && !string.IsNullOrWhiteSpace(student.Email))
                {
                    await emailNotif.SendInactiveStudentReminderAsync(
                        student.Email,
                        student.FullName,
                        daysSince);
                    _logger.LogInformation("Sent inactivity reminder to student: {Name}", student.FullName);
                }
            }

            // ── Mentors inactive for 3+ days (track via AuditLogs) ───────────
            var mentors = await context.Mentors
                .Include(m => m.User)
                .ToListAsync();

            foreach (var mentor in mentors)
            {
                if (mentor.User == null || string.IsNullOrWhiteSpace(mentor.User.Email)) continue;

                // Check last audit log entry for this mentor (using UserId if it exists, fallback to PerformedBy username)
                var lastAudit = await context.AuditLogs
                    .Where(a => a.PerformedBy == mentor.User.UserName)
                    .OrderByDescending(a => a.Timestamp)
                    .FirstOrDefaultAsync();

                DateTime lastActivity = lastAudit?.Timestamp.Date ?? mentor.DateJoined.Date;
                int daysSince = (DateTime.UtcNow.Date - lastActivity).Days;

                if (daysSince >= 3)
                {
                    await emailNotif.SendInactiveMentorReminderAsync(
                        mentor.User.Email,
                        mentor.FullName,
                        daysSince);
                    _logger.LogInformation("Sent inactivity reminder to mentor: {Name}", mentor.FullName);
                }
            }
        }
    }
}