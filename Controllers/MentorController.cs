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
        private readonly IEmailService _emailService;

        public MentorController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env, AuditService auditService,
             IEmailService emailService) 
        {
            _context = context;
            _userManager = userManager;
            _env = env;
            _auditService = auditService;
            _emailService = emailService; 
        }

        // =========================
        // MENTOR DASHBOARD
        // =========================
        public async Task<IActionResult> Dashboard()
        {
            var mentor = await GetCurrentMentorAsync();
            if (mentor == null) return View("Error");

            IQueryable<Student> studentQuery;
            if (mentor.Specialization == "General" || mentor.TrackId == null)
                studentQuery = _context.Students;
            else
                studentQuery = _context.Students
                    .Where(s => s.TrackId == mentor.TrackId || s.MentorId == mentor.Id);

            var students = await studentQuery
                .Include(s => s.User)
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
                .Where(l => !l.IsApproved && studentQuery.Select(s => s.Id).Contains(l.StudentId))
                .CountAsync();

            model.OpenTickets = await _context.SupportTickets
                .Where(t => !t.IsResolved && studentQuery.Select(s => s.Id).Contains(t.StudentId))
                .CountAsync();

            var performance = new List<StudentPerformanceDTO>();
            foreach (var s in students)
            {
                var recent = s.ProgressLogs.Where(l => l.IsApproved && l.Date >= sevenDaysAgo).ToList();
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
            model.AvgConsistency = performance.Any() ? (decimal)performance.Average(p => p.ConsistencyScore) : 0;

            model.TrackLabels = performance.GroupBy(p => p.Track).Select(g => g.Key).ToArray();
            model.TrackCounts = performance.GroupBy(p => p.Track).Select(g => g.Count()).ToArray();
            model.ActivityDates = new string[7];
            model.ActivityCounts = new int[7];

            for (int i = 0; i < 7; i++)
            {
                var d = sevenDaysAgo.AddDays(i);
                model.ActivityDates[i] = d.ToString("MMM dd");
                model.ActivityCounts[i] = await _context.ProgressLogs
                    .Where(l => l.Date.Date == d && studentQuery.Select(s => s.Id).Contains(l.StudentId))
                    .CountAsync();
            }

            return View(model);
        }

        // =========================
        // HELPER: GET CURRENT MENTOR
        // =========================
        private async Task<Mentor?> GetCurrentMentorAsync()
        {
            var userId = _userManager.GetUserId(User);
            return await _context.Mentors
                .Include(m => m.User)
                .Include(m => m.Track)   // <-- THIS was missing, causing Track to always be null
                .FirstOrDefaultAsync(m => m.UserId == userId);
        }

        // =========================
        // MY STUDENTS LIST
        [HttpGet]
        public async Task<IActionResult> Students()
        {
            var mentor = await GetCurrentMentorAsync();
            if (mentor == null) return RedirectToAction(nameof(Dashboard));

            IQueryable<Student> studentQuery;
            if (mentor.Specialization == "General" || mentor.TrackId == null)
                studentQuery = _context.Students;
            else
                studentQuery = _context.Students
                    .Where(s => s.TrackId == mentor.TrackId || s.MentorId == mentor.Id);

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

            // ✅ FIX: Week starts on MONDAY
            var today = DateTime.UtcNow.Date;
            int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
            var startOfWeek = today.AddDays(-daysSinceMonday);

            foreach (var s in students)
            {
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
                    HoursLast7Days = hoursThisWeek,
                    CheckInsLast7Days = checkInsThisWeek,
                    CompletedModules = completedMods,
                    TotalModules = totalMods,
                    ConsistencyScore = consistency,
                    Status = s.EnrollmentStatus
                });
            }

            var pendingLogs = await _context.ProgressLogs
                .Include(l => l.Student)
                .Include(l => l.Module)
                .Where(l => !l.IsApproved && studentQuery.Select(s => s.Id).Contains(l.StudentId))
                .OrderByDescending(l => l.Date)
                .Take(10)
                .ToListAsync();

            int pendingCount = pendingLogs.Count;
            int avgConsistency = modelList.Count == 0 ? 0 : (int)modelList.Average(x => x.ConsistencyScore);

            ViewBag.MyStudentsCount = students.Count;
            ViewBag.PendingLogs = pendingCount;
            ViewBag.AvgConsistency = avgConsistency;
            ViewBag.PendingLogsList = pendingLogs;

            return View(modelList);
        }


        // =========================
        // MENTOR: PROGRESS LOGS
        // =========================
        [HttpGet]
        public async Task<IActionResult> ProgressLogs(string status = "All", string search = "")
        {
            var mentor = await GetCurrentMentorAsync();
            if (mentor == null) return View("Error");

            IQueryable<Student> studentScope;
            if (mentor.Specialization == "General" || mentor.TrackId == null)
                studentScope = _context.Students;
            else
                studentScope = _context.Students.Where(s => s.MentorId == mentor.Id || s.TrackId == mentor.TrackId);

            var studentIds = await studentScope.Select(s => s.Id).ToListAsync();

            var query = _context.ProgressLogs
                .Include(l => l.Student).ThenInclude(s => s.Track)
                .Include(l => l.Module)
                .Where(l => studentIds.Contains(l.StudentId))
                .AsQueryable();

            if (status == "Pending") query = query.Where(l => !l.IsApproved);
            else if (status == "Approved") query = query.Where(l => l.IsApproved);
            if (!string.IsNullOrEmpty(search)) query = query.Where(l => l.Student.FullName.Contains(search));

            ViewBag.CurrentStatus = status;
            ViewBag.CurrentSearch = search;

            var logs = await query.OrderByDescending(l => l.Date).ToListAsync();
            return View("~/Views/Admin/ProgressLogs.cshtml", logs);
        }

        // =========================
        // MENTOR: UPDATE LOG
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateLog(int id, decimal? hours, string? description,
            int? mentorRating, int? quizScore, string? action)
        {
            var mentor = await GetCurrentMentorAsync();
            if (mentor == null) return Forbid();

            var log = await _context.ProgressLogs
                .Include(l => l.Student).ThenInclude(s => s.User)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (log == null) return NotFound();

            bool canAccess = mentor.Specialization == "General"
    || mentor.TrackId == null
    || log.Student.MentorId == mentor.Id
    || log.Student.TrackId == mentor.TrackId;

            if (!canAccess) return Forbid();

            if (action == "Reject")
            {
                log.IsApproved = false;
                log.IsRejected = true;
                log.RejectionReason = "Did not meet requirements";
                log.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["Error"] = "❌ Log rejected.";
                return RedirectToAction(nameof(ProgressLogs));
            }

            var dateToCheck = log.Date.Date;
            decimal otherLogsTotal = await _context.ProgressLogs
                .Where(l => l.StudentId == log.StudentId && l.Date.Date == dateToCheck && l.Id != id)
                .SumAsync(l => l.Hours);

            decimal proposedHours = hours ?? log.Hours;
            if ((otherLogsTotal + proposedHours) > 5)
            {
                TempData["Error"] = $"⚠️ Limit Exceeded! {otherLogsTotal} hrs already logged. Max 5/day.";
                return RedirectToAction(nameof(ProgressLogs));
            }

            log.Hours = proposedHours;
            if (!string.IsNullOrEmpty(description)) log.ActivityDescription = description;
            if (mentorRating.HasValue) log.MentorRating = mentorRating;
            if (quizScore.HasValue && log.PracticeDone) log.QuizScore = quizScore;

            var mentorResponse = Request.Form["mentorResponse"].ToString();
            if (!string.IsNullOrWhiteSpace(mentorResponse)) log.MentorResponse = mentorResponse;

            log.IsApproved = true;
            log.UpdatedAt = DateTime.UtcNow;
            log.VerifiedByUserId = _userManager.GetUserId(User);

            if (log.Student?.User != null)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = log.Student.User.Id,
                    Title = "Log Approved",
                    Message = $"Your log for {log.Date:MMM dd} was approved by your mentor.",
                    Type = "Success",
                    Url = "/Student/Dashboard",
                    TargetPage = "Dashboard",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                });
            }

            await _context.SaveChangesAsync();
            await _auditService.LogAsync("LOG_APPROVED_MENTOR",
                $"Mentor approved log #{log.Id}", User.Identity.Name, _userManager.GetUserId(User));

            TempData["Success"] = "✅ Log approved.";
            return RedirectToAction(nameof(ProgressLogs));
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

            bool isGeneral = mentor?.Specialization == "General" || mentor?.TrackId == null;
            bool isAssigned = log.Student.MentorId == mentor?.Id || log.Student.TrackId == mentor?.TrackId;
            if (!isGeneral && !isAssigned) return Forbid();

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
        // QUIZ SCORES — FIXED
        // =========================
        [HttpGet]
        public async Task<IActionResult> QuizScores(string search = "", int page = 1)
        {
            int pageSize = 15;

            var mentor = await GetCurrentMentorAsync();
            if (mentor == null) return View("Error");

            IQueryable<Student> studentScope;
            if (mentor.Specialization == "General" || mentor.TrackId == null)
                studentScope = _context.Students;
            else
                studentScope = _context.Students
                    .Where(s => s.TrackId == mentor.TrackId || s.MentorId == mentor.Id);

            var studentIds = await studentScope.Select(s => s.Id).ToListAsync();

            var query = _context.ProgressLogs
                .Include(l => l.Student).ThenInclude(s => s.Track)
                .Include(l => l.Module)
                .Where(l => l.QuizScore.HasValue && studentIds.Contains(l.StudentId))
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(l => l.Student.FullName.Contains(search));

            int total = await query.CountAsync();

            ViewBag.Search = search;
            ViewBag.MentorName = mentor.FullName;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);

            var logs = await query
                .OrderByDescending(l => l.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View(logs);
        }

        // =========================
        // MODULE QUIZ SCORES — Mentor (QuizAttempts table)
        // =========================
        [HttpGet]
        public async Task<IActionResult> ModuleQuizScores(string search = "", int page = 1)
        {
            int pageSize = 15;

            var mentor = await GetCurrentMentorAsync();
            if (mentor == null) return View("Error");

            IQueryable<Student> studentScope;
            if (mentor.Specialization == "General" || mentor.TrackId == null)
                studentScope = _context.Students;
            else
                studentScope = _context.Students
                    .Where(s => s.TrackId == mentor.TrackId || s.MentorId == mentor.Id);

            var studentIds = await studentScope.Select(s => s.Id).ToListAsync();

            var query = _context.QuizAttempts
                .Include(a => a.Student).ThenInclude(s => s.Track)
                .Include(a => a.Module)
                .Where(a => studentIds.Contains(a.StudentId))
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(a => a.Student.FullName.Contains(search));

            int total = await query.CountAsync();

            var data = await query
                .OrderByDescending(a => a.AttemptedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);

            return View(data);
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
        [Authorize(Roles = "Admin,Mentor")]
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
            if (mentor == null) return RedirectToAction("Dashboard");

            IQueryable<Student> studentScope;
            if (mentor.Specialization == "General" || mentor.TrackId == null)
                studentScope = _context.Students;
            else
                studentScope = _context.Students
                    .Where(s => s.TrackId == mentor.TrackId || s.MentorId == mentor.Id);

            var students = await studentScope.OrderBy(s => s.FullName).ToListAsync();

            var otherMentors = await _context.Mentors
                .Include(m => m.User)
                .Where(m => m.Id != mentor.Id)
                .OrderBy(m => m.FullName)
                .ToListAsync();

            ViewBag.OtherMentors = otherMentors;
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
            if (user == null) return NotFound();

            var mentor = await _context.Mentors
                .Include(m => m.User)
                .Include(m => m.Track)
                .FirstOrDefaultAsync(m => m.UserId == user.Id);

            if (mentor == null) return NotFound();

            // Handle profile picture update
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

                await _auditService.LogAsync("MENTOR_PROFILE_UPDATED", "Mentor updated profile picture",
                    User.Identity.Name, _userManager.GetUserId(User));

                TempData["Success"] = "Profile picture updated!";
                return RedirectToAction(nameof(Profile));
            }

            // Handle password change
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

                await _auditService.LogAsync("PASSWORD_CHANGED", "Mentor changed password",
                    User.Identity.Name, _userManager.GetUserId(User));

                // Send password change notification email
                try
                {
                    string emailBody = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;padding:20px;border:1px solid #ddd;border-radius:8px;'>
    <h2 style='color:#fd7e14;'>Password Changed &#128274;</h2>
    <p>Hi <strong>{mentor.FullName}</strong>,</p>
    <p>Your SPT Academy account password was successfully changed.</p>
    <p>If you made this change, no further action is needed.</p>
    <p style='color:#dc3545;'><strong>If you did NOT make this change, contact your admin immediately.</strong></p>
    <hr/>
    <p style='color:#6c757d;font-size:0.85rem;'>This is an automated security notification from RMSys SPT Academy.</p>
</div>";
                    await _emailService.SendEmailAsync(user.Email, "SPT Academy - Password Changed", emailBody);
                }
                catch { /* don't block if email fails */ }

                TempData["Success"] = "Password changed successfully! A confirmation email has been sent.";
                return RedirectToAction(nameof(Profile));
            }

            TempData["Error"] = "No changes were submitted.";
            return RedirectToAction(nameof(Profile));
        }
    }
}