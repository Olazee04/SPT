using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;

namespace SPT.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        private readonly IWebHostEnvironment _env;
        public StudentController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;

        }

        // =========================
        // STUDENT DASHBOARD
        // =========================

        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var student = await _context.Students
                .Include(s => s.Track)
                .Include(s => s.Cohort)
                .Include(s => s.Mentor) // ✅ Load Mentor
                .Include(s => s.ProgressLogs) // ✅ Load Logs for stats
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (student == null) return View("ProfilePending");

            return View(student);
        }

        // =========================
        // GET: View Profile Page
        // =========================
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            var student = await _context.Students
                .Include(s => s.Track)
                .Include(s => s.Cohort)
                .Include(s => s.Mentor) // Include Mentor info
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (student == null) return RedirectToAction("Dashboard");

            return View(student);
        }

        // =========================
        // POST: Update Profile (Picture & Password Only)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(IFormFile? profilePicture, string? currentPassword, string? newPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (student == null) return NotFound();

            // 1. Update Picture
            if (profilePicture != null && profilePicture.Length > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "profiles");
                Directory.CreateDirectory(uploadsFolder);
                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(profilePicture.FileName)}";
                using (var stream = new FileStream(Path.Combine(uploadsFolder, fileName), FileMode.Create))
                {
                    await profilePicture.CopyToAsync(stream);
                }
                student.ProfilePicture = $"/uploads/profiles/{fileName}";

                _context.Update(student);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Profile picture updated successfully!";
            }

            // 2. Change Password
            if (!string.IsNullOrEmpty(newPassword))
            {
                if (string.IsNullOrEmpty(currentPassword))
                {
                    TempData["Error"] = "You must enter your current password to set a new one.";
                    return RedirectToAction(nameof(Profile));
                }

                var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
                if (!result.Succeeded)
                {
                    TempData["Error"] = "Password error: " + string.Join(", ", result.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(Profile));
                }
                TempData["Success"] = "Password changed successfully!";
            }

            return RedirectToAction(nameof(Profile));
        }

       

    }
}
