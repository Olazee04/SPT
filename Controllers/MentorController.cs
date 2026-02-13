using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;
using SPT.Models.ViewModels;
using SPT.Services;

namespace SPT.Controllers
{
    [Authorize(Roles = "Mentor")]
    public class MentorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly AuditService _auditService;

        public MentorController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env, AuditService auditService)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
            _auditService = auditService;
        }
        // =========================
        // MENTOR DASHBOARD (ADMIN VIEW — FILTERED)
        // =========================
        public async Task<IActionResult> Dashboard()
        {
            var mentor = await GetCurrentMentorAsync();
            if (mentor == null) return View("Error");

            IQueryable<Student> studentQuery;

            if (mentor.Specialization == "General")
                studentQuery = _context.Students;
            else
                studentQuery = _context.Students.Where(s => s.MentorId == mentor.Id);

            var students = await studentQuery
                .Include(s => s.Track)
                .Include(s => s.ProgressLogs)
                .Include(s => s.ModuleCompletions)
                .ToListAsync();

            var modules = await _context.SyllabusModules.ToListAsync();

            var model = new AdminDashboardViewModel();

            model.TotalStudents = students.Count;
            model.TotalMentors = 1;

            var sevenDaysAgo = DateTime.UtcNow.Date.AddDays(-6);

            model.PendingLogs = await _context.ProgressLogs
                .Where(l => !l.IsApproved &&
                            studentQuery.Select(s => s.Id).Contains(l.StudentId))
                .CountAsync();

            model.OpenTickets = await _context.SupportTickets
                .Where(t => !t.IsResolved &&
                            studentQuery.Select(s => s.Id).Contains(t.StudentId))
                .CountAsync();

            var performance = new List<StudentPerformanceDTO>();

            foreach (var s in students)
            {
                var recent = s.ProgressLogs
                    .Where(l => l.IsApproved && l.Date >= sevenDaysAgo)
                    .ToList();

                var hours = recent.Sum(l => l.Hours);
                var checkins = recent.Select(l => l.Date.Date).Distinct().Count();

                var totalMods = modules.Count(m => m.TrackId == s.TrackId);
                var completed = s.ModuleCompletions.Count(mc => mc.IsCompleted);

                performance.Add(new StudentPerformanceDTO
                {
                    StudentId = s.Id,
                    FullName = s.FullName,
                    Track = s.Track?.Code ?? "N/A",
                    ProfilePicture = s.ProfilePicture,
                    WeeklyHours = hours,
                    WeeklyCheckIns = checkins,
                    CompletedModules = completed,
                    TotalModules = totalMods
                });
            }

            model.StudentPerformance = performance;
            model.ActiveStudents = performance.Count(p => p.Status == "Active");
            model.AvgConsistency = performance.Any()
                ? (decimal)performance.Average(p => p.ConsistencyScore)
                : 0;

            // charts
            model.TrackLabels = performance.GroupBy(p => p.Track)
                .Select(g => g.Key)
                .ToArray();

            model.TrackCounts = performance.GroupBy(p => p.Track)
                .Select(g => g.Count())
                .ToArray();

            model.ActivityDates = new string[7];
            model.ActivityCounts = new int[7];

            for (int i = 0; i < 7; i++)
            {
                var d = sevenDaysAgo.AddDays(i);

                model.ActivityDates[i] = d.ToString("MMM dd");

                model.ActivityCounts[i] = await _context.ProgressLogs
                    .Where(l => l.Date.Date == d &&
                                studentQuery.Select(s => s.Id).Contains(l.StudentId))
                    .CountAsync();
            }

            return View("~/Views/Admin/Dashboard.cshtml", model);
        }

        // =========================
        // HELPER: GET CURRENT MENTOR
        // =========================
        private async Task<Mentor?> GetCurrentMentorAsync()
        {
            var userId = _userManager.GetUserId(User);
            return await _context.Mentors
     .Include(m => m.User)
     .FirstOrDefaultAsync(m => m.UserId == userId);

        }



        // =========================
        // MY STUDENTS LIST
        // =========================
       
        [HttpGet]
        public async Task<IActionResult> Students()
        {
            var mentor = await GetCurrentMentorAsync();
            if (mentor == null) return RedirectToAction(nameof(Students));

            IQueryable<Student> studentQuery;

            if (mentor.Specialization == "General")
                studentQuery = _context.Students;
            else
                studentQuery = _context.Students.Where(s => s.MentorId == mentor.Id);

            var students = await studentQuery
                .Include(s => s.Track)
                .Include(s => s.Cohort)
                .Include(s => s.ProgressLogs)
                .Include(s => s.ModuleCompletions)
                .ToListAsync();

         

            var trackModuleCounts = await _context.SyllabusModules
                .Where(m => m.IsActive)
                .GroupBy(m => m.TrackId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

   

            var modelList = new List<StudentPerformanceViewModel>();

            var today = DateTime.UtcNow.Date;
            var last7Days = today.AddDays(-7);

            foreach (var s in students)
            {
                var recentLogs = s.ProgressLogs
                    .Where(l => l.Date >= last7Days && l.IsApproved)
                    .ToList();

                decimal hours7Days = recentLogs.Sum(l => l.Hours);
                int checkIns7Days = recentLogs.Select(l => l.Date.Date).Distinct().Count();

                int totalMods = trackModuleCounts.ContainsKey(s.TrackId)
                    ? trackModuleCounts[s.TrackId]
                    : 1;

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
                    MentorName = mentor.FullName,
                    TargetHoursPerWeek = s.TargetHoursPerWeek,
                    HoursLast7Days = hours7Days,
                    CheckInsLast7Days = checkIns7Days,
                    CompletedModules = completedMods,
                    TotalModules = totalMods,
                    ConsistencyScore = consistency,
                    Status = s.EnrollmentStatus
                });
            }

            // =========================
            // DASHBOARD METRICS
            // =========================

            var pendingLogs = await _context.ProgressLogs
                .Include(l => l.Student)
                .Include(l => l.Module)
                .Where(l => !l.IsApproved &&
                            studentQuery.Select(s => s.Id).Contains(l.StudentId))
                .OrderByDescending(l => l.Date)
                .Take(10)
                .ToListAsync();

            int pendingCount = pendingLogs.Count;

            int avgConsistency = modelList.Count == 0
                ? 0
                : (int)modelList.Average(x => x.ConsistencyScore);

            // =========================
            // VIEWBAGS
            // =========================

            ViewBag.MyStudentsCount = students.Count;
            ViewBag.PendingLogs = pendingCount;
            ViewBag.AvgConsistency = avgConsistency;
            ViewBag.PendingLogsList = pendingLogs;

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
            await _auditService.LogAsync(
    "LOG_APPROVED",
    $"Approved log #{log.Id} for {log.Student.FullName}",
    User.Identity.Name,
    _userManager.GetUserId(User));

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


        public async Task<IActionResult> Messages()
        {
            var mentor = await GetCurrentMentorAsync();

            var students = mentor.Specialization == "General"
                ? await _context.Students.ToListAsync()
                : await _context.Students.Where(s => s.MentorId == mentor.Id).ToListAsync();

            return View(students);
        }

        // =========================
        // POST: UPDATE PROFILE & PASSWORD
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(IFormFile? profilePicture, string? currentPassword, string? newPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            var mentor = await _context.Mentors
    .Include(m => m.User)
    .Include(m => m.Track)
    .FirstOrDefaultAsync(m => m.UserId == user.Id);

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
                await _auditService.LogAsync(
    "MENTOR_PROFILE_UPDATED",
    $"Mentor updated profile",
    User.Identity.Name,
    _userManager.GetUserId(User));

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
            await _auditService.LogAsync(
  "PASSWORD_CHANGED",
  "User changed password",
  User.Identity.Name,
  _userManager.GetUserId(User));

            return RedirectToAction(nameof(Profile));
          
        }
    }
}