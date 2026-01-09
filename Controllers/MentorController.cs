using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;

namespace SPT.Controllers
{
    [Authorize(Roles = "Mentor")]
    public class MentorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public MentorController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            var mentor = await _context.Mentors.FirstOrDefaultAsync(m => m.UserId == user.Id);
            return View(mentor);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(IFormFile? profilePicture, string? currentPassword, string? newPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            var mentor = await _context.Mentors.FirstOrDefaultAsync(m => m.UserId == user.Id);

            if (mentor == null) return NotFound();

            // 1. Picture Logic
            if (profilePicture != null && profilePicture.Length > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "profiles");
                Directory.CreateDirectory(uploadsFolder);
                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(profilePicture.FileName)}";
                using (var stream = new FileStream(Path.Combine(uploadsFolder, fileName), FileMode.Create))
                {
                    await profilePicture.CopyToAsync(stream);
                }
                mentor.ProfilePicture = $"/uploads/profiles/{fileName}";
                _context.Update(mentor);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Profile picture updated!";
            }

            // 2. Password Logic
            if (!string.IsNullOrEmpty(newPassword))
            {
                if (string.IsNullOrEmpty(currentPassword))
                {
                    TempData["Error"] = "Current password is required to set a new one.";
                    return RedirectToAction(nameof(Profile));
                }
                var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
                if (!result.Succeeded)
                {
                    TempData["Error"] = "Error: " + string.Join(", ", result.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(Profile));
                }
                TempData["Success"] = "Password changed successfully!";
            }

            return RedirectToAction(nameof(Profile));
        }
    }
}