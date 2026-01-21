using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;
using SPT.Models.ViewModels;
using System;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

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
        // GET: STUDENT DASHBOARD (Fully Merged)
        // =========================
        public async Task<IActionResult> Dashboard()
        {
            

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            // 1. Fetch Student with ALL necessary data (Merged Includes)
            var student = await _context.Students
                .Include(s => s.Track)
                .Include(s => s.Cohort)
                .Include(s => s.Mentor)
                .Include(s => s.ModuleCompletions)
                .Include(s => s.ProgressLogs)
                    .ThenInclude(pl => pl.Module) // 👈 Critical for "Recent Activity" table names
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (student == null) return RedirectToAction("CreateProfile");

            // 2. Fetch Active Modules for Dropdown
            var modules = await _context.SyllabusModules
                .Where(m => m.TrackId == student.TrackId && m.IsActive)
                .OrderBy(m => m.DisplayOrder)
                .ToListAsync();

            ViewBag.CurrentModules = modules;

            // 3. Calculate Personal Stats
            var logs = student.ProgressLogs ?? new List<ProgressLog>();
            var approvedLogs = logs.Where(l => l.IsApproved).ToList();

            // --- A. Consistency Score ---
            var last28Days = Enumerable.Range(0, 28).Select(i => DateTime.UtcNow.Date.AddDays(-i)).ToList();
            int daysLogged = approvedLogs.Select(l => l.Date.Date).Distinct().Count(d => last28Days.Contains(d));
            int consistency = (int)((daysLogged / 28.0) * 100);
            ViewBag.Consistency = consistency;

            // --- B. Streak Calculation ---
            int streak = 0;
            var dates = approvedLogs.Select(l => l.Date.Date).Distinct().OrderByDescending(d => d).ToList();
            var checkDate = DateTime.UtcNow.Date;
            if (dates.Contains(checkDate) || dates.Contains(checkDate.AddDays(-1)))
            {
                foreach (var d in dates)
                {
                    if (d == checkDate) { streak++; checkDate = checkDate.AddDays(-1); }
                    else if (d == checkDate.AddDays(-1)) { streak++; checkDate = checkDate.AddDays(-2); }
                    else break;
                }
            }
            ViewBag.Streak = streak;

            // --- C. Weekly Hours ---
            var startOfWeek = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek); // Sunday as start
            ViewBag.WeeklyHours = approvedLogs.Where(l => l.Date >= startOfWeek).Sum(l => l.Hours);

            // --- D. Global Rank (Restored from your old code) ---
            decimal myTotalHours = approvedLogs.Sum(l => l.Hours);
            var betterStudentsCount = await _context.Students
                .Where(s => s.EnrollmentStatus == "Active" && s.Id != student.Id)
                .Select(s => new
                {
                    Id = s.Id,
                    TotalHours = s.ProgressLogs.Where(p => p.IsApproved).Sum(p => (decimal?)p.Hours) ?? 0
                })
                .CountAsync(x => x.TotalHours > myTotalHours);

            ViewBag.Rank = betterStudentsCount + 1;

            // ---------------------------------------------------------
            // 🔒 MODULE LOCKING LOGIC (Fixed Property Names)
            // ---------------------------------------------------------

            // 1. Fetch All Active Modules with Original Names
            var allModules = await _context.SyllabusModules
                .Where(m => m.TrackId == student.TrackId && m.IsActive)
                .OrderBy(m => m.DisplayOrder)
                .Select(m => new {
                    Id = m.Id,
                    ModuleCode = m.ModuleCode, // ✅ Keep as ModuleCode
                    ModuleName = m.ModuleName, // ✅ Keep as ModuleName
                    DisplayOrder = m.DisplayOrder
                })
                .ToListAsync();

            var completedModuleIds = student.ModuleCompletions
                .Where(c => c.IsCompleted)
                .Select(c => c.ModuleId)
                .ToList();

            // 2. Find last completed order
            int lastCompletedOrder = 0;
            if (completedModuleIds.Any())
            {
                var lastCompleted = allModules
                    .Where(m => completedModuleIds.Contains(m.Id))
                    .OrderByDescending(m => m.DisplayOrder)
                    .FirstOrDefault();

                if (lastCompleted != null) lastCompletedOrder = lastCompleted.DisplayOrder;
            }

            // 3. Filter Unlocked Modules
            var unlockedModules = allModules
                .Where(m => m.DisplayOrder <= lastCompletedOrder + 1)
                .ToList();

            ViewBag.CurrentModules = unlockedModules;

            // ---------------------------------------------------------
            // 🚀 NEW FEATURES (Announcement, Next Up, Leaderboard)
            // ---------------------------------------------------------

            // 1. Announcement
            try
            {
                ViewBag.LatestAnnouncement = await _context.Announcements
                    .OrderByDescending(a => a.CreatedAt)
                    .FirstOrDefaultAsync();
            }
            catch { ViewBag.LatestAnnouncement = null; }

            // 2. Next Up Module
            var nextModule = allModules.FirstOrDefault(m => !completedModuleIds.Contains(m.Id));
            ViewBag.NextModule = nextModule;

            // 3. Mini Leaderboard
            if (student.CohortId != null)
            {
                var topStudents = await _context.Students
                    .Where(s => s.CohortId == student.CohortId)
                    .Select(s => new {
                        Name = s.FullName,
                        TotalHours = s.ProgressLogs.Where(l => l.IsApproved).Sum(l => l.Hours),
                        ProfilePic = s.ProfilePicture
                    })
                    .OrderByDescending(x => x.TotalHours)
                    .Take(3)
                    .ToListAsync();

                ViewBag.Leaderboard = topStudents;
            }

            return View(student);
        }


        // =========================
        // POST: LOG WORK (Detailed Version)
        // =========================
        [HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> LogWork(
    int moduleId,
    decimal hours,
    string description,
    string evidenceUrl,
    string location,
    string customTopic,
    DateTime logDate, // 👈 NEW PARAMETER
    bool practiceDone,
    int? quizScore,
    int? miniProjectProgress,
    string? blocker,
    string? nextGoal)
        {
            var user = await _userManager.GetUserAsync(User);
            var student = await _context.Students
                .Include(s => s.ModuleCompletions)
                .Include(s => s.ProgressLogs) // 👈 Need logs to calculate daily total
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (student == null) return NotFound();

            // ==================================================
            // 🛡️ RULE 1: DATE RESTRICTION (Today & Yesterday Only)
            // ==================================================
            var today = DateTime.UtcNow.Date;
            var yesterday = today.AddDays(-1);

            // Normalize input date to remove time part
            logDate = logDate.Date;

            if (logDate < yesterday || logDate > today)
            {
                TempData["Error"] = "🚫 You can only log work for Today or Yesterday.";
                return RedirectToAction(nameof(Dashboard));
            }

            // ==================================================
            // 🛡️ RULE 2: TOTAL DAILY LIMIT (Max 5 Hours)
            // ==================================================
            // 1. Calculate hours already logged for this specific date
            decimal existingHours = student.ProgressLogs
                .Where(l => l.Date.Date == logDate)
                .Sum(l => l.Hours);

            // 2. Check if new total exceeds limit
            if (existingHours + hours > 5)
            {
                decimal remaining = 5 - existingHours;
                string msg = remaining > 0
                    ? $"🚫 Daily limit exceeded. You have already logged {existingHours} hours for {logDate:MMM dd}. You can only add {remaining} more."
                    : $"🚫 Daily limit reached. You cannot log any more hours for {logDate:MMM dd}.";

                TempData["Error"] = msg;
                return RedirectToAction(nameof(Dashboard));
            }

            // ==================================================
            // 🛡️ HANDLE "OTHER" TOPIC
            // ==================================================
            if (moduleId == -1)
            {
                if (string.IsNullOrWhiteSpace(customTopic))
                {
                    TempData["Error"] = "Please specify what you learned.";
                    return RedirectToAction(nameof(Dashboard));
                }

                var allModules = await _context.SyllabusModules
                    .Where(m => m.TrackId == student.TrackId)
                    .OrderBy(m => m.DisplayOrder)
                    .ToListAsync();

                var completedIds = student.ModuleCompletions.Select(c => c.ModuleId).ToList();
                var currentModule = allModules.FirstOrDefault(m => !completedIds.Contains(m.Id)) ?? allModules.Last();

                moduleId = currentModule.Id;
                description = $"[Self-Study: {customTopic}] " + description;
            }

            // ==================================================
            // 💾 SAVE LOG
            // ==================================================
            var log = new ProgressLog
            {
                StudentId = student.Id,
                ModuleId = moduleId,
                Date = logDate, // 👈 Use the user-selected date
                Hours = hours,
                Location = location,
                ActivityDescription = description,
                EvidenceUrl = evidenceUrl,
                PracticeDone = practiceDone,
                QuizScore = quizScore,
                MiniProjectProgress = miniProjectProgress,
                Blocker = blocker,
                NextGoal = nextGoal,
                IsApproved = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.ProgressLogs.Add(log);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ Log submitted for {logDate:MMM dd}!";
            return RedirectToAction(nameof(Dashboard));
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
            if (user == null) return RedirectToAction("Login", "Account");

            var student = await _context.Students
                .Include(s => s.Track)
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (student == null)
                return RedirectToAction(nameof(Dashboard));

            int trackId = student.TrackId;

            // 1. Load ALL modules for this track
            var modules = await _context.SyllabusModules
                .Where(m => m.TrackId == trackId && m.IsActive)
                .Include(m => m.Resources) // ModuleResources
                .OrderBy(m => m.DisplayOrder)
                .ToListAsync();

            // 2. Load completed modules
            var completedIds = await _context.ModuleCompletions
                .Where(mc => mc.StudentId == student.Id && mc.IsCompleted)
                .Select(mc => mc.ModuleId)
                .ToListAsync();

            // 3. Build ViewModel (NO NULL RISK)
            var model = modules.Select((m, index) =>
            {
                bool isLocked = m.DisplayOrder > 1 &&
                    !completedIds.Contains(
                        modules.First(x => x.DisplayOrder == m.DisplayOrder - 1).Id
                    );

                return new CurriculumViewModel
                {
                    Id = m.Id,
                    ModuleCode = m.ModuleCode,
                    ModuleName = m.ModuleName,
                    Title = m.ModuleName,
                    Topics = m.Topics,
                    Description = m.Topics,
                    RequiredHours = m.RequiredHours,
                    Difficulty = m.DifficultyLevel,
                    IsCompleted = completedIds.Contains(m.Id),
                    IsLocked = isLocked,
                    IsMiniProject = m.IsMiniProject,


                    Resources = m.Resources
                        .Where(r => r.IsActive)
                        .ToList()
                };
            }).ToList();

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
        // =========================
        // GET: Leaderboard (Hall of Fame)
        // =========================
        [HttpGet]
        public async Task<IActionResult> Leaderboard()
        {
            var user = await _userManager.GetUserAsync(User);
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);
            if (student == null) return RedirectToAction("Dashboard");

            // 1. Get Top 20 Students (Active only)
            // Ordered by: Total Verified Hours (Desc), then Name (Asc)
            var leaderboard = await _context.Students
                .Include(s => s.Track)
                .Include(s => s.Cohort)
                .Include(s => s.ProgressLogs) // Need logs to sum hours
                .Where(s => s.EnrollmentStatus == "Active")
                .Select(s => new LeaderboardViewModel
                {
                    StudentId = s.Id,
                    FullName = s.FullName,
                    TrackCode = s.Track != null ? s.Track.Code : "N/A",
                    CohortName = s.Cohort != null ? s.Cohort.Name : "N/A",
                    ProfilePicture = s.ProfilePicture,
                  
                    TotalHours = s.ProgressLogs.Where(l => l.IsApproved).Sum(l => (decimal?)l.Hours) ?? 0
                })
                .OrderByDescending(x => x.TotalHours)
                .Take(20)
                .ToListAsync();

            // 2. Find My Rank
            // We scan the list to see if "I" am inside the top 20
            ViewBag.MyStudentId = student.Id;

            return View(leaderboard);
        }

        
        // =========================
        // GET: Certificate of Completion
        // =========================
        [HttpGet]
        public async Task<IActionResult> Certificate()
        {
            var user = await _userManager.GetUserAsync(User);
            var student = await _context.Students
                .Include(s => s.Track)
                .Include(s => s.Cohort)
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (student == null) return RedirectToAction("Dashboard");

            // 1. Get Total Modules Count for this Track
            var totalModules = await _context.SyllabusModules
                .CountAsync(m => m.TrackId == student.TrackId && m.IsActive);

            // 2. Get Student's Completed Modules Count
            var completedCount = await _context.ModuleCompletions
                .CountAsync(mc => mc.StudentId == student.Id && mc.IsCompleted);

            // 3. Calculate Progress
            double progress = totalModules == 0 ? 0 : ((double)completedCount / totalModules) * 100;

            // 4. Pass Data to View
            ViewBag.IsEligible = completedCount >= totalModules && totalModules > 0;
            ViewBag.Progress = (int)progress;
            ViewBag.CompletedCount = completedCount;
            ViewBag.TotalModules = totalModules;

            return View(student);
        }

        [HttpGet]
        public IActionResult Settings()
        {
            return View();
        }

        // =========================
        // POST: Request Account Deletion
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestDeletion(string reason)
        {
            var user = await _userManager.GetUserAsync(User);
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (student == null) return RedirectToAction("Login", "Account");

            // Check if a request is already pending to avoid spam
            bool alreadyRequested = await _context.SupportTickets.AnyAsync(t =>
                t.StudentId == student.Id &&
                t.Subject == "⚠️ ACCOUNT DELETION REQUEST" &&
                !t.IsResolved);

            if (alreadyRequested)
            {
                TempData["Error"] = "You already have a pending deletion request.";
                return RedirectToAction(nameof(Profile)); // Assuming Profile is the settings page
            }

            // Create the Support Ticket
            var ticket = new SupportTicket
            {
                StudentId = student.Id,
                Subject = "⚠️ ACCOUNT DELETION REQUEST",
                Category = "Account Issue", // Ensure this category exists or use "Other"
                Message = $"Student has requested account deletion.\n\nReason Provided: {(string.IsNullOrEmpty(reason) ? "No reason given." : reason)}",
                Status = "Open",
                IsResolved = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.SupportTickets.Add(ticket);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Deletion request sent to Admin. You will be contacted shortly.";
            return RedirectToAction(nameof(Profile));
        }
    }
}