using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;

namespace SPT.Controllers
{
    [Authorize(Roles = "Admin,Mentor")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IWebHostEnvironment env)
        {
            _userManager = userManager;
            _context = context;
            _env = env;
        }

        // =========================
        // DASHBOARD
        // =========================
        public async Task<IActionResult> Dashboard()
        {
            var totalStudents = await _context.Students.CountAsync();
            var activeStudents = await _context.Students.CountAsync(s => s.EnrollmentStatus == "Active");

            // ✅ NEW: Count logs that are NOT approved yet
            var pendingLogs = await _context.ProgressLogs.CountAsync(p => !p.IsApproved);

            ViewBag.TotalStudents = totalStudents;
            ViewBag.ActiveStudents = activeStudents;
            ViewBag.PendingLogs = pendingLogs; // Pass to View

            return View();
        }

        // =========================
        // LIST STUDENTS
        // =========================
        [HttpGet]
        public async Task<IActionResult> Students()
        {
            var students = await _context.Students
                .Include(s => s.Track)
                .Include(s => s.Cohort)
                .Include(s => s.Mentor)
                .Include(s => s.User)
                .OrderByDescending(s => s.DateJoined)
                .ToListAsync();

            return View(students);
        }

        // =========================
        // STUDENT DETAILS (New Feature)
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
        // CREATE STUDENT (GET)
        // =========================
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateStudent()
        {
            await PopulateDropdowns();
            return View();
        }

        // =========================
        // CREATE STUDENT (POST)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateStudent(Student model, IFormFile? profilePicture, string password)
        {
            // 1. Remove validation errors for fields we generate automatically
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

            // 2. Generate Username (Surname + FirstInitial + 3 Digit ID)
            var nameParts = model.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string surname = nameParts.Length > 0 ? nameParts[0] : "Student";
            string firstInitial = nameParts.Length > 1 ? nameParts[1].Substring(0, 1) : "X";

            int nextId = await _context.Students.CountAsync() + 1;
            string username = $"{surname}{firstInitial}{nextId:D3}"; // e.g. OwoiluZ003

            // 3. Create Identity User (Login)
            var user = new ApplicationUser
            {
                UserName = username,
                Email = model.Email,
                EmailConfirmed = true
            };

            // Use provided password or default
            string finalPassword = string.IsNullOrEmpty(password) ? "Student@123" : password;
            var result = await _userManager.CreateAsync(user, finalPassword);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
                await PopulateDropdowns();
                return View(model);
            }

            await _userManager.AddToRoleAsync(user, "Student");

            // 4. Auto-Generate or Find Cohort (TrackCode + MMYY)
            var track = await _context.Tracks.FindAsync(model.TrackId);
            string cohortName = $"{track.Code}{model.DateJoined:MMyy}"; // e.g. FS1224

            var cohort = await _context.Cohorts.FirstOrDefaultAsync(c => c.Name == cohortName);

            if (cohort == null)
            {
                // Create new cohort if it doesn't exist
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

            // 5. Handle Profile Picture
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

            // 6. Save Student
            model.UserId = user.Id;
            model.CohortId = cohort.Id;
            model.TargetHoursPerWeek = 25; // Force Constant
            model.CreatedAt = DateTime.UtcNow;
            model.UpdatedAt = DateTime.UtcNow;

            _context.Students.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ Student Created!<br><strong>Username:</strong> {username}<br><strong>Password:</strong> {finalPassword}<br><strong>Cohort:</strong> {cohortName}";
            return RedirectToAction(nameof(Students));
        }

        // =========================
        // HELPER: GET NEXT STUDENT ID (For JavaScript)
        // =========================
        [HttpGet]
        public async Task<IActionResult> GetNextStudentId()
        {
            // Count existing students and add 1
            int nextId = await _context.Students.CountAsync() + 1;
            return Json(nextId);
        }

        // =========================
        // HELPER: POPULATE DROPDOWNS
        // =========================
        private async Task PopulateDropdowns()
        {
            var tracks = await _context.Tracks.Where(t => t.IsActive).ToListAsync();

            // IMPORTANT: Pass the FULL LIST of tracks to ViewBag so the View can read the 'Code'
            ViewBag.TrackList = tracks;

            // Keep this for Edit screens if needed, but Create uses the TrackList above
            ViewBag.Tracks = new SelectList(tracks, "Id", "Name");

            ViewBag.Cohorts = new SelectList(
                await _context.Cohorts.Where(c => c.IsActive).ToListAsync(),
                "Id",
                "Name"
            );

            ViewBag.Mentors = new SelectList(
                await _context.Mentors.ToListAsync(),
                "Id",
                "FullName"
            );
        }

