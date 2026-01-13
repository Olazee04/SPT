using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

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

            // 1. Fetch Student Data
            var student = await _context.Students
                .Include(s => s.Track)
                .Include(s => s.Cohort)
                .Include(s => s.Mentor)
                .Include(s => s.ProgressLogs)
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (student == null) return View("ProfilePending");

            // ---------------------------------------------------------
            // 📊 STATS CALCULATION ENGINE
            // ---------------------------------------------------------
            // We verify specific stats here so they appear on the Dashboard
            var logs = student.ProgressLogs.Where(l => l.IsApproved).ToList(); // Only verified logs count
            var today = DateTime.UtcNow.Date;

            // A. Calculate Streak (Consecutive days backwards from today/yesterday)
            int currentStreak = 0;
            var logDates = logs.Select(l => l.Date.Date).Distinct().OrderByDescending(d => d).ToList();

            if (logDates.Any())
            {
                var lastLogDate = logDates.First();
                // Check if streak is alive (logged Today OR Yesterday)
                if (lastLogDate == today || lastLogDate == today.AddDays(-1))
                {
                    currentStreak = 1;
                    for (int i = 0; i < logDates.Count - 1; i++)
                    {
                        // Check if the next date in the list is exactly 1 day before the current one
                        if (logDates[i].AddDays(-1) == logDates[i + 1])
                            currentStreak++;
                        else
                            break;
                    }
                }
            }

            // B. Calculate Consistency Score (Last 28 Days Performance)
            var last28Days = today.AddDays(-28);
            decimal recentHours = logs.Where(l => l.Date >= last28Days).Sum(l => l.Hours);
            decimal targetHours4Weeks = (decimal)(student.TargetHoursPerWeek * 4);

            int consistencyScore = 0;
            if (targetHours4Weeks > 0)
            {
                consistencyScore = (int)((recentHours / targetHours4Weeks) * 100);
                if (consistencyScore > 100) consistencyScore = 100;
            }

            // C. Calculate Rank (Compare against all active students)
            decimal myTotalHours = logs.Sum(l => l.Hours);

            // Count how many students have MORE hours than me
            var betterStudentsCount = await _context.Students
                .Where(s => s.EnrollmentStatus == "Active" && s.Id != student.Id)
                .Select(s => new {
                    Id = s.Id,
                    // Sum only approved hours for other students too
                    TotalHours = s.ProgressLogs.Where(p => p.IsApproved).Sum(p => (decimal?)p.Hours) ?? 0
                })
                .CountAsync(x => x.TotalHours > myTotalHours);

            int rank = betterStudentsCount + 1; // If 0 people are better, I am #1

            // ---------------------------------------------------------
            // PASS DATA TO VIEW
            // ---------------------------------------------------------
            ViewBag.Streak = currentStreak;
            ViewBag.Consistency = consistencyScore;
            ViewBag.Rank = rank;

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
                .Include(s => s.Mentor)
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
        // =========================
        // GET: My Curriculum / Roadmap
        // =========================
        [HttpGet]
        public async Task<IActionResult> Curriculum()
        {
            var user = await _userManager.GetUserAsync(User);
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);
            if (student == null) return RedirectToAction("Dashboard");

            // 1. Get all modules for this track
            var modules = await _context.SyllabusModules
                .Where(m => m.TrackId == student.TrackId && m.IsActive)
                .OrderBy(m => m.DisplayOrder)
                .ToListAsync();

            // 2. Get completed modules (Where student passed the quiz)
            var completions = await _context.ModuleCompletions
                .Where(mc => mc.StudentId == student.Id && mc.IsCompleted)
                .ToListAsync();

            // 3. Build the View Model with Locking Logic
            var model = new List<CurriculumViewModel>();
            bool previousModuleCompleted = true; // First module is always unlocked

            foreach (var module in modules)
            {
                var completion = completions.FirstOrDefault(c => c.ModuleId == module.Id);
                bool isCompleted = completion != null;

                var item = new CurriculumViewModel
                {
                    ModuleId = module.Id,
                    ModuleCode = module.ModuleCode,
                    Title = module.ModuleName,
                    Description = module.Topics,
                    RequiredHours = module.RequiredHours,
                    Difficulty = module.DifficultyLevel,
                    IsCompleted = isCompleted,
                    // Logic: Locked if previous wasn't finished AND this one isn't finished
                    IsLocked = !previousModuleCompleted && !isCompleted
                };

                model.Add(item);

                // This module determines if the *next* one is unlocked
                previousModuleCompleted = isCompleted;
            }

            return View(model);
        }

        // =========================
        // GET: Attendance Page
        // =========================
        [HttpGet]
        public async Task<IActionResult> Attendance()
        {
            var user = await _userManager.GetUserAsync(User);
            var student = await _context.Students
                .Include(s => s.ProgressLogs)
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (student == null) return RedirectToAction("Dashboard");

            // Filter APPROVED logs only
            var logs = student.ProgressLogs
                .Where(l => l.IsApproved)
                .OrderByDescending(l => l.Date)
                .ToList();

            // 1. Calculate Weekly Attendance (Resets on Monday)
            var today = DateTime.UtcNow.Date;
            // Calculate days since Monday (0 = Monday, 6 = Sunday)
            // Note: DayOfWeek.Monday is 1 in C#. We need to handle Sunday (0) correctly.
            int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            var thisMonday = today.AddDays(-1 * diff);

            var daysLoggedThisWeek = logs
                .Where(l => l.Date.Date >= thisMonday)
                .Select(l => l.Date.Date)
                .Distinct()
                .Count();

            // 2. Location Stats
            var officeDays = logs.Count(l => l.Location == "Office");
            var remoteDays = logs.Count(l => l.Location == "Remote");

            // 3. Monthly Graph Data (Last 6 Months)
            var monthLabels = new List<string>();
            var monthCounts = new List<int>();

            for (int i = 5; i >= 0; i--)
            {
                var monthStart = new DateTime(today.Year, today.Month, 1).AddMonths(-i);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                var count = logs
                    .Where(l => l.Date >= monthStart && l.Date <= monthEnd)
                    .Select(l => l.Date.Date)
                    .Distinct()
                    .Count();

                monthLabels.Add(monthStart.ToString("MMM"));
                monthCounts.Add(count);
            }

            var model = new AttendanceViewModel
            {
                DaysLoggedThisWeek = daysLoggedThisWeek,
                TotalOfficeDays = officeDays,
                TotalRemoteDays = remoteDays,
                MonthLabels = monthLabels,
                MonthlyAttendanceCounts = monthCounts,
                RecentLogs = logs.Take(10).ToList()
            };

            return View(model);
        }

        // =========================
        // GET: Take Quiz Page
        // =========================
        [HttpGet]
        public async Task<IActionResult> TakeQuiz(int moduleId)
        {
            var user = await _userManager.GetUserAsync(User);
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);

            // 1. Security Check: Is this module actually unlocked?
            // (You can reuse the logic from Curriculum() here for strict security)

            var module = await _context.SyllabusModules
                .Include(m => m.Track)
                .FirstOrDefaultAsync(m => m.Id == moduleId);

            if (module == null) return NotFound();

            // 2. Fetch Questions (Randomize order if you want!)
            var questions = await _context.QuizQuestions
                .Include(q => q.Options)
                .Where(q => q.ModuleId == moduleId)
                .ToListAsync();

            if (!questions.Any())
            {
                TempData["Error"] = "No quiz questions found for this module yet.";
                return RedirectToAction("Curriculum");
            }

            ViewBag.Module = module;
            return View(questions);
        }

        // =========================
        // POST: Submit Quiz
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitQuiz(int moduleId, Dictionary<int, int> answers)
        {
            var user = await _userManager.GetUserAsync(User);
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);

            // 1. Calculate Score
            int correctCount = 0;
            int totalQuestions = answers.Count;

            foreach (var answer in answers)
            {
                int questionId = answer.Key;
                int selectedOptionId = answer.Value;

                var isCorrect = await _context.QuizOptions
                    .AnyAsync(o => o.Id == selectedOptionId && o.QuestionId == questionId && o.IsCorrect);

                if (isCorrect) correctCount++;
            }

            double percentage = totalQuestions == 0 ? 0 : ((double)correctCount / totalQuestions) * 100;
            bool passed = percentage >= 75; // Pass mark is 75%

            // 2. Save Result
            if (passed)
            {
                // Check if already completed to avoid duplicates
                var existing = await _context.ModuleCompletions
                    .FirstOrDefaultAsync(mc => mc.StudentId == student.Id && mc.ModuleId == moduleId);

                if (existing == null)
                {
                    var completion = new ModuleCompletion
                    {
                        StudentId = student.Id,
                        ModuleId = moduleId,
                        IsCompleted = true,
                        QuizCompleted = true,
                        CompletionDate = DateTime.UtcNow,
                        VerifiedBy = "System (Quiz Passed)"
                    };
                    _context.ModuleCompletions.Add(completion);
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = $"🎉 Passed! You scored {percentage}%. The next module is unlocked.";
            }
            else
            {
                TempData["Error"] = $"❌ Failed. You scored {percentage}%. You need 75% to pass. Try again!";
            }

            return RedirectToAction("Curriculum");
        }
    }
}