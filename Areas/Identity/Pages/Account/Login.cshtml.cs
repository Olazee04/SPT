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
            [Required(ErrorMessage = "Please enter your username or email.")]
            [Display(Name = "Username or Email")]
            public string Email { get; set; }  // kept as "Email" so existing view binding still works

            [Required(ErrorMessage = "Please enter your password.")]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            public bool RememberMe { get; set; }

            // "Student" or "AdminMentor" — sent from the login tab buttons
            public string LoginType { get; set; }
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

            // ── Step 1: Find user by email OR username ──────────────────
            var user = await _userManager.FindByEmailAsync(Input.Email)
                    ?? await _userManager.FindByNameAsync(Input.Email);

            if (user == null)
            {
                await _audit.LogAsync("LOGIN_FAILED", "User not found", Input.Email);
                ModelState.AddModelError("", "Invalid login attempt.");
                return Page();
            }

            // ── Step 2: Enforce tab/role restriction ────────────────────
            bool isStudent = await _userManager.IsInRoleAsync(user, "Student");
            bool isAdminOrMentor = await _userManager.IsInRoleAsync(user, "Admin")
                                || await _userManager.IsInRoleAsync(user, "Mentor");

            if (Input.LoginType == "Student" && !isStudent)
            {
                // Admin/Mentor tried to use the Student tab
                ModelState.AddModelError("", "This login is for students only. Please use the Admin / Mentor login.");
                return Page();
            }

            if (Input.LoginType == "AdminMentor" && !isAdminOrMentor)
            {
                // Student tried to use the Admin/Mentor tab
                ModelState.AddModelError("", "This login is for staff only. Please use the Student login.");
                return Page();
            }

            // ── Step 3: Attempt sign in ─────────────────────────────────
            var result = await _signInManager.PasswordSignInAsync(
                user.UserName,
                Input.Password,
                Input.RememberMe,
                lockoutOnFailure: true);

            if (result.IsLockedOut)
            {
                ModelState.AddModelError("", "Account locked due to multiple failed attempts. Try again in 5 minutes.");
                return Page();
            }

            if (!result.Succeeded)
            {
                await _audit.LogAsync("LOGIN_FAILED", "Wrong password", user.UserName, user.Id);
                ModelState.AddModelError("", "Invalid login attempt.");
                return Page();
            }

            await _audit.LogAsync("LOGIN_SUCCESS", "User logged in", user.UserName, user.Id);

            // ── Step 4: Redirect by role ────────────────────────────────
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