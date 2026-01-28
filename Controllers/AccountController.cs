using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SPT.Models;
using SPT.Services;

namespace SPT.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser>
    _signInManager;
        private readonly UserManager<ApplicationUser>
            _userManager;
        private readonly AuditService _auditService;
        public AccountController(
        SignInManager<ApplicationUser>
            signInManager,
            UserManager<ApplicationUser>
                userManager, AuditService auditService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _auditService = auditService;
        }

        // =========================
        // GET: /Account/Login
        // =========================
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken] // Ensure this is here for security
        public async Task<IActionResult> Login(string username, string password, bool rememberMe)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("", "Username and password are required");
                return View();
            }

            var result = await _signInManager.PasswordSignInAsync(username, password, rememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                var user = await _userManager.FindByNameAsync(username);

                // SAFETY CHECK
                if (user == null) return RedirectToAction("Login");

                // 1. Check Admin
                if (await _userManager.IsInRoleAsync(user, "Admin"))
                    return RedirectToAction("Dashboard", "Admin");

                // 2. Check Student
                if (await _userManager.IsInRoleAsync(user, "Student"))
                    return RedirectToAction("Dashboard", "Student");

                // 3. Check Mentor (THIS IS THE PART YOU ASKED ABOUT)
                if (await _userManager.IsInRoleAsync(user, "Mentor"))
                {
                    // Redirect Mentor to the Admin Dashboard (Shared view)
                    return RedirectToAction("Dashboard", "Admin");
                }

                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError("", "Invalid username or password");
            return View();
        }

        // =========================
        // POST: /Account/Logout
        // =========================
        [HttpPost]
        public async Task<IActionResult>
            Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }
    }
}
