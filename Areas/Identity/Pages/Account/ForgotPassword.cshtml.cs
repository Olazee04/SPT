using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SPT.Models;
using SPT.Services;

namespace SPT.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;

        public ForgotPasswordModel(UserManager<ApplicationUser> userManager, IEmailService emailService)
        {
            _userManager = userManager;
            _emailService = emailService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);

            // ✅ Always show success even if email not found (security best practice)
            // This prevents attackers from knowing which emails are registered
            if (user == null)
            {
                TempData["Success"] = "If that email exists in our system, a reset link has been sent.";
                return Page();
            }

            // Generate reset token
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetLink = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new { area = "Identity", token, email = Input.Email },
                protocol: Request.Scheme);

            // Send email
            try
            {
                string body = $@"
                    <h2>Password Reset Request</h2>
                    <p>Hi {user.UserName},</p>
                    <p>You requested a password reset for your SPT account.</p>
                    <p>Click the button below to reset your password. This link expires in 24 hours.</p>
                    <p style='text-align:center;margin:30px 0;'>
                        <a href='{resetLink}' 
                           style='background:#4f46e5;color:white;padding:12px 30px;border-radius:8px;text-decoration:none;font-weight:bold;'>
                            Reset My Password
                        </a>
                    </p>
                    <p>If you did not request this, please ignore this email.</p>
                    <p>Best regards,<br/>RMSys SPT Team</p>";

                await _emailService.SendEmailAsync(Input.Email, "Reset Your SPT Password", body);
            }
            catch { /* Email failure should not expose errors */ }

            TempData["Success"] = "If that email exists in our system, a reset link has been sent.";
            return Page();
        }
    }
}