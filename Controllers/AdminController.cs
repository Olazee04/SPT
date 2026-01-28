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
    // ✅ Allow Mentors to access Dashboard and Reviews, but restrict specific actions below
    [Authorize(Roles = "Admin,Mentor")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly IEmailService _emailService; 
        private readonly AuditService _auditService;
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
        // ADMIN DASHBOARD (MERGED)
        // =========================
        public async Task<IActionResult> Dashboard()
        {
           
            var mentors = await _userManager.GetUsersInRoleAsync("Mentor");
            var students = await _context.Students
                .Include(s => s.Track)
                .Include(s => s.ProgressLogs)
                .Include(s => s.ModuleCompletions)
                .ToListAsync();

            var allModules = await _context.SyllabusModules.ToListAsync();

            // 2. Initialize ViewModel
            var model = new AdminDashboardViewModel
            {
                PendingLogs = await _context.ProgressLogs.CountAsync(l => !l.IsApproved),
                OpenTickets = await _context.SupportTickets.CountAsync(t => t.Status == "Open"),
                TotalStudents = students.Count,
                TotalMentors = mentors.Count
            };

            // ----------------------------------------------------
            // PART A: Student Analytics Table Calculation
            // ----------------------------------------------------
            var performanceList = new List<StudentPerformanceDTO>();
            var sevenDaysAgo = DateTime.UtcNow.Date.AddDays(-6); // Ensure 7 day range

            foreach (var s in students)
            {
                // Filter Logs for Last 7 Days
                var recentLogs = s.ProgressLogs
                    .Where(l => l.Date >= sevenDaysAgo && l.IsApproved)
                    .ToList();

                // Weekly Stats
                decimal weeklyHours = recentLogs.Sum(l => l.Hours);
                int checkIns = recentLogs.Select(l => l.Date.Date).Distinct().Count();

                // Modules
                int totalTrackModules = allModules.Count(m => m.TrackId == s.TrackId);
                int completedCount = s.ModuleCompletions.Count(mc => mc.IsCompleted);

                // Mentor Score
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
            {
                model.AvgConsistency = (decimal)performanceList.Average(p => p.ConsistencyScore);
            }

            // ----------------------------------------------------
            // PART B: Chart Data Preparation
            // ----------------------------------------------------

            // Chart 1: Students per Track
            var trackGroups = students
                .Where(s => s.EnrollmentStatus == "Active")
                .GroupBy(s => s.Track?.Code ?? "Unassigned")
                .Select(g => new { Track = g.Key, Count = g.Count() })
                .ToList();

            model.TrackLabels = trackGroups.Select(x => x.Track).ToArray();
            model.TrackCounts = trackGroups.Select(x => x.Count).ToArray();

            // Chart 2: Activity (Logs per Day)
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

            return View(model);
        }

        // =========================
        // PENDING LOGS (View All)
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
        // POST: Update, Approve, or Reject Log (Merged Logic)
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
                    await _emailService.SendEmailAsync(log.Student.Email, "Log Rejected", body);
                }
                _context.ProgressLogs.Remove(log);
                await _context.SaveChangesAsync();
                TempData["Error"] = "❌ Log rejected and removed.";
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
            if (quizScore.HasValue) log.QuizScore = quizScore;

            log.IsApproved = true;
            log.UpdatedAt = DateTime.UtcNow;
            log.VerifiedByUserId = _userManager.GetUserId(User);

            try
            {
                if (log.Student?.User != null)
                {
                    var notification = new Notification
                    {
                        UserId = log.Student.User.Id, 
                        Title = "Log Approved",       
                        Message = $"Your log for {log.Date:MMM dd} was approved.",
                        Type = "Success",
                        Url = "/Student/Dashboard",
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    };
                    _context.Notifications.Add(notification);
                }

                _context.Entry(log).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                TempData["Success"] = "✅ Log verified successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Database Error: " + ex.Message;
            }
    
            string referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer) && referer.Contains("Dashboard"))
            {
                return RedirectToAction("Dashboard");
            }
            return RedirectToAction("ProgressLogs");
        }

        // =========================
        // LIST STUDENTS (Performance Table)
        // =========================
        [HttpGet]
        public async Task<IActionResult> Students(string searchString, string cohortFilter, string trackFilter)
        {
            ViewBag.TotalStudents = await _context.Students.CountAsync();
    
                var query = _context.Students
                .Include(s => s.Track)
                .Include(s => s.Cohort)
                .Include(s => s.Mentor)
                .Include(s => s.ProgressLogs)
                .Include(s => s.ModuleCompletions)
                .AsQueryable();

            // 2. Apply Filters (If search is used)
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(s => s.FullName.Contains(searchString) || s.Email.Contains(searchString));
            }

            var students = await query.ToListAsync();
            var modelList = new List<StudentPerformanceViewModel>();

            var today = DateTime.UtcNow.Date;
            var last7Days = today.AddDays(-7);

            // 3. Get Total Modules per Track (Dictionary for speed)
            var trackModuleCounts = await _context.SyllabusModules
                .Where(m => m.IsActive)
                .GroupBy(m => m.TrackId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            // 4. Transform Data
            foreach (var s in students)
            {
                // Calculate recent stats
                var recentLogs = s.ProgressLogs.Where(l => l.Date >= last7Days && l.IsApproved).ToList();
                decimal hours7Days = recentLogs.Sum(l => l.Hours);
                int checkIns7Days = recentLogs.Select(l => l.Date.Date).Distinct().Count();

                // Module Progress
                int totalMods = trackModuleCounts.ContainsKey(s.TrackId) ? trackModuleCounts[s.TrackId] : 1;
                int completedMods = s.ModuleCompletions.Count(mc => mc.IsCompleted);

                // Calculate Consistency (Simple logic: If < 50% of target hours in last 4 weeks, score drops)
                // We'll use a simplified version here for the table
                int consistency = 0;
                if (s.TargetHoursPerWeek > 0)
                {
                    consistency = (int)((hours7Days / s.TargetHoursPerWeek) * 100);
                    if (consistency > 100) consistency = 100;
                }

                // Determine Status
                string status = "Active";
                string statusColor = "success";

                if (s.EnrollmentStatus == "Suspended") status = "Inactive";
                else if (consistency < 30) status = "At Risk"; // Low activity

                modelList.Add(new StudentPerformanceViewModel
                {
                    StudentId = s.Id,
                    FullName = s.FullName,
                    Email = s.Email,
                    ProfilePicture = s.ProfilePicture,
                    CohortName = s.Cohort?.Name ?? "N/A",
                    TrackCode = s.Track?.Code ?? "N/A",
                    MentorName = s.Mentor?.FullName ?? "Unassigned",
                    TargetHoursPerWeek = s.TargetHoursPerWeek,
                    HoursLast7Days = hours7Days,
                    CheckInsLast7Days = checkIns7Days,
                    CompletedModules = completedMods,
                    TotalModules = totalMods,
                    ConsistencyScore = consistency,
                    Status = status
                });
            }

            return View(modelList);
        }

        // =========================
        // CREATE STUDENT (ADMIN ONLY)
        // =========================
        [HttpGet]
        [Authorize(Roles = "Admin")] // 🔒 Strict Admin
        public async Task<IActionResult> CreateStudent()
        {
            await PopulateDropdowns();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")] // 🔒 Strict Admin
        public async Task<IActionResult> CreateStudent(Student model, IFormFile? profilePicture, string password)
        {
            ModelState.Remove("UserId");
            ModelState.Remove("CohortId");
            ModelState.Remove("User");
            ModelState.Remove("Cohort");
            ModelState.Remove("Track");

            if (!ModelState.IsValid)
            {
                await PopulateDropdowns();
                return View(model);
            }

            // 1. Generate Username
            var nameParts = model.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string surname = nameParts.Length > 0 ? nameParts[0] : "Student";
            string firstInitial = nameParts.Length > 1 ? nameParts[1].Substring(0, 1) : "X";
            int nextId = await _context.Students.CountAsync() + 1;
            string username = $"{surname}{firstInitial}{nextId:D3}";

            // 2. Create Identity User
            var user = new ApplicationUser
            {
                UserName = username,
                Email = model.Email,
                EmailConfirmed = true
            };
            string finalPassword = string.IsNullOrEmpty(password) ? "Student@123" : password;
            var result = await _userManager.CreateAsync(user, finalPassword);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
                await PopulateDropdowns();
                return View(model);
            }

            await _userManager.AddToRoleAsync(user, "Student");

            // 3. Auto-Generate Cohort
            var track = await _context.Tracks.FindAsync(model.TrackId);
            string cohortName = $"{track.Code}{model.DateJoined:MMyy}";
            var cohort = await _context.Cohorts.FirstOrDefaultAsync(c => c.Name == cohortName);

            if (cohort == null)
            {
                cohort = new Cohort
                {
                    Name = cohortName,
                    StartDate = model.DateJoined,
                    EndDate = model.DateJoined.AddMonths(6),
                    IsActive = true
                };
                _context.Cohorts.Add(cohort);
                await _context.SaveChangesAsync();
            }

            // 4. Handle Profile Picture
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

            // 5. Save Student
            model.UserId = user.Id;
            model.CohortId = cohort.Id;
            model.TargetHoursPerWeek = 25;
            model.CreatedAt = DateTime.UtcNow;
            model.UpdatedAt = DateTime.UtcNow;

            _context.Students.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ Student Created! Username: {username} | Password: {finalPassword}";
            return RedirectToAction(nameof(Students));
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
            TempData["Success"] = "✅ Student updated successfully!";
            return RedirectToAction(nameof(Details), new { id = model.Id });
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
            await _auditService.LogAsync("Delete Student", $"Deleted student: {student.FullName} (ID: {id})", User.Identity.Name);

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
        public async Task<IActionResult> CreateMentor(Mentor model, string email, string password, string username)
        {
            ModelState.Remove("User");
            ModelState.Remove("UserId");
            ModelState.Remove("Track");

            if (!ModelState.IsValid)
            {
                ViewBag.Tracks = new SelectList(await _context.Tracks.ToListAsync(), "Id", "Name");
                return View(model);
            }

            var user = new ApplicationUser { UserName = username, Email = email, EmailConfirmed = true };
            var result = await _userManager.CreateAsync(user, password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
                ViewBag.Tracks = new SelectList(await _context.Tracks.ToListAsync(), "Id", "Name");
                return View(model);
            }

            await _userManager.AddToRoleAsync(user, "Mentor");

            model.UserId = user.Id;
            _context.Mentors.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ Mentor Created! Login: {username}";
            return RedirectToAction("Dashboard");
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
                .ToListAsync();

            return View(mentors);
        }

        // =========================
        // ANNOUNCEMENTS
        // =========================
        [HttpGet]
        [Authorize(Roles = "Admin, Mentor")] // Usually only admins broadcast, but change to "Admin,Mentor" if needed
        public IActionResult CreateAnnouncement()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Mentor")]
        public async Task<IActionResult> CreateAnnouncement(Announcement model)
        {
            if (ModelState.IsValid)
            {
                model.PostedBy = User.Identity?.Name ?? "Admin";
                model.CreatedAt = DateTime.UtcNow;
                _context.Announcements.Add(model);
                await _context.SaveChangesAsync();

                // 🔔 Trigger Notifications Logic
                var users = new List<ApplicationUser>();
                if (model.Audience == "Students" || model.Audience == "All")
                    users.AddRange(await _userManager.GetUsersInRoleAsync("Student"));
                if (model.Audience == "Mentors" || model.Audience == "All")
                    users.AddRange(await _userManager.GetUsersInRoleAsync("Mentor"));

                foreach (var user in users.DistinctBy(u => u.Id))
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = user.Id,
                        Title = "New Announcement", // 👈 THIS WAS MISSING
                        Message = $"📢 {model.Title}: {model.Message}",
                        Type = "Info",
                        IsRead = false,
                        Url = "/Notification/Index",
                        CreatedAt = DateTime.UtcNow
                    });
                }
                await _context.SaveChangesAsync();

                TempData["Success"] = "📢 Announcement sent!";
                return RedirectToAction("Dashboard");
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

            // 1. 🔍 FILTER: By Specific Student (clicked from Dashboard)
            if (studentId.HasValue)
            {
                query = query.Where(l => l.StudentId == studentId.Value);

                // Fetch student name for the view title
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
        public async Task<IActionResult> Details(int id)
        {
            var student = await _context.Students
                .Include(s => s.Track)
                .Include(s => s.Cohort)
                .Include(s => s.Mentor)
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (student == null) return NotFound();

            return View(student);
        }

        // =========================
        // HELPERS
        // =========================
        private async Task PopulateDropdowns()
        {
            var tracks = await _context.Tracks.Where(t => t.IsActive).ToListAsync();
            ViewBag.TrackList = tracks; // Full list for JS
            ViewBag.Tracks = new SelectList(tracks, "Id", "Name");
            ViewBag.Cohorts = new SelectList(await _context.Cohorts.Where(c => c.IsActive).ToListAsync(), "Id", "Name");
            ViewBag.Mentors = new SelectList(await _context.Mentors.ToListAsync(), "Id", "FullName");
        }

        [HttpGet]
        public async Task<IActionResult> GetNextStudentId()
        {
            int nextId = await _context.Students.CountAsync() + 1;
            return Json(nextId);
        }

        // =========================
        // SUPPORT TICKET MANAGEMENT
        // =========================
        [HttpGet]
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
                    Url = "/Support/Index",
                    CreatedAt = DateTime.UtcNow
                };
                _context.Notifications.Add(notification);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(SupportTickets));
        }
        // =========================
        // LIBRARY MANAGEMENT
        // =========================
        [HttpGet]
        public async Task<IActionResult> ManageLibrary()
        {
            var resources = await _context.Resources
                .Include(r => r.Track)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
            return View(resources);
        }

        [HttpGet]
        public async Task<IActionResult> CreateResource()
        {
            ViewBag.Tracks = new SelectList(await _context.Tracks.ToListAsync(), "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateResource(Resource model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.UtcNow;
                _context.Resources.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Resource added successfully!";
                return RedirectToAction(nameof(ManageLibrary));
            }
            ViewBag.Tracks = new SelectList(await _context.Tracks.ToListAsync(), "Id", "Name");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteResource(int id)
        {
            var resource = await _context.Resources.FindAsync(id);
            if (resource != null)
            {
                _context.Resources.Remove(resource);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Resource deleted.";
            }
            return RedirectToAction(nameof(ManageLibrary));
        }
        // =========================
        // GET: MANAGE QUIZZES
        // =========================
        public async Task<IActionResult> ManageQuizzes(int? trackId)
        {
            // 1. Start Query for Modules
            var query = _context.SyllabusModules
                .Include(m => m.Track)
                .AsQueryable();

            // 2. Filter by Track if selected
            if (trackId.HasValue)
            {
                query = query.Where(m => m.TrackId == trackId.Value);
            }

            // 3. Execute Query
            var modules = await query
                .OrderBy(m => m.Track.Name)
                .ThenBy(m => m.DisplayOrder)
                .ToListAsync();

            // 4. Populate Dropdown for filtering
            ViewBag.Tracks = await _context.Tracks.ToListAsync();
            ViewBag.SelectedTrackId = trackId;

            return View(modules); // 👈 We are sending "List<SyllabusModule>"
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


        // =========================
        // SYSTEM AUDIT LOGS
        // =========================
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AuditLogs()
        {
            var logs = await _context.AuditLogs
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();

            return View(logs);
        }


    }
}