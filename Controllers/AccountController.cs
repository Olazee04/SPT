using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SPT.Models;

namespace SPT.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser>
    _signInManager;
        private readonly UserManager<ApplicationUser>
            _userManager;

        public AccountController(
        SignInManager<ApplicationUser>
            signInManager,
            UserManager<ApplicationUser>
                userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        // =========================
        // GET: /Account/Login
        // =========================
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // =========================
        // POST: /Account/Login
        // =========================
        [HttpPost]
        public async Task<IActionResult>
            Login(
            string username,
            string password,
            bool rememberMe)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("", "Username and password are required");
                return View();
            }

            var result = await _signInManager.PasswordSignInAsync(
            username,
            password,
            rememberMe,
            lockoutOnFailure: false
            );

            // ✅ LOGIN SUCCESS
            // AccountController.cs -> Login (POST)

            if (result.Succeeded)
            {
                var user = await _userManager.FindByNameAsync(username);

                // SAFETY CHECK: Ensure user is not null
                if (user == null)
                {
                    return RedirectToAction("Login");
                }
                if (await _userManager.IsInRoleAsync(user, "Admin"))
                    return RedirectToAction("Dashboard", "Admin");

                if (await _userManager.IsInRoleAsync(user, "Student"))
                    return RedirectToAction("Dashboard", "Student");

                if (await _userManager.IsInRoleAsync(user, "Mentor"))
                    return RedirectToAction("Dashboard", "Mentor");

                return RedirectToAction("Index", "Home");
            }

            // ❌ LOGIN FAILED
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
