using SPT.Data;
using Microsoft.EntityFrameworkCore;

namespace SPT.Services
{
    public class EmailNotificationService
    {
        private readonly IEmailService _emailService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EmailNotificationService> _logger;

        private const string LOGIN_URL = "https://rmsysspt.onrender.com";

        public EmailNotificationService(
            IEmailService emailService,
            ApplicationDbContext context,
            ILogger<EmailNotificationService> logger)
        {
            _emailService = emailService;
            _context = context;
            _logger = logger;
        }

        // ── Generic: "You have a notification, login to view it" ──────────────
        public async Task SendNotificationAlertAsync(string toEmail, string title, string message)
        {
            try
            {
                string body = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;padding:20px;border:1px solid #ddd;border-radius:8px;'>
    <h2 style='color:#0d6efd;'>🔔 New Notification</h2>
    <p>You have a new notification on the <strong>RMSys SPT Academy</strong> portal.</p>
    <div style='background:#f8f9fa;border-left:4px solid #0d6efd;padding:15px;margin:16px 0;border-radius:4px;'>
        <h3 style='margin:0 0 8px 0;color:#212529;'>{title}</h3>
        <p style='margin:0;color:#495057;'>{message}</p>
    </div>
    <p>Please login to view the full details.</p>
    <p><a href='{LOGIN_URL}' style='background:#0d6efd;color:white;padding:10px 20px;border-radius:5px;text-decoration:none;display:inline-block;'>Login to Dashboard</a></p>
    <hr/>
    <p style='color:#6c757d;font-size:0.85rem;'>This is an automated notification from RMSys SPT Academy.</p>
</div>";
                await _emailService.SendEmailAsync(toEmail, $"🔔 SPT Notification: {title}", body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Notification email failed to {Email}: {Msg}", toEmail, ex.Message);
            }
        }

        // ── Progress log submitted → notify admin + mentor ───────────────────
        public async Task SendLogSubmittedAsync(string studentUsername, string studentFullName, DateTime logDate, decimal hours, string adminEmail, string? mentorEmail)
        {
            string subject = $"📋 New Progress Log: {studentUsername}";
            string body = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;padding:20px;border:1px solid #ddd;border-radius:8px;'>
    <h2 style='color:#0d6efd;'>📋 New Progress Log Submitted</h2>
    <p><strong>{studentFullName}</strong> (username: <strong>{studentUsername}</strong>) has submitted a progress log.</p>
    <table style='border-collapse:collapse;width:100%;margin:16px 0;'>
        <tr style='background:#f8f9fa;'>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>Date</td>
            <td style='padding:10px;border:1px solid #dee2e6;'>{logDate:MMM dd, yyyy}</td>
        </tr>
        <tr>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>Hours Logged</td>
            <td style='padding:10px;border:1px solid #dee2e6;'>{hours} hrs</td>
        </tr>
    </table>
    <p>Please login to review and approve the log.</p>
    <p><a href='{LOGIN_URL}' style='background:#0d6efd;color:white;padding:10px 20px;border-radius:5px;text-decoration:none;display:inline-block;'>Login to Review</a></p>
    <hr/>
    <p style='color:#6c757d;font-size:0.85rem;'>This is an automated notification from RMSys SPT Academy.</p>
</div>";

            try { await _emailService.SendEmailAsync(adminEmail, subject, body); } catch { }

            if (!string.IsNullOrWhiteSpace(mentorEmail) && mentorEmail != adminEmail)
            {
                try { await _emailService.SendEmailAsync(mentorEmail, subject, body); } catch { }
            }
        }

        public async Task SendLogApprovedAsync(string studentEmail, string studentFullName, DateTime logDate, decimal hours, int rank)
        {
            try
            {
                string body = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;padding:20px;border:1px solid #ddd;border-radius:8px;'>
    <h2 style='color:#198754;'>✅ Progress Log Approved</h2>
    <p>Hi <strong>{studentFullName}</strong>,</p>
    <p>Your progress log has been approved!</p>
    <table style='border-collapse:collapse;width:100%;margin:16px 0;'>
        <tr style='background:#f8f9fa;'>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>Date</td>
            <td style='padding:10px;border:1px solid #dee2e6;'>{logDate:MMM dd, yyyy}</td>
        </tr>
        <tr>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>Hours Approved</td>
            <td style='padding:10px;border:1px solid #dee2e6;'>{hours} hrs</td>
        </tr>
        <tr style='background:#f8f9fa;'>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>Your Leaderboard Rank</td>
            <td style='padding:10px;border:1px solid #dee2e6;'>#{rank}</td>
        </tr>
    </table>
    <p>Login to see your updated progress and leaderboard position.</p>
    <p><a href='{LOGIN_URL}' style='background:#198754;color:white;padding:10px 20px;border-radius:5px;text-decoration:none;display:inline-block;'>View Dashboard</a></p>
    <hr/>
    <p style='color:#6c757d;font-size:0.85rem;'>This is an automated notification from RMSys SPT Academy.</p>
</div>";
                await _emailService.SendEmailAsync(studentEmail, "✅ Your Progress Log Has Been Approved", body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Log approved email failed: {Msg}", ex.Message);
            }
        }

        // ── Log deleted → notify student ─────────────────────────────────────
        public async Task SendLogDeletedAsync(string studentEmail, string studentFullName, DateTime logDate, decimal hours)
        {
            try
            {
                string body = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;padding:20px;border:1px solid #ddd;border-radius:8px;'>
    <h2 style='color:#dc3545;'>🗑️ Progress Log Removed</h2>
    <p>Hi <strong>{studentFullName}</strong>,</p>
    <p>A progress log has been removed from your record by an administrator.</p>
    <table style='border-collapse:collapse;width:100%;margin:16px 0;'>
        <tr style='background:#f8f9fa;'>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>Date</td>
            <td style='padding:10px;border:1px solid #dee2e6;'>{logDate:MMM dd, yyyy}</td>
        </tr>
        <tr>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>Hours Removed</td>
            <td style='padding:10px;border:1px solid #dee2e6;'>{hours} hrs</td>
        </tr>
    </table>
    <p>If you have questions about this action, please contact your mentor or admin.</p>
    <p><a href='{LOGIN_URL}' style='background:#0d6efd;color:white;padding:10px 20px;border-radius:5px;text-decoration:none;display:inline-block;'>Login to Dashboard</a></p>
    <hr/>
    <p style='color:#6c757d;font-size:0.85rem;'>This is an automated notification from RMSys SPT Academy.</p>
</div>";
                await _emailService.SendEmailAsync(studentEmail, "🗑️ A Progress Log Has Been Removed", body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Log deleted email failed: {Msg}", ex.Message);
            }
        }

        // ── Support ticket created → notify admin + mentor ───────────────────
        public async Task SendSupportTicketCreatedAsync(string studentFullName, string subject, string message, string adminEmail, string? mentorEmail)
        {
            string emailSubject = $"🎫 New Support Ticket: {studentFullName}";
            string body = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;padding:20px;border:1px solid #ddd;border-radius:8px;'>
    <h2 style='color:#fd7e14;'>🎫 New Support Ticket</h2>
    <p><strong>{studentFullName}</strong> has submitted a support ticket.</p>
    <table style='border-collapse:collapse;width:100%;margin:16px 0;'>
        <tr style='background:#f8f9fa;'>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>Subject</td>
            <td style='padding:10px;border:1px solid #dee2e6;'>{subject}</td>
        </tr>
        <tr>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>Message</td>
            <td style='padding:10px;border:1px solid #dee2e6;'>{message}</td>
        </tr>
    </table>
    <p>Please login to respond to this ticket.</p>
    <p><a href='{LOGIN_URL}' style='background:#fd7e14;color:white;padding:10px 20px;border-radius:5px;text-decoration:none;display:inline-block;'>Login to Respond</a></p>
    <hr/>
    <p style='color:#6c757d;font-size:0.85rem;'>This is an automated notification from RMSys SPT Academy.</p>
</div>";

            try { await _emailService.SendEmailAsync(adminEmail, emailSubject, body); } catch { }
            if (!string.IsNullOrWhiteSpace(mentorEmail) && mentorEmail != adminEmail)
            {
                try { await _emailService.SendEmailAsync(mentorEmail, emailSubject, body); } catch { }
            }
        }

        // ── Support ticket response → notify student ──────────────────────────
        public async Task SendSupportTicketResponseAsync(string studentEmail, string studentFullName, string ticketSubject, string adminResponse)
        {
            try
            {
                string body = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;padding:20px;border:1px solid #ddd;border-radius:8px;'>
    <h2 style='color:#0d6efd;'>💬 Support Ticket Update</h2>
    <p>Hi <strong>{studentFullName}</strong>,</p>
    <p>Your support ticket has received a response.</p>
    <table style='border-collapse:collapse;width:100%;margin:16px 0;'>
        <tr style='background:#f8f9fa;'>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>Ticket</td>
            <td style='padding:10px;border:1px solid #dee2e6;'>{ticketSubject}</td>
        </tr>
        <tr>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>Response</td>
            <td style='padding:10px;border:1px solid #dee2e6;'>{adminResponse}</td>
        </tr>
    </table>
    <p>Login to view the full conversation.</p>
    <p><a href='{LOGIN_URL}' style='background:#0d6efd;color:white;padding:10px 20px;border-radius:5px;text-decoration:none;display:inline-block;'>Login to View</a></p>
    <hr/>
    <p style='color:#6c757d;font-size:0.85rem;'>This is an automated notification from RMSys SPT Academy.</p>
</div>";
                await _emailService.SendEmailAsync(studentEmail, $"💬 Response to Your Ticket: {ticketSubject}", body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Ticket response email failed: {Msg}", ex.Message);
            }
        }

        // ── New resource added → notify all students on that track ───────────
        public async Task SendNewResourceAddedAsync(string trackName, string resourceTitle, string resourceUrl, List<string> studentEmails)
        {
            string subject = $"📚 New Resource Added: {resourceTitle}";
            string body = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;padding:20px;border:1px solid #ddd;border-radius:8px;'>
    <h2 style='color:#0d6efd;'>📚 New Study Resource Available</h2>
    <p>A new resource has been added to your <strong>{trackName}</strong> curriculum.</p>
    <table style='border-collapse:collapse;width:100%;margin:16px 0;'>
        <tr style='background:#f8f9fa;'>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>Resource</td>
            <td style='padding:10px;border:1px solid #dee2e6;'>{resourceTitle}</td>
        </tr>
    </table>
    <p>Login to access it in your curriculum.</p>
    <p><a href='{LOGIN_URL}' style='background:#0d6efd;color:white;padding:10px 20px;border-radius:5px;text-decoration:none;display:inline-block;'>View Curriculum</a></p>
    <hr/>
    <p style='color:#6c757d;font-size:0.85rem;'>This is an automated notification from RMSys SPT Academy.</p>
</div>";

            foreach (var email in studentEmails)
            {
                try { await _emailService.SendEmailAsync(email, subject, body); } catch { }
            }
        }

        // ── Inactive student reminder ─────────────────────────────────────────
        public async Task SendInactiveStudentReminderAsync(string studentEmail, string studentFullName, int daysSinceLastLog)
        {
            try
            {
                string body = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;padding:20px;border:1px solid #ddd;border-radius:8px;'>
    <h2 style='color:#fd7e14;'>⏰ Don't Fall Behind!</h2>
    <p>Hi <strong>{studentFullName}</strong>,</p>
    <p>We noticed you haven't logged any progress in the last <strong>{daysSinceLastLog} days</strong>.</p>
    <p>Consistency is key to completing your programme. Even 30 minutes of study a day makes a big difference!</p>
    <p>Login now to log your progress and stay on track.</p>
    <p><a href='{LOGIN_URL}' style='background:#fd7e14;color:white;padding:10px 20px;border-radius:5px;text-decoration:none;display:inline-block;'>Log Your Progress</a></p>
    <hr/>
    <p style='color:#6c757d;font-size:0.85rem;'>This is an automated reminder from RMSys SPT Academy.</p>
</div>";
                await _emailService.SendEmailAsync(studentEmail, "⏰ You haven't logged progress recently", body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Inactive student reminder failed: {Msg}", ex.Message);
            }
        }

        // ── Inactive mentor reminder ──────────────────────────────────────────
        public async Task SendInactiveMentorReminderAsync(string mentorEmail, string mentorFullName, int daysSinceLastLogin)
        {
            try
            {
                string body = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;padding:20px;border:1px solid #ddd;border-radius:8px;'>
    <h2 style='color:#fd7e14;'>⏰ Your Students Need You!</h2>
    <p>Hi <strong>{mentorFullName}</strong>,</p>
    <p>You haven't logged into the SPT Academy portal in the last <strong>{daysSinceLastLogin} days</strong>.</p>
    <p>Your students may have pending progress logs waiting for your review and approval.</p>
    <p><a href='{LOGIN_URL}' style='background:#fd7e14;color:white;padding:10px 20px;border-radius:5px;text-decoration:none;display:inline-block;'>Login to Review</a></p>
    <hr/>
    <p style='color:#6c757d;font-size:0.85rem;'>This is an automated reminder from RMSys SPT Academy.</p>
</div>";
                await _emailService.SendEmailAsync(mentorEmail, "⏰ Your students are waiting for log approvals", body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Inactive mentor reminder failed: {Msg}", ex.Message);
            }
        }
    }
}