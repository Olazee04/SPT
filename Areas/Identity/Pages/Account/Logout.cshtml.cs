using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SPT.Models;
using SPT.Services;

namespace SPT.Areas.Identity.Pages.Account
{
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuditService _audit;

        public LogoutModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            AuditService audit)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _audit = audit;
        }

        public async Task<IActionResult> OnPost(string returnUrl = null)
        {
            var user = await _userManager.GetUserAsync(User);

            await _signInManager.SignOutAsync();

            await _audit.LogAsync(
                "LOGOUT",
                "User logged out",
                User.Identity?.Name,
                user?.Id);
          

            if (returnUrl != null)
                return LocalRedirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }
    }
}