        // =========================
        // EDIT STUDENT (GET)
        // =========================
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditStudent(int id)
        {
            var student = await _context.Students
                .Include(s => s.Track)
                .Include(s => s.Cohort)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (student == null) return NotFound();

            await PopulateDropdowns();
            return View(student);
        }

        // =========================
        // EDIT STUDENT (POST)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditStudent(int id, Student model, IFormFile? profilePicture)
        {
            if (id != model.Id) return NotFound();

            // Ignore validation for automated fields during edit
            ModelState.Remove("User");
            ModelState.Remove("Cohort");
            ModelState.Remove("Track");

            if (!ModelState.IsValid)
            {
                await PopulateDropdowns();
                return View(model);
            }

            var existingStudent = await _context.Students.FirstOrDefaultAsync(s => s.Id == id);
            if (existingStudent == null) return NotFound();

            // Update fields
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
            existingStudent.EmergencyContactAddress = model.EmergencyContactAddress;
            existingStudent.UpdatedAt = DateTime.UtcNow;

            // Handle new profile picture
            if (profilePicture != null && profilePicture.Length > 0)
            {
                if (!string.IsNullOrEmpty(existingStudent.ProfilePicture))
                {
                    var oldPath = Path.Combine(_env.WebRootPath, existingStudent.ProfilePicture.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }

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
        // DELETE/DEACTIVATE STUDENT
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateStudent(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();

            student.EnrollmentStatus = "Suspended";
            student.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ Student deactivated successfully!";
            return RedirectToAction(nameof(Students));
        }

        // =========================
        // GET: Review Pending Logs
        // =========================
        [HttpGet]
        public async Task<IActionResult> ReviewLogs()
        {
            var pendingLogs = await _context.ProgressLogs
                .Include(p => p.Student)
                .Include(p => p.Module)
                .Where(p => !p.IsApproved) // Only show unapproved logs
                .OrderByDescending(p => p.Date)
                .ToListAsync();

            return View(pendingLogs);
        }

        // POST: Approve or Reject Log
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveLog(int id, decimal verifiedHours, int? quizScore, string action)
        {
            var log = await _context.ProgressLogs.FindAsync(id);
            if (log == null) return NotFound();

            if (action == "Approve")
            {
                log.Hours = verifiedHours;
                log.QuizScore = quizScore;
                log.IsApproved = true;

                // This line crashes if Migration wasn't run. 
                // We use a try-catch to be safe, or simply comment it out if you haven't migrated yet.
                try
                {
                    log.VerifiedByUserId = _userManager.GetUserId(User);
                }
                catch { /* Ignore if column missing for now */ }

                TempData["Success"] = "✅ Log verified and saved.";
            }
            else if (action == "Reject")
            {
                _context.ProgressLogs.Remove(log);
                TempData["Error"] = "❌ Log rejected.";
            }

            // CRITICAL: This actually commits the changes to the database
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ReviewLogs));
        }

        // =========================
        // CREATE MENTOR (GET)
        // =========================
        [HttpGet]
        [Authorize(Roles = "Admin")] 
        public async Task<IActionResult> CreateMentor()
        {
            ViewBag.Tracks = new SelectList(await _context.Tracks.ToListAsync(), "Id", "Name");
            return View();
        }

        // =========================
        // CREATE MENTOR (POST)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateMentor(Mentor model, string email, string password, string username)
        {
            // 1. Ignore Validation for Auto-Generated Fields
            ModelState.Remove("User");
            ModelState.Remove("UserId");
            ModelState.Remove("Track"); // <--- ADD THIS (Fixes the issue if Track is empty)

            if (!ModelState.IsValid)
            {
                // DEBUGGING: This puts the specific error into the Alert box so you can see it
                var errors = string.Join("; ", ModelState.Values
                                                .SelectMany(v => v.Errors)
                                                .Select(e => e.ErrorMessage));

                TempData["Error"] = $"Failed to create: {errors}"; // Show error on screen

                ViewBag.Tracks = new SelectList(await _context.Tracks.ToListAsync(), "Id", "Name");
                return View(model);
            }

            // 2. Create Login
            var user = new ApplicationUser
            {
                UserName = username,
                Email = email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
                ViewBag.Tracks = new SelectList(await _context.Tracks.ToListAsync(), "Id", "Name");
                return View(model);
            }

            await _userManager.AddToRoleAsync(user, "Mentor");

            // 3. Save Mentor Profile
            model.UserId = user.Id;
            _context.Mentors.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ Mentor Created! Login: <strong>{username}</strong>";
            return RedirectToAction("Dashboard");
        }
    }
}