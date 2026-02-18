using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SPT.Services;
using SPT.Models;

namespace SPT.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly AuditService _audit;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<LoginModel> logger,
            AuditService audit)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _audit = audit;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            public bool RememberMe { get; set; }

            public string LoginType { get; set; } // Student / AdminMentor
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (!ModelState.IsValid)
                return Page();

            // Try find by email first
            var user = await _userManager.FindByEmailAsync(Input.Email);

            // If not found → try username
            if (user == null)
                user = await _userManager.FindByNameAsync(Input.Email);

            if (user == null)
            {
                await _audit.LogAsync("LOGIN_FAILED", "User not found", Input.Email);
                ModelState.AddModelError("", "Invalid login attempt.");
                return Page();
            }

            var result = await _signInManager.PasswordSignInAsync(
                user.UserName,
                Input.Password,
                Input.RememberMe,
                false);

            if (!result.Succeeded)
            {
                await _audit.LogAsync("LOGIN_FAILED", "Wrong password", user.UserName, user.Id);
                ModelState.AddModelError("", "Invalid login attempt.");
                return Page();
            }

            await _audit.LogAsync("LOGIN_SUCCESS", "User logged in", user.UserName, user.Id);

            if (await _userManager.IsInRoleAsync(user, "Admin"))
                return RedirectToAction("Dashboard", "Admin");

            if (await _userManager.IsInRoleAsync(user, "Mentor"))
                return RedirectToAction("Dashboard", "Mentor");

            if (await _userManager.IsInRoleAsync(user, "Student"))
                return RedirectToAction("Dashboard", "Student");

            return RedirectToAction("Index", "Home");
        }
       
    }

}

