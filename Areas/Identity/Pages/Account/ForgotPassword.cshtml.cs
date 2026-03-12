using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SPT.Models;
using SPT.Services;
using System.ComponentModel.DataAnnotations;

namespace SPT.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _config;

        public ForgotPasswordModel(
            UserManager<ApplicationUser> userManager,
            IEmailService emailService,
            IConfiguration config)
        {
            _userManager = userManager;
            _emailService = emailService;
            _config = config;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required]
            public string FullName { get; set; } = string.Empty;

            [Required]
            public string Username { get; set; } = string.Empty;

            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            public string Role { get; set; } = "Student";

            // Student fills this
            public string? Course { get; set; }

            // Mentor fills this
            public string? MentorCourse { get; set; }
        }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            string role = Input.Role ?? "Student";
            string courseInfo = role == "Mentor"
                ? $"Course They Teach: {Input.MentorCourse ?? "Not specified"}"
                : $"Enrolled Course/Track: {Input.Course ?? "Not specified"}";

            string adminEmail = _config["Email:User"] ?? "admin@spt.com";

            // ── Email to ADMIN notifying of reset request ──
            string adminBody = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;padding:20px;border:1px solid #ddd;border-radius:8px;'>
    <h2 style='color:#dc3545;'>&#128274; Password Reset Request</h2>
    <p>A <strong>{role}</strong> has requested a password reset. Please verify their identity and reset their password from the Admin panel.</p>
    <table style='border-collapse:collapse;width:100%;margin:16px 0;'>
        <tr style='background:#f8f9fa;'>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>Role</td>
            <td style='padding:10px;border:1px solid #dee2e6;'>{role}</td>
        </tr>
        <tr>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>Full Name</td>
            <td style='padding:10px;border:1px solid #dee2e6;'>{Input.FullName}</td>
        </tr>
        <tr style='background:#f8f9fa;'>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>Username</td>
            <td style='padding:10px;border:1px solid #dee2e6;'>{Input.Username}</td>
        </tr>
        <tr>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>Email</td>
            <td style='padding:10px;border:1px solid #dee2e6;'>{Input.Email}</td>
        </tr>
        <tr style='background:#f8f9fa;'>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>{(role == "Mentor" ? "Course Taught" : "Enrolled Track")}</td>
            <td style='padding:10px;border:1px solid #dee2e6;'>{(role == "Mentor" ? Input.MentorCourse : Input.Course) ?? "Not specified"}</td>
        </tr>
    </table>
    <p>
        <a href='https://rmsysspt.onrender.com/Admin/Students'
           style='background:#0d6efd;color:white;padding:10px 20px;border-radius:5px;text-decoration:none;display:inline-block;'>
            Go to Admin Panel
        </a>
    </p>
    <p style='color:#6c757d;font-size:0.85rem;'>Please reset their password and the system will automatically email it to them.</p>
    <hr/>
    <p style='color:#6c757d;font-size:0.85rem;'>This is an automated request from RMSys SPT Academy.</p>
</div>";

            // ── Confirmation email to the USER ──
            string userBody = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;padding:20px;border:1px solid #ddd;border-radius:8px;'>
    <h2 style='color:#0d6efd;'>Password Reset Request Received &#9989;</h2>
    <p>Hi <strong>{Input.FullName}</strong>,</p>
    <p>Your password reset request has been received and sent to the admin.</p>
    <p>The admin will verify your details and reset your password. You will receive your new password by email shortly.</p>
    <p style='color:#dc3545;'><strong>If you did not make this request, please contact your admin immediately.</strong></p>
    <hr/>
    <p style='color:#6c757d;font-size:0.85rem;'>This is an automated message from RMSys SPT Academy.</p>
</div>";

            bool emailSent = false;
            try
            {
                await _emailService.SendEmailAsync(adminEmail, $"[SPT] Password Reset Request — {role}: {Input.FullName}", adminBody);
                await _emailService.SendEmailAsync(Input.Email, "SPT Academy - Password Reset Request Received", userBody);
                emailSent = true;
            }
            catch { }

            if (emailSent)
                TempData["Success"] = "✅ Your request has been sent to the admin. Check your email for confirmation. Your new password will be emailed to you once reset.";
            else
                TempData["Error"] = "⚠️ Request could not be sent. Please contact your admin directly at rmsyssolutions@gmail.com";

            return Page();
        }
    }
}