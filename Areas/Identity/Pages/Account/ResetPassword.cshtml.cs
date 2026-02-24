using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SPT.Models;

namespace SPT.Areas.Identity.Pages.Account
{
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ResetPasswordModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [MinLength(4)]
            public string Password { get; set; }

            [Required]
            [Compare("Password", ErrorMessage = "Passwords do not match.")]
            public string ConfirmPassword { get; set; }

            public string Token { get; set; }
        }

        public IActionResult OnGet(string token, string email)
        {
            if (token == null || email == null) return RedirectToPage("./Login");
            Input = new InputModel { Token = token, Email = email };
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null) return RedirectToPage("./Login");

            var result = await _userManager.ResetPasswordAsync(user, Input.Token, Input.Password);

            if (result.Succeeded)
            {
                TempData["Success"] = "? Password reset successfully! You can now log in.";
                return RedirectToPage("./Login");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return Page();
        }
    }
}