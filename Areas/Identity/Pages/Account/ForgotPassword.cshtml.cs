using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SPT.Models;
using SPT.Services;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.WebUtilities;

namespace SPT.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
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
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);

            // ✅ Always show the same message whether user exists or not (security)
            if (user == null)
            {
                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            // Generate password reset token
            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            var callbackUrl = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new { area = "Identity", code },
                protocol: Request.Scheme);

            string emailBody = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;padding:20px;border:1px solid #ddd;border-radius:8px;'>
    <h2 style='color:#0d6efd;'>Reset Your Password &#128274;</h2>
    <p>Hi <strong>{user.UserName}</strong>,</p>
    <p>We received a request to reset your SPT Academy account password.</p>
    <p>Click the button below to reset it. This link expires in <strong>24 hours</strong>.</p>
    <p style='margin:24px 0;'>
        <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'
           style='background:#0d6efd;color:white;padding:12px 24px;border-radius:5px;text-decoration:none;display:inline-block;font-size:16px;'>
            Reset Password
        </a>
    </p>
    <p>If the button doesn't work, copy and paste this link into your browser:</p>
    <p style='word-break:break-all;color:#6c757d;font-size:0.85rem;'>{HtmlEncoder.Default.Encode(callbackUrl)}</p>
    <p style='color:#dc3545;'><strong>If you did not request a password reset, ignore this email. Your password will not change.</strong></p>
    <hr/>
    <p style='color:#6c757d;font-size:0.85rem;'>This is an automated message from RMSys SPT Academy. Do not reply.</p>
</div>";

            try
            {
                await _emailService.SendEmailAsync(Input.Email, "SPT Academy - Reset Your Password", emailBody);
            }
            catch
            {
                // Still redirect — don't reveal that email failed
            }

            return RedirectToPage("./ForgotPasswordConfirmation");
        }
    }
}