using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;
using SPT.Models.ViewModels;

namespace SPT.Controllers
{
    // ✅ Allow Mentors to access Dashboard and Reviews, but restrict specific actions below
    [Authorize(Roles = "Admin,Mentor")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        // =========================
        // ADMIN & MENTOR DASHBOARD
        // =========================
        public async Task<IActionResult> Dashboard()
        {
            // 1. High-Level Stats
            ViewBag.TotalStudents = await _context.Students.CountAsync(s => s.EnrollmentStatus == "Active");
            ViewBag.TotalMentors = await _context.Mentors.CountAsync();

            // Critical Items
            ViewBag.PendingLogs = await _context.ProgressLogs.CountAsync(l => !l.IsApproved);
            ViewBag.OpenTickets = await _context.SupportTickets.CountAsync(t => !t.IsResolved);

            // 2. Fetch Recent Unapproved Logs (The "Inbox")
            var recentLogs = await _context.ProgressLogs
                   .Include(l => l.Student)
                   .Include(l => l.Module)
                   .Where(l => !l.IsApproved)
                   .OrderByDescending(l => l.Date)
                   .Take(5)
                   .ToListAsync();

            // ==========================================
            // 📊 CHART DATA PREPARATION
            // ==========================================

            // CHART 1: Students per Track
            var studentsPerTrack = await _context.Students
                .Include(s => s.Track)
                .Where(s => s.EnrollmentStatus == "Active")
                .GroupBy(s => s.Track.Code)
                .Select(g => new { Track = g.Key, Count = g.Count() })
                .ToListAsync();

            ViewBag.TrackLabels = studentsPerTrack.Select(x => x.Track).ToArray();
            ViewBag.TrackCounts = studentsPerTrack.Select(x => x.Count).ToArray();

            // CHART 2: Activity (Logs per Day - Last 7 Days)
            var sevenDaysAgo = DateTime.UtcNow.Date.AddDays(-6);
            var logsLast7Days = await _context.ProgressLogs
                .Where(l => l.Date >= sevenDaysAgo)
                .GroupBy(l => l.Date.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToListAsync();

            // Fill in missing days
            var dateLabels = new List<string>();
            var logCounts = new List<int>();

            for (int i = 0; i < 7; i++)
            {
                var date = sevenDaysAgo.AddDays(i);
                var record = logsLast7Days.FirstOrDefault(l => l.Date == date);
                dateLabels.Add(date.ToString("MMM dd"));
                logCounts.Add(record?.Count ?? 0);
            }

            ViewBag.DateLabels = dateLabels.ToArray();
            ViewBag.LogCounts = logCounts.ToArray();

            return View(recentLogs);
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
        // APPROVE LOG ACTION (Handles both Simple & Detailed)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveLog(int id, decimal? verifiedHours, int? quizScore, string? action)
        {
            var log = await _context.ProgressLogs.FindAsync(id);
            if (log == null) return NotFound();

            // Logic 1: Rejection
            if (action == "Reject")
            {
                _context.ProgressLogs.Remove(log);
                await _context.SaveChangesAsync();
                TempData["Error"] = "❌ Log rejected and removed.";
                return RedirectToAction(nameof(PendingLogs));
            }

            // Logic 2: Approval
            // If values are null (Simple Dashboard Click), use existing values
            log.Hours = verifiedHours ?? log.Hours;
            log.QuizScore = quizScore ?? log.QuizScore;
            log.IsApproved = true;
            log.UpdatedAt = DateTime.UtcNow;

            try
            {
                // Capture who verified it
                // Note: Ensure your ProgressLog model has VerifiedByUserId, or comment this out
                // log.VerifiedByUserId = _userManager.GetUserId(User); 
            }
            catch { }

            await _context.SaveChangesAsync();
            TempData["Success"] = "✅ Log verified successfully.";

            // Return to where we came from (Dashboard or List)
            string referer = Request.Headers["Referer"].ToString();
            if (referer.Contains("Dashboard")) return RedirectToAction(nameof(Dashboard));
            return RedirectToAction(nameof(PendingLogs));
        }

        // =========================
        // LIST STUDENTS (Performance Table)
        // =========================
        [HttpGet]
        public async Task<IActionResult> Students(string searchString, string cohortFilter, string trackFilter)
        {
            // 1. Start with Query
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
        [Authorize(Roles = "Admin")] // Usually only admins broadcast, but change to "Admin,Mentor" if needed
        public IActionResult CreateAnnouncement()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
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
        public async Task<IActionResult> ProgressLogs(string status = "All", string search = "")
        {
            var query = _context.ProgressLogs
                .Include(l => l.Student)
                .ThenInclude(s => s.Track)
                .Include(l => l.Module)
                .OrderByDescending(l => l.Date) // Newest first
                .AsQueryable();

            // 1. Filter by Status
            if (status == "Pending")
            {
                query = query.Where(l => !l.IsApproved);
            }
            else if (status == "Approved")
            {
                query = query.Where(l => l.IsApproved);
            }

            // 2. Filter by Student Name (Search)
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(l => l.Student.FullName.Contains(search));
            }

            // Pass current filter state to View for the UI buttons
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
        public async Task<IActionResult> ResolveTicket(int id, string response)
        {
            var ticket = await _context.SupportTickets.FindAsync(id);
            if (ticket == null) return NotFound();

            ticket.AdminResponse = response;
            ticket.IsResolved = true;
            ticket.Status = "Resolved"; // Ensure string status updates too
            ticket.ResolvedAt = DateTime.UtcNow; // Ensure you added this field to model, or remove if not using

            // Notify the student
            var notification = new Notification
            {
                UserId = ticket.Student.UserId, // Assuming you can access User via Student navigation
                Message = $"✅ Your ticket '{ticket.Subject}' has been resolved.",
                Type = "Success",
                IsRead = false,
                Url = "/Support/Index",
                CreatedAt = DateTime.UtcNow
            };
            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();
            TempData["Success"] = "Ticket resolved and student notified.";
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

    }
}