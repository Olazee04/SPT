using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;
using SPT.Models.ViewModels;
using SPT.Services;

namespace SPT.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly IEmailService _emailService;
        private readonly AuditService _auditService;

        private static DateTime ToUtc(DateTime dt) =>
            DateTime.SpecifyKind(dt, DateTimeKind.Utc);

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env,
            IEmailService emailService,
            AuditService auditService)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
            _emailService = emailService;
            _auditService = auditService;
        }

        // =========================
        // ADMIN DASHBOARD
        // =========================
        [Authorize(Roles = "Admin, Mentor")]
        public async Task<IActionResult> Dashboard()
        {
            var mentors = await _userManager.GetUsersInRoleAsync("Mentor");
            var students = await _context.Students
                .Include(s => s.Track)
                .Include(s => s.ProgressLogs)
                .Include(s => s.ModuleCompletions)
                .ToListAsync();

            var allModules = await _context.SyllabusModules.ToListAsync();

            var model = new AdminDashboardViewModel
            {
                PendingLogs = await _context.ProgressLogs.CountAsync(l => !l.IsApproved),
                OpenTickets = await _context.SupportTickets.CountAsync(t => t.Status == "Open"),
                TotalStudents = students.Count,
                TotalMentors = mentors.Count
            };

            var performanceList = new List<StudentPerformanceDTO>();
            var sevenDaysAgo = DateTime.UtcNow.Date.AddDays(-6);

            foreach (var s in students)
            {
                var recentLogs = s.ProgressLogs
                    .Where(l => l.Date >= sevenDaysAgo && l.IsApproved)
                    .ToList();

                decimal weeklyHours = recentLogs.Sum(l => l.Hours);
                int checkIns = recentLogs.Select(l => l.Date.Date).Distinct().Count();

                int totalTrackModules = allModules.Count(m => m.TrackId == s.TrackId);
                int completedCount = s.ModuleCompletions.Count(mc => mc.IsCompleted);

                var ratedLogs = s.ProgressLogs
                    .Where(l => l.MentorRating.HasValue)
                    .OrderByDescending(l => l.Date)
                    .Take(3)
                    .ToList();

                double avgScore = ratedLogs.Any()
                    ? ratedLogs.Average(l => l.MentorRating.Value)
                    : 0;

                performanceList.Add(new StudentPerformanceDTO
                {
                    StudentId = s.Id,
                    FullName = s.FullName,
                    Track = s.Track?.Code ?? "N/A",
                    ProfilePicture = s.ProfilePicture,
                    WeeklyHours = weeklyHours,
                    WeeklyCheckIns = checkIns,
                    TotalModules = totalTrackModules,
                    CompletedModules = completedCount,
                    AverageMentorScore = Math.Round(avgScore, 1)
                });
            }

            model.StudentPerformance = performanceList.OrderByDescending(p => p.ConsistencyScore).ToList();
            model.ActiveStudents = performanceList.Count(p => p.Status == "Active");
            if (performanceList.Any())
                model.AvgConsistency = (decimal)performanceList.Average(p => p.ConsistencyScore);

            var trackGroups = students
                .Where(s => s.EnrollmentStatus == "Active")
                .GroupBy(s => s.Track?.Code ?? "Unassigned")
                .Select(g => new { Track = g.Key, Count = g.Count() })
                .ToList();

            model.TrackLabels = trackGroups.Select(x => x.Track).ToArray();
            model.TrackCounts = trackGroups.Select(x => x.Count).ToArray();

            var allLogsLast7Days = await _context.ProgressLogs
                .Where(l => l.Date >= sevenDaysAgo)
                .GroupBy(l => l.Date.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToListAsync();

            var dateLabels = new List<string>();
            var logCounts = new List<int>();

            for (int i = 0; i < 7; i++)
            {
                var date = sevenDaysAgo.AddDays(i);
                var record = allLogsLast7Days.FirstOrDefault(l => l.Date == date);
                dateLabels.Add(date.ToString("MMM dd"));
                logCounts.Add(record?.Count ?? 0);
            }

            model.ActivityDates = dateLabels.ToArray();
            model.ActivityCounts = logCounts.ToArray();

            // At-Risk Students: no approved log in last 5 days
            var fiveDaysAgo = DateTime.UtcNow.Date.AddDays(-5);

            model.AtRiskStudents = students
                .Where(s => s.EnrollmentStatus == "Active" &&
                            !s.ProgressLogs.Any(l => l.IsApproved && l.Date >= fiveDaysAgo))
                .Select(s => new AtRiskStudentDTO
                {
                    StudentId = s.Id,
                    FullName = s.FullName,
                    ProfilePicture = s.ProfilePicture,
                    LastLogDate = s.ProgressLogs
                        .Where(l => l.IsApproved)
                        .OrderByDescending(l => l.Date)
                        .Select(l => (DateTime?)l.Date)
                        .FirstOrDefault()
                })
                .ToList();

            return View(model);

        }

        // =========================
        // PENDING LOGS
        // =========================
        public async Task<IActionResult> PendingLogs()
        {
            var logs = await _context.ProgressLogs
                .Include(l => l.Student)
                .Include(l => l.Module)
                .Where(l => !l.IsApproved)
                .OrderBy(l => l.Date)
                .ToListAsync();

            return View(logs);
        }

        // =========================
        // UPDATE / APPROVE / REJECT LOG
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateLog(int id, decimal? hours, string? description, int? mentorRating, int? quizScore, string? action)
        {
            var log = await _context.ProgressLogs
                .Include(l => l.Student)
                .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (log == null)
            {
                TempData["Error"] = "Log not found.";
                return RedirectToAction("ProgressLogs");
            }

            if (action == "Reject")
            {
                if (log.Student?.User != null)
                {
                    string reason = "The submission did not meet the requirements.";
                    string body = $"<p>Your log for {log.Date:d} was rejected.</p><p><strong>Reason:</strong> {reason}</p>";
                    try { await _emailService.SendEmailAsync(log.Student.Email, "Log Rejected", body); } catch { }
                }

                log.IsApproved = false;
                log.IsRejected = true;
                log.RejectionReason = "Did not meet requirements";
                log.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                TempData["Error"] = "❌ Log rejected.";
                return RedirectToAction("ProgressLogs");
            }

            var dateToCheck = log.Date.Date;
            decimal otherLogsTotal = await _context.ProgressLogs
                .Where(l => l.StudentId == log.StudentId && l.Date.Date == dateToCheck && l.Id != id)
                .SumAsync(l => l.Hours);

            decimal proposedHours = hours ?? log.Hours;

            if ((otherLogsTotal + proposedHours) > 5)
            {
                TempData["Error"] = $"⚠️ Limit Exceeded! Student has {otherLogsTotal} hrs already. Adding {proposedHours} hrs totals {otherLogsTotal + proposedHours} (Max 5).";
                return RedirectToAction("ProgressLogs");
            }

            log.Hours = proposedHours;
            if (!string.IsNullOrEmpty(description)) log.ActivityDescription = description;
            if (mentorRating.HasValue) log.MentorRating = mentorRating;
            if (!log.PracticeDone) log.QuizScore = null;
            if (quizScore.HasValue) log.QuizScore = quizScore;

            var mentorResponse = Request.Form["mentorResponse"].ToString();
            if (!string.IsNullOrWhiteSpace(mentorResponse) && log.MentorResponse != mentorResponse)
            {
                log.MentorResponse = mentorResponse;
                if (log.Student?.User != null)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = log.Student.User.Id,
                        Title = "Mentor Feedback",
                        Message = $"💬 Mentor responded to your log ({log.Date:MMM dd}).",
                        Type = "Info",
                        Url = "/Student/Dashboard",
                        TargetPage = "Dashboard",
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    });
                }
            }

            log.IsApproved = true;
            log.UpdatedAt = DateTime.UtcNow;
            log.VerifiedByUserId = _userManager.GetUserId(User);

            if (log.Student?.User != null)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = log.Student.User.Id,
                    Title = "Log Approved",
                    Message = $"Your log for {log.Date:MMM dd} was approved.",
                    Type = "Success",
                    Url = "/Student/Dashboard",
                    TargetPage = "SomePage",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                });
            }

            try
            {
                _context.Entry(log).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                await _auditService.LogAsync(
                    "LOG_APPROVED_ADMIN",
                    $"Admin approved log #{log.Id}",
                    User.Identity.Name,
                    _userManager.GetUserId(User));

                TempData["Success"] = "✅ Log verified successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Database Error: " + ex.Message;
            }

            string referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer) && referer.Contains("Dashboard"))
                return RedirectToAction("Dashboard");

            return RedirectToAction("ProgressLogs");
        }

        // =========================
        // LIST STUDENTS — with search + pagination
        // =========================
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Students(string searchString, int page = 1, int pageSize = 20)
        {
            ViewBag.TotalStudents = await _context.Students.CountAsync();

            var query = _context.Students
                .Include(s => s.Track)
                .Include(s => s.Cohort)
                .Include(s => s.Mentor)
                .Include(s => s.User)
                .Include(s => s.ProgressLogs)
                .Include(s => s.ModuleCompletions)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
                query = query.Where(s => s.FullName.Contains(searchString) || s.Email.Contains(searchString));

            int totalCount = await query.CountAsync();

            var students = await query
                .OrderBy(s => s.FullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var modelList = new List<StudentPerformanceViewModel>();

            // ✅ FIX: Week starts on MONDAY — resets every Monday at 00:00
            var today = DateTime.UtcNow.Date;
            int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
            var startOfWeek = today.AddDays(-daysSinceMonday);

            var trackModuleCounts = await _context.SyllabusModules
                .Where(m => m.IsActive)
                .GroupBy(m => m.TrackId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            foreach (var s in students)
            {
                // ✅ FIX: Weekly hours use Monday-based week
                var weekLogs = s.ProgressLogs.Where(l => l.Date.Date >= startOfWeek && l.IsApproved).ToList();
                decimal hoursThisWeek = weekLogs.Sum(l => l.Hours);
                int checkInsThisWeek = weekLogs.Select(l => l.Date.Date).Distinct().Count();

                int totalMods = trackModuleCounts.ContainsKey(s.TrackId) ? trackModuleCounts[s.TrackId] : 1;
                int completedMods = s.ModuleCompletions.Count(mc => mc.IsCompleted);

                int consistency = 0;
                if (s.TargetHoursPerWeek > 0)
                {
                    consistency = (int)((hoursThisWeek / s.TargetHoursPerWeek) * 100);
                    if (consistency > 100) consistency = 100;
                }

                string status = "Active";
                if (s.EnrollmentStatus == "Suspended") status = "Inactive";
                else if (consistency < 30) status = "At Risk";

                modelList.Add(new StudentPerformanceViewModel
                {
                    StudentId = s.Id,
                    UserId = s.UserId,
                    Username = s.User?.UserName,
                    FullName = s.FullName,
                    Email = s.Email,
                    ProfilePicture = s.ProfilePicture,
                    CohortName = s.Cohort?.Name ?? "N/A",
                    TrackCode = s.Track?.Code ?? "N/A",
                    MentorName = s.Mentor?.FullName ?? "Unassigned",
                    TargetHoursPerWeek = s.TargetHoursPerWeek,
                    HoursLast7Days = hoursThisWeek,      // ✅ Now = this week (Mon–Sun)
                    CheckInsLast7Days = checkInsThisWeek,
                    CompletedModules = completedMods,
                    TotalModules = totalMods,
                    ConsistencyScore = consistency,
                    Status = status
                });
            }

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.SearchString = searchString;

            return View(modelList);
        }



        // =========================
        // CREATE STUDENT (ADMIN ONLY)
        // =========================
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateStudent()
        {
            await PopulateDropdowns();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateStudent(Student model, IFormFile? profilePicture, string password)
        {
            ModelState.Remove("UserId");
            ModelState.Remove("CohortId");
            ModelState.Remove("User");
            ModelState.Remove("Cohort");
            ModelState.Remove("Track");

            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .Select(x => $"{x.Key}: {string.Join(", ", x.Value.Errors.Select(e => e.ErrorMessage))}")
                    .ToList();

                TempData["Error"] = "VALIDATION FAILED: " + string.Join(" | ", errors);
                await PopulateDropdowns();
                return View(model);
            }

            // ✅ FIX 1: Declare username here (only once)
            string username = string.Empty;
            string finalPassword = string.IsNullOrEmpty(password) ? "Student@123" : password;

            try
            {
                // STEP 1: Generate Username
                var nameParts = model.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string surname = nameParts.Length > 0 ? nameParts[0] : "Student";
                string firstInitial = nameParts.Length > 1 ? nameParts[1].Substring(0, 1) : "X";
                int nextId = await _context.Students.AnyAsync()
                    ? await _context.Students.MaxAsync(s => s.Id) + 1
                    : 1;

                // ✅ FIX 1: ASSIGN don't redeclare (no 'string' keyword here)
                username = $"{surname}{firstInitial}{nextId:D3}";

                while (await _userManager.FindByNameAsync(username) != null)
                {
                    nextId++;
                    username = $"{surname}{firstInitial}{nextId:D3}";
                }

                // STEP 2: Create Identity User
                var user = new ApplicationUser
                {
                    UserName = username,
                    Email = model.Email,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, finalPassword);

                if (!result.Succeeded)
                {
                    await PopulateDropdowns();
                    return View(model);
                }

                await _userManager.AddToRoleAsync(user, "Student");

                // STEP 3: Auto-Generate Cohort
                var track = await _context.Tracks.FindAsync(model.TrackId);
                if (track == null)
                {
                    await _userManager.DeleteAsync(user);
                    await PopulateDropdowns();
                    return View(model);
                }

                var joinedUtc = DateTime.SpecifyKind(model.DateJoined, DateTimeKind.Utc);
                string cohortName = $"{track.Code}{joinedUtc:MMyy}";
                var cohort = await _context.Cohorts.FirstOrDefaultAsync(c => c.Name == cohortName);

                if (cohort == null)
                {
                    cohort = new Cohort
                    {
                        Name = cohortName,
                        StartDate = joinedUtc,
                        EndDate = joinedUtc.AddMonths(6),
                        IsActive = true
                    };
                    _context.Cohorts.Add(cohort);
                    await _context.SaveChangesAsync();
                }

                // STEP 4: Handle Profile Picture
                if (profilePicture != null && profilePicture.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "profiles");
                    Directory.CreateDirectory(uploadsFolder);
                    var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(profilePicture.FileName)}";
                    using (var stream = new FileStream(Path.Combine(uploadsFolder, fileName), FileMode.Create))
                    {
                        await profilePicture.CopyToAsync(stream);
                    }
                    model.ProfilePicture = $"/uploads/profiles/{fileName}";
                }

                // STEP 5: Save Student
                model.UserId = user.Id;
                model.CohortId = cohort.Id;
                model.TargetHoursPerWeek = 25;
                model.EnrollmentStatus = "Active";
                model.DateJoined = joinedUtc;
                model.CreatedAt = DateTime.UtcNow;
                model.UpdatedAt = DateTime.UtcNow;
                model.Track = null;
                model.Cohort = null;
                model.Mentor = null;
                model.User = null;

                _context.ChangeTracker.Clear();
                _context.Students.Add(model);
                await _context.SaveChangesAsync();

                await _auditService.LogAsync(
                    "CREATE_STUDENT",
                    $"Student created: {model.FullName}",
                    User.Identity.Name,
                    _userManager.GetUserId(User));

                // Email credentials to student
                try
                {
                    string emailBody = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;padding:20px;border:1px solid #ddd;border-radius:8px;'>
    <h2 style='color:#0d6efd;'>Welcome to RMSys SPT Academy! 🎓</h2>
    <p>Hi <strong>{model.FullName}</strong>,</p>
    <p>Your student account has been created. Here are your login details:</p>
    <table style='border-collapse:collapse;width:100%;margin:16px 0;'>
        <tr style='background:#f8f9fa;'>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>Username</td>
            <td style='padding:10px;border:1px solid #dee2e6;'><strong>{username}</strong></td>
        </tr>
        <tr>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>Email</td>
            <td style='padding:10px;border:1px solid #dee2e6;'>{model.Email}</td>
        </tr>
        <tr style='background:#f8f9fa;'>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>Password</td>
            <td style='padding:10px;border:1px solid #dee2e6;'><strong>{finalPassword}</strong></td>
        </tr>
    </table>
    <p>You can login using either your <strong>username</strong> or <strong>email address</strong>.</p>
    <p>
        <a href='https://rmsysspt.onrender.com'
           style='background:#0d6efd;color:white;padding:10px 20px;border-radius:5px;text-decoration:none;display:inline-block;'>
            Login Now
        </a>
    </p>
    <p style='color:#dc3545;'><strong>⚠️ Please change your password immediately after logging in for the first time.</strong></p>
    <hr/>
    <p style='color:#6c757d;font-size:0.85rem;'>This is an automated message from RMSys SPT Academy. Do not reply to this email.</p>
</div>";

                    await _emailService.SendEmailAsync(model.Email, "🎓 Welcome to SPT Academy – Your Login Details", emailBody);
                    TempData["Success"] = $"✅ Student Created! Username: {username} — Login details sent to {model.Email}";

                }
                catch
                {
                    TempData["Success"] = $"✅ Student Created! Username: {username} | Temp Password: {finalPassword} (Email delivery failed — note this down)";
                }

                return RedirectToAction(nameof(Students));
            }
            // ✅ FIX 2: Outer catch was missing entirely
            catch (Exception ex)
            {
                TempData["Error"] = $"EXCEPTION: {ex.Message} | INNER: {ex.InnerException?.Message}";
                await PopulateDropdowns();
                return RedirectToAction(nameof(Students));
            }
        }


        // =========================
        // EDIT STUDENT (ADMIN ONLY)
        // =========================
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditStudent(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();
            await PopulateDropdowns();
            return View(student);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditStudent(int id, Student model, IFormFile? profilePicture)
        {
            if (id != model.Id) return NotFound();

            var existingStudent = await _context.Students.FindAsync(id);
            if (existingStudent == null) return NotFound();

            // Update allowed fields
            existingStudent.FullName = model.FullName;
            existingStudent.Email = model.Email;
            existingStudent.Phone = model.Phone;
            existingStudent.Address = model.Address;
            existingStudent.TrackId = model.TrackId;
            existingStudent.MentorId = model.MentorId;
            existingStudent.DateJoined = model.DateJoined;
            existingStudent.GitHubUrl = model.GitHubUrl;
            existingStudent.PortfolioUrl = model.PortfolioUrl;
            existingStudent.EmergencyContactName = model.EmergencyContactName;
            existingStudent.EmergencyContactPhone = model.EmergencyContactPhone;
            existingStudent.UpdatedAt = DateTime.UtcNow;

            // Handle Photo
            if (profilePicture != null && profilePicture.Length > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "profiles");
                Directory.CreateDirectory(uploadsFolder);
                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(profilePicture.FileName)}";
                using (var stream = new FileStream(Path.Combine(uploadsFolder, fileName), FileMode.Create))
                {
                    await profilePicture.CopyToAsync(stream);
                }
                existingStudent.ProfilePicture = $"/uploads/profiles/{fileName}";
            }

            await _context.SaveChangesAsync();
            // ✅ Email credentials privately instead of showing password in UI
            
            await _context.SaveChangesAsync();
            TempData["Success"] = "✅ Student updated successfully.";
            return RedirectToAction(nameof(Students));
        }

        // ================
        // DELETE STUDENT 
        // ================
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            var student = await _context.Students.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == id);
            if (student == null) return NotFound();

            // ✅ THIS IS WHERE YOU ADD THE AUDIT LOG
            await _auditService.LogAsync(
    "DELETE_STUDENT",
    $"Deleted student {student.FullName} ({student.Email})",
    User.Identity.Name,
    _userManager.GetUserId(User));

            // Delete User (Cascade deletes student profile)
            if (student.User != null)
            {
                await _userManager.DeleteAsync(student.User);
            }
            else
            {
                _context.Students.Remove(student);
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Student deleted successfully.";
            return RedirectToAction(nameof(Students));
        }

        // =========================
        // CREATE MENTOR (ADMIN ONLY)
        // =========================
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateMentor()
        {
            ViewBag.Tracks = new SelectList(await _context.Tracks.ToListAsync(), "Id", "Name");
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateMentor(
            Mentor mentor,
            string username,
            string email,
            string password,
            IFormFile? profilePicture)
        {
            ModelState.Remove("User");
            ModelState.Remove("UserId");
            ModelState.Remove("Track");

            if (!ModelState.IsValid)
            {
                ViewBag.Tracks = new SelectList(await _context.Tracks.ToListAsync(), "Id", "Name");
                return View(mentor);
            }

            var user = new ApplicationUser
            {
                UserName = username,
                Email = email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);

                ViewBag.Tracks = new SelectList(await _context.Tracks.ToListAsync(), "Id", "Name");
                return View(mentor);
            }

            await _userManager.AddToRoleAsync(user, "Mentor");

            mentor.UserId = user.Id;
            mentor.DateJoined = DateTime.UtcNow;

            if (profilePicture != null && profilePicture.Length > 0)
            {
                var uploads = Path.Combine(_env.WebRootPath, "uploads", "profiles");
                Directory.CreateDirectory(uploads);

                var fileName = Guid.NewGuid() + Path.GetExtension(profilePicture.FileName);
                var path = Path.Combine(uploads, fileName);

                using var stream = new FileStream(path, FileMode.Create);
                await profilePicture.CopyToAsync(stream);

                mentor.ProfilePicture = "/uploads/profiles/" + fileName;
            }

            _context.Mentors.Add(mentor);
            await _context.SaveChangesAsync();
            await _auditService.LogAsync(
 "CREATE_MENTOR",
 $"Mentor created: {mentor.FullName}",
 User.Identity.Name,
_userManager.GetUserId(User));

            try
            {
                string mentorWelcomeEmail = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;padding:20px;border:1px solid #ddd;border-radius:8px;'>
    <h2 style='color:#0d6efd;'>Welcome to RMSys SPT Academy! &#127979;</h2>
    <p>Hi <strong>{mentor.FullName}</strong>,</p>
    <p>Your mentor account has been created. Here are your login details:</p>
    <table style='border-collapse:collapse;width:100%;margin:16px 0;'>
        <tr style='background:#f8f9fa;'>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>Username</td>
            <td style='padding:10px;border:1px solid #dee2e6;'><strong>{username}</strong></td>
        </tr>
        <tr>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>Email</td>
            <td style='padding:10px;border:1px solid #dee2e6;'>{email}</td>
        </tr>
        <tr style='background:#f8f9fa;'>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>Password</td>
            <td style='padding:10px;border:1px solid #dee2e6;'><strong>{password}</strong></td>
        </tr>
    </table>
    <p>You can login using either your <strong>username</strong> or <strong>email address</strong>.</p>
    <p>
        <a href='https://rmsysspt.onrender.com'
           style='background:#0d6efd;color:white;padding:10px 20px;border-radius:5px;text-decoration:none;display:inline-block;'>
            Login Now
        </a>
    </p>
    <p style='color:#dc3545;'><strong>Please change your password immediately after your first login.</strong></p>
    <hr/>
    <p style='color:#6c757d;font-size:0.85rem;'>This is an automated message from RMSys SPT Academy.</p>
</div>";
                await _emailService.SendEmailAsync(email, "Welcome to SPT Academy - Your Login Details", mentorWelcomeEmail);
                TempData["Success"] = $"Mentor Created! Login details sent to {email}";
            }
            catch
            {
                TempData["Success"] = $"Mentor Created! Username: {username} | Password: {password} (Email delivery failed)";
            }

            return RedirectToAction("Mentors");

        }


        // =========================
        // LIST MENTORS
        // =========================
        [HttpGet]

        public async Task<IActionResult> Mentors()
        {
            // Include the User to get email, and Students to count them
            var mentors = await _context.Mentors
                .Include(m => m.User)
                .Include(m => m.Students)
                .Include(m => m.Track)
                .ToListAsync();

            ViewBag.TotalStudents = await _context.Students.CountAsync();

            return View(mentors);
        }

        [HttpGet]
        public async Task<IActionResult> EditMentor(int id)
        {
            var mentor = await _context.Mentors
                .Include(m => m.User)   // ← THIS was missing — causes email to be null
                .FirstOrDefaultAsync(m => m.Id == id);
            if (mentor == null) return NotFound();

            ViewBag.Tracks = new SelectList(_context.Tracks, "Id", "Name", mentor.TrackId);
            return View("EditMentor", mentor);  // ← use dedicated view, not CreateMentor
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditMentor(Mentor mentor, IFormFile? profilePicture)
        {
            var dbMentor = await _context.Mentors.FindAsync(mentor.Id);
            if (dbMentor == null) return NotFound();

            dbMentor.FullName = mentor.FullName;
            dbMentor.Phone = mentor.Phone;
            dbMentor.Address = mentor.Address;
            dbMentor.NextOfKin = mentor.NextOfKin;
            dbMentor.NextOfKinPhone = mentor.NextOfKinPhone;
            dbMentor.NextOfKinAddress = mentor.NextOfKinAddress;

            if (profilePicture != null && profilePicture.Length > 0)
            {
                var uploads = Path.Combine(_env.WebRootPath, "uploads", "profiles");
                Directory.CreateDirectory(uploads);

                var fileName = Guid.NewGuid() + Path.GetExtension(profilePicture.FileName);
                var path = Path.Combine(uploads, fileName);

                using var stream = new FileStream(path, FileMode.Create);
                await profilePicture.CopyToAsync(stream);

                dbMentor.ProfilePicture = "/uploads/profiles/" + fileName;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Mentor updated";
            return RedirectToAction("Mentors");
        }


        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMentor(int id)
        {
            var mentor = await _context.Mentors
                .Include(m => m.User)
                 .Include(m => m.Students)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (mentor == null) return NotFound();

            // optional safety — block if has students
            if (mentor.Students.Any())
            {
                TempData["Error"] = "Cannot delete mentor with assigned students.";
                return RedirectToAction("Mentors");
            }

            _context.Mentors.Remove(mentor);
            await _userManager.DeleteAsync(mentor.User);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Mentor deleted successfully.";
            return RedirectToAction("Mentors");
        }



        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetUserPassword(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            // Generate a readable temp password
            var newPassword = "Temp@" + new Random().Next(1000, 9999);

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
                // ✅ FIX: redirect back to wherever we came from
                string referer = Request.Headers["Referer"].ToString();
                if (!string.IsNullOrEmpty(referer) && referer.Contains("Mentor"))
                    return RedirectToAction("Mentors");
                return RedirectToAction("Students");
            }

            await _auditService.LogAsync(
                "PASSWORD_RESET",
                $"Admin reset password for {user.Email}",
                User.Identity.Name,
                _userManager.GetUserId(User));

            // Find the person's full name
            string fullName = user.UserName ?? "User";
            var mentor = await _context.Mentors.FirstOrDefaultAsync(m => m.UserId == userId);
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == userId);
            if (mentor != null) fullName = mentor.FullName;
            else if (student != null) fullName = student.FullName;

            // Send email with new password
            try
            {
                string emailBody = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;padding:20px;border:1px solid #ddd;border-radius:8px;'>
    <h2 style='color:#dc3545;'>Password Reset &#128274;</h2>
    <p>Hi <strong>{fullName}</strong>,</p>
    <p>Your SPT Academy account password has been reset by an administrator.</p>
    <table style='border-collapse:collapse;width:100%;margin:16px 0;'>
        <tr style='background:#f8f9fa;'>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>Username</td>
            <td style='padding:10px;border:1px solid #dee2e6;'>{user.UserName}</td>
        </tr>
        <tr>
            <td style='padding:10px;font-weight:bold;border:1px solid #dee2e6;'>New Temporary Password</td>
            <td style='padding:10px;border:1px solid #dee2e6;'><strong style='color:#dc3545;'>{newPassword}</strong></td>
        </tr>
    </table>
    <p><strong>Login using your username above</strong> (not email) with the temporary password.</p>
    <p>
        <a href='https://rmsysspt.onrender.com'
           style='background:#0d6efd;color:white;padding:10px 20px;border-radius:5px;text-decoration:none;display:inline-block;'>
            Login Now
        </a>
    </p>
    <p style='color:#dc3545;'><strong>&#9888; Please change your password immediately after logging in.</strong></p>
    <hr/>
    <p style='color:#6c757d;font-size:0.85rem;'>This is an automated security message from RMSys SPT Academy. Do not reply.</p>
</div>";

                await _emailService.SendEmailAsync(user.Email, "SPT Academy - Your Password Has Been Reset", emailBody);
                TempData["Success"] = $"Password reset. New password sent to {user.Email}";
            }
            catch
            {
                TempData["Success"] = $"Password reset. New temp password: {newPassword} (Email failed — share manually)";
            }

            // ✅ FIX: redirect to correct page based on who was reset
            if (mentor != null) return RedirectToAction("Mentors");
            return RedirectToAction("Students");
        }



        // =========================
        // ANNOUNCEMENTS
        // =========================
        [HttpGet]
        [Authorize(Roles = "Admin,Mentor")]
        public IActionResult CreateAnnouncement()
        {
            return View(new Announcement());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Mentor")]
        public async Task<IActionResult> CreateAnnouncement(Announcement model)
        {
            ModelState.Remove("TargetPage");
            model.TargetPage = "Dashboard";

            if (ModelState.IsValid)
            {
                model.PostedBy = User.Identity?.Name ?? "System";
                model.CreatedAt = DateTime.UtcNow;
                _context.Announcements.Add(model);

                await _auditService.LogAsync(
                    "ANNOUNCEMENT_CREATED",
                    $"Announcement: {model.Title}",
                    User.Identity.Name,
                    _userManager.GetUserId(User));

                await _context.SaveChangesAsync();

                // Build recipient list based on audience
                var recipientUsers = new List<ApplicationUser>();

                if (model.Audience == "Students" || model.Audience == "All")
                    recipientUsers.AddRange(await _userManager.GetUsersInRoleAsync("Student"));

                if (model.Audience == "Mentors" || model.Audience == "All")
                    recipientUsers.AddRange(await _userManager.GetUsersInRoleAsync("Mentor"));

                // Admins ALWAYS receive every announcement
                recipientUsers.AddRange(await _userManager.GetUsersInRoleAsync("Admin"));

                // Determine redirect: Mentor goes to Mentor dashboard, Admin to Admin dashboard
                string redirectUrl = User.IsInRole("Admin") ? "/Admin/Dashboard" : "/Mentor/Dashboard";

                foreach (var user in recipientUsers.DistinctBy(u => u.Id))
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = user.Id,
                        Title = "📢 " + model.Title,
                        Message = model.Message,
                        Type = "Info",
                        IsRead = false,
                        TargetPage = "Dashboard",
                        Url = redirectUrl,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = $"📢 Announcement sent to {model.Audience} (+ Admins)!";

                return User.IsInRole("Admin")
                    ? RedirectToAction("Dashboard", "Admin")
                    : RedirectToAction("Dashboard", "Mentor");
            }

            return View(model);
        }
        // =========================
        // MASTER PROGRESS LOGS (History & Review)
        // =========================
        [HttpGet]
        public async Task<IActionResult> ProgressLogs(string status = "All", string search = "", int? studentId = null)
        {
            var query = _context.ProgressLogs
                .Include(l => l.Student)
                .ThenInclude(s => s.Track)
                .Include(l => l.Module)
                .AsQueryable();

              if (studentId.HasValue)
            {
                query = query.Where(l => l.StudentId == studentId.Value);

                
                var studentName = await _context.Students
                    .Where(s => s.Id == studentId)
                    .Select(s => s.FullName)
                    .FirstOrDefaultAsync();

                ViewBag.FilterName = studentName;
                ViewBag.IsFiltered = true;
            }

            // 2. Filter by Status
            if (status == "Pending")
            {
                query = query.Where(l => !l.IsApproved);
            }
            else if (status == "Approved")
            {
                query = query.Where(l => l.IsApproved);
            }

            // 3. Filter by Student Name (Search Box)
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(l => l.Student.FullName.Contains(search));
            }

            // Order: Newest first
            query = query.OrderByDescending(l => l.Date);

            // Pass current filter state to View
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentSearch = search;

            var logs = await query.ToListAsync();
            return View(logs);
        }
        // =========================
        // STUDENT DETAILS (Profile View for Admin)
        // =========================
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Details(int id)
        {
            var student = await _context.Students
    .Include(s => s.Track)
    .Include(s => s.Cohort)
    .Include(s => s.Mentor)
    .Include(s => s.User)
    .Include(s => s.ProgressLogs.Where(l => l.QuizScore.HasValue))  // ← quiz logs only
        .ThenInclude(l => l.Module)
    .FirstOrDefaultAsync(s => s.Id == id);

            if (student == null) return NotFound();

            // Pass username via ViewBag
            ViewBag.Username = student.User?.UserName ?? "N/A";

            return View(student);
        }

        // =========================
        // HELPERS
        // =========================
        private async Task PopulateDropdowns(int? selectedTrackId = null)
        {
            var tracks = await _context.Tracks.Where(t => t.IsActive).ToListAsync();
            ViewBag.TrackList = tracks;
            ViewBag.Tracks = new SelectList(tracks, "Id", "Name");
            ViewBag.Cohorts = new SelectList(await _context.Cohorts.Where(c => c.IsActive).ToListAsync(), "Id", "Name");

            // ── Mentor dropdown: filtered + labelled ──
            var mentors = await _context.Mentors
                .Include(m => m.Track)
                .ToListAsync();

            // Show: General mentors always + mentors assigned to the selected track
            var filteredMentors = mentors
                .Where(m =>
                    m.Specialization == "General" ||
                    m.TrackId == null ||
                    (selectedTrackId.HasValue && m.TrackId == selectedTrackId))
                .Select(m => new
                {
                    Id = m.Id,
                    // Label: "FullName [General]" or "FullName [FSC]"
                    DisplayName = m.Specialization == "General" || m.TrackId == null
                        ? $"{m.FullName} [General]"
                        : $"{m.FullName} [{m.Track?.Code ?? m.Specialization}]"
                })
                .ToList();

            ViewBag.Mentors = new SelectList(filteredMentors, "Id", "DisplayName");
        }

        [HttpGet]
        public async Task<IActionResult> GetMentorsForTrack(int trackId)
        {
            var mentors = await _context.Mentors
                .Include(m => m.Track)
                .Where(m => m.Specialization == "General" || m.TrackId == null || m.TrackId == trackId)
                .ToListAsync();

            var result = mentors.Select(m => new
            {
                id = m.Id,
                name = (m.Specialization == "General" || m.TrackId == null)
                           ? $"{m.FullName} [General]"
                           : $"{m.FullName} [{(m.Track != null ? m.Track.Code : m.Specialization)}]"
                //                             
            }).ToList();

            return Json(result);
        }

        // =========================
        // SUPPORT TICKET MANAGEMENT
        // =========================
        [HttpGet]
        [Authorize(Roles = "Admin,Mentor")]
        public async Task<IActionResult> SupportTickets(string status = "Open")
        {
            var query = _context.SupportTickets
                .Include(t => t.Student)
                .OrderByDescending(t => t.CreatedAt)
                .AsQueryable();

            if (status != "All")
            {
                // Filter by Open/Resolved
                bool isResolved = status == "Resolved";
                query = query.Where(t => t.IsResolved == isResolved);
            }

            ViewBag.CurrentStatus = status;
            var tickets = await query.ToListAsync();
            return View(tickets);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Mentor")]
        [ValidateAntiForgeryToken]

        public async Task<IActionResult> ResolveTicket(int id, string response, string actionType)
        {
            // ✅ FIX 1: Include Student data to prevent NullReferenceException
            var ticket = await _context.SupportTickets
                .Include(t => t.Student)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null) return NotFound();

            // Update the Admin Response
            ticket.AdminResponse = response;

            // ✅ FIX 2: Check which button was clicked
            if (actionType == "Resolve")
            {
                ticket.IsResolved = true;
                ticket.Status = "Resolved";
                TempData["Success"] = "Response sent and ticket closed.";
            }
            else
            {
                // Just a reply, keep it open
                ticket.IsResolved = false;
                ticket.Status = "In Progress"; // Change status to show it's being worked on
                TempData["Success"] = "Response sent. Ticket remains open.";
            }

            // Send Notification (Only if Student exists)
            if (ticket.Student != null)
            {
                var notification = new Notification
                {
                    UserId = ticket.Student.UserId,
                    Title = "Support Ticket Update",
                    Message = $"💬 Admin responded to your ticket: '{ticket.Subject}'",
                    Type = "Info",
                    IsRead = false,
                    TargetPage = "SomePage",
                    Url = "/Support/Index",
                    CreatedAt = DateTime.UtcNow
                };
                _context.Notifications.Add(notification);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(SupportTickets));
        }

        [HttpGet]
        public async Task<IActionResult> ManageTracks()
        {
            var tracks = await _context.Tracks
                .Include(t => t.Students)
                .Include(t => t.Modules)
                .OrderBy(t => t.Name)
                .ToListAsync();

            return View(tracks);
        }

        // POST: /Admin/CreateTrack
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTrack(string name, string code)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
            {
                TempData["Error"] = "Track name and code are required.";
                return RedirectToAction("ManageTracks");
            }

            code = code.Trim().ToUpper();
            name = name.Trim();

            // Check for duplicate code
            bool codeExists = await _context.Tracks.AnyAsync(t => t.Code == code);
            if (codeExists)
            {
                TempData["Error"] = $"A track with code '{code}' already exists.";
                return RedirectToAction("ManageTracks");
            }

            var track = new Track
            {
                Name = name,
                Code = code,
                IsActive = true
            };

            _context.Tracks.Add(track);
            await _context.SaveChangesAsync();

            // Auto-create 19 modules for the new track (same pattern as SeedData)
            var modules = new List<SyllabusModule>();

            for (int i = 1; i <= 18; i++)
            {
                modules.Add(new SyllabusModule
                {
                    TrackId = track.Id,
                    DisplayOrder = i,
                    ModuleCode = $"{track.Code}-{i:00}",
                    ModuleName = $"{track.Name} – Module {i}",
                    RequiredHours = 8,
                    DifficultyLevel = i <= 5 ? "Beginner" : i <= 12 ? "Intermediate" : "Advanced",
                    Topics = $"Core learning content for {track.Name} (Part {i})",
                    HasQuiz = i % 3 == 0,
                    IsActive = true
                });
            }

            modules.Add(new SyllabusModule
            {
                TrackId = track.Id,
                DisplayOrder = 19,
                ModuleCode = $"CAP-{track.Code}",
                ModuleName = "Mini Project",
                RequiredHours = 40,
                DifficultyLevel = "Expert",
                Topics = $"Build a real-world {track.Name} project",
                HasProject = true,
                IsMiniProject = true,
                IsActive = true
            });

            _context.SyllabusModules.AddRange(modules);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Track '{name}' ({code}) created with 19 modules.";
            return RedirectToAction("ManageTracks");
        }

        // POST: /Admin/EditTrack
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTrack(int id, string name, string code)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
            {
                TempData["Error"] = "Track name and code are required.";
                return RedirectToAction("ManageTracks");
            }

            var track = await _context.Tracks.FindAsync(id);
            if (track == null)
            {
                TempData["Error"] = "Track not found.";
                return RedirectToAction("ManageTracks");
            }

            code = code.Trim().ToUpper();
            name = name.Trim();

            // Check duplicate code (excluding current track)
            bool codeExists = await _context.Tracks.AnyAsync(t => t.Code == code && t.Id != id);
            if (codeExists)
            {
                TempData["Error"] = $"A track with code '{code}' already exists.";
                return RedirectToAction("ManageTracks");
            }

            track.Name = name;
            track.Code = code;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Track updated to '{name}' ({code}).";
            return RedirectToAction("ManageTracks");
        }

        // POST: /Admin/ToggleTrack
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleTrack(int id)
        {
            var track = await _context.Tracks.FindAsync(id);
            if (track == null)
            {
                TempData["Error"] = "Track not found.";
                return RedirectToAction("ManageTracks");
            }

            track.IsActive = !track.IsActive;
            await _context.SaveChangesAsync();

            string status = track.IsActive ? "activated" : "deactivated";
            TempData["Success"] = $"Track '{track.Name}' has been {status}.";
            return RedirectToAction("ManageTracks");
        }

        // POST: /Admin/DeleteTrack
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTrack(int id)
        {
            var track = await _context.Tracks
                .Include(t => t.Students)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (track == null)
            {
                TempData["Error"] = "Track not found.";
                return RedirectToAction("ManageTracks");
            }

            if (track.Students != null && track.Students.Any())
            {
                TempData["Error"] = $"Cannot delete '{track.Name}' — it has {track.Students.Count} enrolled student(s). Deactivate it instead.";
                return RedirectToAction("ManageTracks");
            }

            _context.Tracks.Remove(track);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Track '{track.Name}' has been deleted.";
            return RedirectToAction("ManageTracks");
        }


        // =========================
        // LIBRARY MANAGEMENT
        // =========================
        [HttpGet]
        [Authorize(Roles = "Admin,Mentor")]
        public async Task<IActionResult> ManageLibrary()
        {
            var resources = await _context.Resources
                .Include(r => r.Track)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
            return View(resources);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Mentor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateResource(Resource model, int? trackId)
        {
            
            ModelState.Remove("Track");
            ModelState.Remove("Module");
            ModelState.Remove("TrackId");   // we handle it manually below

            model.TrackId = trackId;

            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.UtcNow;
                _context.Resources.Add(model);

                await _auditService.LogAsync(
                    "RESOURCE_CREATED",
                    $"Resource added: {model.Title}",
                    User.Identity.Name,
                    _userManager.GetUserId(User));

                await _context.SaveChangesAsync();
                TempData["Success"] = "Resource added successfully!";
                return RedirectToAction(nameof(ManageLibrary));
            }

            // Debug: show what failed
            var errors = ModelState
                .Where(x => x.Value.Errors.Count > 0)
                .Select(x => $"{x.Key}: {string.Join(", ", x.Value.Errors.Select(e => e.ErrorMessage))}")
                .ToList();
            TempData["Error"] = "Validation failed: " + string.Join(" | ", errors);

            var tracks = await _context.Tracks.ToListAsync();
            ViewBag.Tracks = new SelectList(tracks, "Id", "Name");
            ViewBag.TrackList = tracks;
            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Mentor")]
        public async Task<IActionResult> CreateResource()
        {
            var tracks = await _context.Tracks.ToListAsync();
            ViewBag.Tracks = new SelectList(tracks, "Id", "Name");
            ViewBag.TrackList = tracks;  // ADD THIS — needed for the manual foreach in the view
            return View();
        }


        [HttpPost]
        [Authorize(Roles = "Admin,Mentor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteResource(int id)
        {
            var resource = await _context.Resources.FindAsync(id);
            if (resource != null)
            {
                _context.Resources.Remove(resource);
                await _auditService.LogAsync(
    "RESOURCE_DELETED",
    $"Resource deleted: {resource.Title}",
    User.Identity.Name,
    _userManager.GetUserId(User));

                await _context.SaveChangesAsync();
                TempData["Success"] = "Resource deleted.";
            }
            return RedirectToAction(nameof(ManageLibrary));
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Mentor")]
        public async Task<IActionResult> ManageCurriculum(int? trackId)
        {
            bool isAdmin = User.IsInRole("Admin");
            int? mentorTrackId = null;

            if (!isAdmin)
            {
                var userId = _userManager.GetUserId(User);
                var mentor = await _context.Mentors.FirstOrDefaultAsync(m => m.UserId == userId);
                if (mentor != null)
                {
                    mentorTrackId = mentor.TrackId;
                    // Track-specific mentor: force to their track
                    if (mentor.Specialization != "General" && mentor.TrackId.HasValue)
                        trackId = mentor.TrackId;
                }
            }

            var availableTracks = isAdmin
                ? await _context.Tracks.Where(t => t.IsActive).ToListAsync()
                : await _context.Tracks.Where(t => t.IsActive && (mentorTrackId == null || t.Id == mentorTrackId)).ToListAsync();

            var query = _context.SyllabusModules
                .Include(m => m.Track)
                .Include(m => m.Resources)
                .Include(m => m.Questions)
                .AsQueryable();

            if (trackId.HasValue)
                query = query.Where(m => m.TrackId == trackId.Value);
            else if (!isAdmin && mentorTrackId.HasValue)
                query = query.Where(m => m.TrackId == mentorTrackId.Value);

            var modules = await query.OrderBy(m => m.TrackId).ThenBy(m => m.DisplayOrder).ToListAsync();

            ViewBag.Tracks = availableTracks;
            ViewBag.SelectedTrackId = trackId;
            ViewBag.IsAdmin = isAdmin;
            // Pass total counts per track for the tab badges
            ViewBag.TrackCounts = await _context.SyllabusModules
                .GroupBy(m => m.TrackId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            return View(modules);
        }

        // =========================
        // CREATE MODULE — GET
        // =========================
        [HttpGet]
        [Authorize(Roles = "Admin,Mentor")]
        public async Task<IActionResult> CreateModule(int? trackId)
        {
            ViewBag.Tracks = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                await _context.Tracks.Where(t => t.IsActive).ToListAsync(), "Id", "Name", trackId);
            ViewBag.SelectedTrackId = trackId;
            return View(new SyllabusModule { TrackId = trackId ?? 0, IsActive = true, RequiredHours = 8, PassScore = 75 });
        }

        // =========================
        // CREATE MODULE — POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Mentor")]
        public async Task<IActionResult> CreateModule(SyllabusModule model)
        {
            ModelState.Remove("Track");
            ModelState.Remove("PrerequisiteModule");
            ModelState.Remove("ProgressLogs");
            ModelState.Remove("ModuleCompletions");
            ModelState.Remove("Resources");
            ModelState.Remove("Questions");

            if (!ModelState.IsValid)
            {
                ViewBag.Tracks = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                    await _context.Tracks.Where(t => t.IsActive).ToListAsync(), "Id", "Name", model.TrackId);
                return View(model);
            }

            // Auto-set display order to next available for this track
            if (model.DisplayOrder == 0)
            {
                int maxOrder = await _context.SyllabusModules
                    .Where(m => m.TrackId == model.TrackId)
                    .MaxAsync(m => (int?)m.DisplayOrder) ?? 0;
                model.DisplayOrder = maxOrder + 1;
            }

            _context.SyllabusModules.Add(model);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("MODULE_CREATED", $"Module created: {model.ModuleName}", User.Identity.Name, _userManager.GetUserId(User));
            TempData["Success"] = $"✅ Module '{model.ModuleName}' created successfully.";
            return RedirectToAction(nameof(ManageCurriculum), new { trackId = model.TrackId });
        }

        // =========================
        // EDIT MODULE — GET
        // =========================
        [HttpGet]
        [Authorize(Roles = "Admin,Mentor")]
        public async Task<IActionResult> EditModule(int id)
        {
            var module = await _context.SyllabusModules
                .Include(m => m.Track)
                .Include(m => m.Resources)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (module == null) return NotFound();

            ViewBag.Tracks = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                await _context.Tracks.Where(t => t.IsActive).ToListAsync(), "Id", "Name", module.TrackId);

            return View(module);
        }

        // =========================
        // EDIT MODULE — POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Mentor")]
        public async Task<IActionResult> EditModule(SyllabusModule model)
        {
            ModelState.Remove("Track");
            ModelState.Remove("PrerequisiteModule");
            ModelState.Remove("ProgressLogs");
            ModelState.Remove("ModuleCompletions");
            ModelState.Remove("Resources");
            ModelState.Remove("Questions");

            if (!ModelState.IsValid)
            {
                ViewBag.Tracks = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                    await _context.Tracks.Where(t => t.IsActive).ToListAsync(), "Id", "Name", model.TrackId);
                return View(model);
            }

            var existing = await _context.SyllabusModules.FindAsync(model.Id);
            if (existing == null) return NotFound();

            existing.ModuleCode = model.ModuleCode;
            existing.ModuleName = model.ModuleName;
            existing.TrackId = model.TrackId;
            existing.Topics = model.Topics;
            existing.RequiredHours = model.RequiredHours;
            existing.DifficultyLevel = model.DifficultyLevel;
            existing.DisplayOrder = model.DisplayOrder;
            existing.HasQuiz = model.HasQuiz;
            existing.HasProject = model.HasProject;
            existing.IsMiniProject = model.IsMiniProject;
            existing.PassScore = model.PassScore;
            existing.IsActive = model.IsActive;
            existing.WeightPercentage = model.WeightPercentage;

            await _context.SaveChangesAsync();
            await _auditService.LogAsync("MODULE_UPDATED", $"Module updated: {model.ModuleName}", User.Identity.Name, _userManager.GetUserId(User));
            TempData["Success"] = $"✅ Module '{model.ModuleName}' updated successfully.";
            return RedirectToAction(nameof(ManageCurriculum), new { trackId = model.TrackId });
        }

        // =========================
        // DELETE MODULE — POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteModule(int id)
        {
            var module = await _context.SyllabusModules
                .Include(m => m.ModuleCompletions)
                .Include(m => m.Resources)
                .Include(m => m.Questions)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (module == null) return NotFound();

            // Block deletion if any student completed it
            if (module.ModuleCompletions.Any(mc => mc.IsCompleted))
            {
                TempData["Error"] = $"❌ Cannot delete '{module.ModuleName}' — {module.ModuleCompletions.Count(mc => mc.IsCompleted)} student(s) have completed it.";
                return RedirectToAction(nameof(ManageCurriculum), new { trackId = module.TrackId });
            }

            int trackId = module.TrackId;
            _context.SyllabusModules.Remove(module);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("MODULE_DELETED", $"Module deleted: {module.ModuleName}", User.Identity.Name, _userManager.GetUserId(User));
            TempData["Success"] = $"✅ Module '{module.ModuleName}' deleted.";
            return RedirectToAction(nameof(ManageCurriculum), new { trackId });
        }

        // =========================
        // TOGGLE MODULE ACTIVE
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Mentor")]
        public async Task<IActionResult> ToggleModule(int id)
        {
            var module = await _context.SyllabusModules.FindAsync(id);
            if (module == null) return NotFound();

            module.IsActive = !module.IsActive;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Module '{module.ModuleName}' is now {(module.IsActive ? "Active" : "Inactive")}.";
            return RedirectToAction(nameof(ManageCurriculum), new { trackId = module.TrackId });
        }

        // =========================
        // MANAGE RESOURCES — GET (for a specific module)
        // =========================
        [HttpGet]
        [Authorize(Roles = "Admin,Mentor")]
        public async Task<IActionResult> ManageResources(int moduleId)
        {
            var module = await _context.SyllabusModules
                .Include(m => m.Track)
                .Include(m => m.Resources)
                .FirstOrDefaultAsync(m => m.Id == moduleId);

            if (module == null) return NotFound();

            return View(module);
        }

        // =========================
        // ADD RESOURCE — POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Mentor")]
        public async Task<IActionResult> AddResource(int moduleId, string title, string url, string type)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
            {
                TempData["Error"] = "Title and URL are required.";
                return RedirectToAction(nameof(ManageResources), new { moduleId });
            }

            _context.ModuleResources.Add(new ModuleResource
            {
                ModuleId = moduleId,
                Title = title,
                Url = url,
                Type = type ?? "Article",
                IsActive = true
            });

            await _context.SaveChangesAsync();
            await _auditService.LogAsync("RESOURCE_ADDED", $"Resource added to module #{moduleId}: {title}", User.Identity.Name, _userManager.GetUserId(User));
            TempData["Success"] = "✅ Resource added successfully.";
            return RedirectToAction(nameof(ManageResources), new { moduleId });
        }

        // =========================
        // EDIT RESOURCE — POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Mentor")]
        public async Task<IActionResult> EditResource(int id, string title, string url, string type, bool isActive)
        {
            var resource = await _context.ModuleResources.FindAsync(id);
            if (resource == null) return NotFound();

            resource.Title = title;
            resource.Url = url;
            resource.Type = type;
            resource.IsActive = isActive;

            await _context.SaveChangesAsync();
            TempData["Success"] = "✅ Resource updated.";
            return RedirectToAction(nameof(ManageResources), new { moduleId = resource.ModuleId });
        }

        // =========================
        // DELETE RESOURCE — POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Mentor")]
        public async Task<IActionResult> DeleteResource_Module(int id)
        {
            var resource = await _context.ModuleResources.FindAsync(id);
            if (resource == null) return NotFound();

            int moduleId = resource.ModuleId;
            _context.ModuleResources.Remove(resource);
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ Resource deleted.";
            return RedirectToAction(nameof(ManageResources), new { moduleId });
        }
        // =========================
        // GET: MANAGE QUIZZES
        // =========================
        [Authorize(Roles = "Admin,Mentor")]
        public async Task<IActionResult> ManageQuizzes(int? trackId)
        {
            bool isAdmin = User.IsInRole("Admin");
            int? mentorTrackId = null;
            bool isGeneralMentor = false;

            if (!isAdmin)
            {
                var userId = _userManager.GetUserId(User);
                var mentor = await _context.Mentors.FirstOrDefaultAsync(m => m.UserId == userId);
                if (mentor != null)
                {
                    mentorTrackId = mentor.TrackId;
                    isGeneralMentor = mentor.Specialization == "General" || mentor.TrackId == null;
                }

                // Track-specific mentor: force filter to their track
                if (!isGeneralMentor && mentorTrackId.HasValue && !trackId.HasValue)
                    trackId = mentorTrackId;
            }

            // ── Available tracks for dropdown ──
            List<Track> availableTracks;
            if (isAdmin || isGeneralMentor)
                availableTracks = await _context.Tracks.ToListAsync();
            else
                availableTracks = await _context.Tracks
                    .Where(t => t.Id == mentorTrackId)
                    .ToListAsync();

            // ── Module query ──
            var query = _context.SyllabusModules
                .Include(m => m.Track)
                .Include(m => m.Questions)   // ✅ FIX: Must include so qCount works in view
                .AsQueryable();

            if (trackId.HasValue)
                query = query.Where(m => m.TrackId == trackId.Value);
            else if (!isAdmin && !isGeneralMentor && mentorTrackId.HasValue)
                query = query.Where(m => m.TrackId == mentorTrackId.Value);

            var modules = await query
                .OrderBy(m => m.Track.Name)
                .ThenBy(m => m.DisplayOrder)
                .ToListAsync();

            // ✅ FIX: Auto-correct phantom "Enabled" — HasQuiz=true but 0 questions
            bool correctionNeeded = false;
            foreach (var mod in modules)
            {
                if (mod.HasQuiz && (mod.Questions == null || !mod.Questions.Any()))
                {
                    mod.HasQuiz = false;
                    correctionNeeded = true;
                }
            }
            if (correctionNeeded)
                await _context.SaveChangesAsync();

            ViewBag.Tracks = availableTracks;
            ViewBag.SelectedTrackId = trackId;
            ViewBag.IsAdmin = isAdmin;
            ViewBag.MentorTrackId = mentorTrackId;

            return View(modules);
        }

        // =========================
        // QUIZ SCORES — Admin sees all students
        // =========================
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> QuizScores(string search = "", int? trackId = null, int page = 1, int pageSize = 30)
        {
            var query = _context.ProgressLogs
                .Include(l => l.Student).ThenInclude(s => s.Track)
                .Include(l => l.Module)
                .Where(l => l.QuizScore.HasValue)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(l => l.Student.FullName.Contains(search));

            if (trackId.HasValue)
                query = query.Where(l => l.Student.TrackId == trackId.Value);

            int totalCount = await query.CountAsync();

            var logs = await query
                .OrderByDescending(l => l.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Tracks = await _context.Tracks.ToListAsync();
            ViewBag.SelectedTrack = trackId;
            ViewBag.Search = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            return View(logs);
        }

        // =========================
        // MODULE QUIZ SCORES — Admin (QuizAttempts table)
        // =========================
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ModuleQuizScores(
            string search = "", int? trackId = null, int page = 1, int pageSize = 20)
        {
            var query = _context.QuizAttempts
                .Include(a => a.Student).ThenInclude(s => s.Track)
                .Include(a => a.Module)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(a => a.Student.FullName.Contains(search));

            if (trackId.HasValue)
                query = query.Where(a => a.Student.TrackId == trackId.Value);

            int total = await query.CountAsync();

            var data = await query
                .OrderByDescending(a => a.AttemptedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Tracks = await _context.Tracks.ToListAsync();
            ViewBag.SelectedTrack = trackId;   // int? — no cast error
            ViewBag.Search = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);

            return View(data);
        }


        public IActionResult QuizResults(string search = "", string result = "",
    int? moduleId = null, int page = 1, int pageSize = 20)
        {
            var query = _context.QuizAttempts
                .Include(a => a.Student).ThenInclude(s => s.Track)
                .Include(a => a.Module)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(a => a.Student.FullName.Contains(search));

            if (result == "pass") query = query.Where(a => a.Score >= 70);
            else if (result == "fail") query = query.Where(a => a.Score < 70);

            // ✅ FIX: guard with .HasValue before using .Value
            if (moduleId.HasValue && moduleId.Value > 0)
                query = query.Where(a => a.ModuleId == moduleId.Value);

            int total = query.Count();

            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.TotalRecords = total;
            ViewBag.Search = search;
            ViewBag.ResultFilter = result;
            ViewBag.SelectedModule = moduleId ?? 0;

            ViewBag.Modules = _context.SyllabusModules
                .Select(m => new { m.Id, m.ModuleCode, m.ModuleName })
                .ToList();

            var data = query
                .OrderByDescending(a => a.AttemptedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return View("~/Views/Quiz/AllResults.cshtml", data);
        }

        // =========================
        // BULK IMPORT STUDENTS
        // =========================
        [HttpGet]
        public IActionResult ImportStudents()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportStudents(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a valid CSV file.";
                return View();
            }

            if (!file.FileName.EndsWith(".csv"))
            {
                TempData["Error"] = "Only .csv files are allowed.";
                return View();
            }

            int successCount = 0;
            int errorCount = 0;
            var errors = new List<string>();

            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                var header = await reader.ReadLineAsync();

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var values = line.Split(',');

                    if (values.Length < 3)
                    {
                        errorCount++;
                        continue;
                    }

                    string fullName = values[0].Trim();
                    string email = values[1].Trim();
                    string trackCode = values[2].Trim();

                    if (await _userManager.FindByEmailAsync(email) != null)
                    {
                        errors.Add($"{email}: Email already exists.");
                        errorCount++;
                        continue;
                    }

                    var track = await _context.Tracks.FirstOrDefaultAsync(t => t.Code == trackCode);
                    if (track == null)
                    {
                        errors.Add($"{email}: Invalid Track Code '{trackCode}'.");
                        errorCount++;
                        continue;
                    }

                    var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
                    var result = await _userManager.CreateAsync(user, "Student@123");

                    if (result.Succeeded)
                    {
                        await _userManager.AddToRoleAsync(user, "Student");

                        var student = new Student
                        {
                            UserId = user.Id,
                            FullName = fullName,
                            Email = email,
                            TrackId = track.Id,
                            DateJoined = DateTime.UtcNow,
                            TargetHoursPerWeek = 20,
                            EnrollmentStatus = "Active"
                        };
                       
                        _context.Students.Add(student);
                        successCount++;

                        try
                        {
                            string body = $"<h1>Welcome to RMSys SPT Academy!</h1><p>Your account has been created.</p><p><strong>Email:</strong> {email}</p><p><strong>Password:</strong> Student@123</p><p>Please login and change your password immediately.</p>";
                            await _emailService.SendEmailAsync(email, "Welcome to SPT", body);
                        }
                        catch
                        {

                        }
                    }
                    else
                    {
                        errorCount++;
                        errors.Add($"{email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                    }
                }
                await _auditService.LogAsync(
    "STUDENT_IMPORT",
    $"Imported {successCount} students",
    User.Identity.Name,
    _userManager.GetUserId(User));

                await _context.SaveChangesAsync();
            }

            if (errorCount > 0)
            {
                TempData["Warning"] = $"Imported {successCount} students. {errorCount} failed. Check details below.";
                ViewBag.Errors = errors;
            }
            else
            {
                TempData["Success"] = $"Successfully imported {successCount} students!";
                return RedirectToAction("Students");
            }

            return View();
        }

        // Helper to Download Template
        public IActionResult DownloadTemplate()
        {
            var csv = "FullName,Email,TrackCode\nJohn Doe,john@example.com,FSC\nJane Smith,jane@test.com,API";
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "StudentImportTemplate.csv");
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportStudents()
        {
            var students = await _context.Students
                .Include(s => s.Track)
                .Include(s => s.Cohort)
                .Include(s => s.Mentor)
                .ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("FullName,Email,Track,Cohort,Mentor,DateJoined,Status");

            foreach (var s in students)
            {
                csv.AppendLine($"{s.FullName},{s.Email},{s.Track?.Code ?? "N/A"},{s.Cohort?.Name ?? "N/A"},{s.Mentor?.FullName ?? "Unassigned"},{s.DateJoined:yyyy-MM-dd},{s.EnrollmentStatus}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"Students_Export_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        // =========================
        // SYSTEM AUDIT LOGS
        // =========================
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AuditLogs(int page = 1, int pageSize = 50, string tab = "All")
        {
            var query = _context.AuditLogs.AsQueryable();

            // Filter by tab category
            query = tab switch
            {
                "Student" => query.Where(a =>
                    a.Action.Contains("STUDENT") ||
                    a.Action.Contains("IMPORT") ||
                    a.Action.Contains("LOG_APPROVED") ||
                    a.Action.Contains("LOG_REJECTED")),

                "Mentor" => query.Where(a =>
                    a.Action.Contains("MENTOR") ||
                    a.Action.Contains("LOG_APPROVED_MENTOR")),

                "Auth" => query.Where(a =>
                    a.Action.Contains("LOGIN") ||
                    a.Action.Contains("LOGOUT") ||
                    a.Action.Contains("PASSWORD") ||
                    a.Action.Contains("REQUEST")),

                "System" => query.Where(a =>
                    a.Action.Contains("RESOURCE") ||
                    a.Action.Contains("ANNOUNCEMENT") ||
                    a.Action.Contains("CERTIFICATE") ||
                    a.Action.Contains("CREATE_MENTOR") ||
                    a.Action.Contains("DELETE_MENTOR")),

                _ => query  // "All" — no filter
            };

            int totalCount = await query.CountAsync();

            var logs = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.ActiveTab = tab;

            return View(logs);
        }


    }
}