using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;
using SPT.Models.ViewModels;

namespace SPT.Controllers
{
    [Authorize(Roles = "Mentor")]
    public class MentorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public MentorController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        // =========================
        // HELPER: GET CURRENT MENTOR
        // =========================
        private async Task<Mentor?> GetCurrentMentorAsync()
        {
            var userId = _userManager.GetUserId(User);
            return await _context.Mentors.FirstOrDefaultAsync(m => m.UserId == userId);
        }

        // =========================
        // MENTOR DASHBOARD
        // =========================
        public async Task<IActionResult> Dashboard()
        {
            var mentor = await GetCurrentMentorAsync();
            if (mentor == null) return View("Error"); // Handle missing profile gracefully

            // 1. Scoped Stats (Only MY students)
            var myStudentsQuery = _context.Students.Where(s => s.MentorId == mentor.Id && s.EnrollmentStatus == "Active");

            ViewBag.MyStudentsCount = await myStudentsQuery.CountAsync();

            ViewBag.PendingLogs = await _context.ProgressLogs
                .Where(l => l.Student.MentorId == mentor.Id && !l.IsApproved)
                .CountAsync();

            ViewBag.AvgConsistency = 85; // Placeholder or calculate properly if needed

            // 2. Fetch Recent Logs for "Inbox"
            var recentLogs = await _context.ProgressLogs
                .Include(l => l.Student)
                .Include(l => l.Module)
                .Where(l => l.Student.MentorId == mentor.Id && !l.IsApproved)
                .OrderByDescending(l => l.Date)
                .Take(5)
                .ToListAsync();

            return View(recentLogs);
        }

        // =========================
        // MY STUDENTS LIST
        // =========================
        [HttpGet]
        public async Task<IActionResult> Students()
        {
            var mentor = await GetCurrentMentorAsync();
            if (mentor == null) return RedirectToAction(nameof(Dashboard));

            // 1. Fetch Data
            var students = await _context.Students
                .Include(s => s.Track)
                .Include(s => s.Cohort)
                .Include(s => s.ProgressLogs)
                .Include(s => s.ModuleCompletions)
                .Where(s => s.MentorId == mentor.Id) // 🔒 Filter by Mentor
                .ToListAsync();

            // 2. Transform for ViewModel
            var modelList = new List<StudentPerformanceViewModel>();
            var today = DateTime.UtcNow.Date;
            var last7Days = today.AddDays(-7);

            var trackModuleCounts = await _context.SyllabusModules
                .Where(m => m.IsActive)
                .GroupBy(m => m.TrackId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            foreach (var s in students)
            {
                var recentLogs = s.ProgressLogs.Where(l => l.Date >= last7Days && l.IsApproved).ToList();
                decimal hours7Days = recentLogs.Sum(l => l.Hours);
                int checkIns7Days = recentLogs.Select(l => l.Date.Date).Distinct().Count();
                int totalMods = trackModuleCounts.ContainsKey(s.TrackId) ? trackModuleCounts[s.TrackId] : 1;
                int completedMods = s.ModuleCompletions.Count(mc => mc.IsCompleted);

                int consistency = 0;
                if (s.TargetHoursPerWeek > 0)
                {
                    consistency = (int)((hours7Days / s.TargetHoursPerWeek) * 100);
                    if (consistency > 100) consistency = 100;
                }

                modelList.Add(new StudentPerformanceViewModel
                {
                    StudentId = s.Id,
                    FullName = s.FullName,
                    Email = s.Email,
                    ProfilePicture = s.ProfilePicture,
                    CohortName = s.Cohort?.Name ?? "N/A",
                    TrackCode = s.Track?.Code ?? "N/A",
                    MentorName = "Me",
                    TargetHoursPerWeek = s.TargetHoursPerWeek,
                    HoursLast7Days = hours7Days,
                    CheckInsLast7Days = checkIns7Days,
                    CompletedModules = completedMods,
                    TotalModules = totalMods,
                    ConsistencyScore = consistency,
                    Status = s.EnrollmentStatus
                });
            }

            return View(modelList);
        }

        // =========================
        // APPROVE LOG (Scoped)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveLog(int id, string? action)
        {
            var mentor = await GetCurrentMentorAsync();
            var log = await _context.ProgressLogs.Include(l => l.Student).FirstOrDefaultAsync(l => l.Id == id);

            if (log == null) return NotFound();

            // 🔒 SECURITY CHECK: Ensure this student belongs to this mentor
            if (log.Student.MentorId != mentor?.Id)
            {
                return Forbid();
            }

            if (action == "Reject")
            {
                _context.ProgressLogs.Remove(log);
                TempData["Error"] = "Log Rejected.";
            }
            else
            {
                log.IsApproved = true;
                log.UpdatedAt = DateTime.UtcNow;
                TempData["Success"] = "Log Approved.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Dashboard));
        }

        // =========================
        // GET: PROFILE
        // =========================
        public async Task<IActionResult> Profile()
        {
            var mentor = await GetCurrentMentorAsync();
            if (mentor == null) return RedirectToAction("Dashboard");
            return View(mentor);
        }

        // =========================
        // POST: UPDATE PROFILE & PASSWORD
        // =========================
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