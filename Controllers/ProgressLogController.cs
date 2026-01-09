using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;

namespace SPT.Controllers
{
    [Authorize(Roles = "Student")]
    public class ProgressLogController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProgressLogController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // =========================
        // GET: Show the Logging Form
        // =========================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            var student = await _context.Students
                .Include(s => s.Track)
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (student == null) return RedirectToAction("Dashboard", "Student");

            // 1. Fetch Modules for Dropdown
            var modules = await _context.SyllabusModules
                .Where(m => m.TrackId == student.TrackId && m.IsActive)
                .OrderBy(m => m.DisplayOrder)
                .ToListAsync();

            ViewBag.ModuleId = new SelectList(modules, "Id", "ModuleName");
            ViewBag.StudentName = student.FullName;

            // 2. CALCULATE COMPLETION % (The "Gatekeeper")
            // Count total modules in their track
            int totalModules = await _context.SyllabusModules
                .CountAsync(m => m.TrackId == student.TrackId && m.IsActive);

            // Count how many this student has completed
            int completedModules = await _context.ModuleCompletions
                .CountAsync(m => m.StudentId == student.Id && m.IsCompleted);

            // Calculate percentage (Protect against divide by zero)
            double percentage = totalModules == 0 ? 0 : ((double)completedModules / totalModules) * 100;

            // 3. Set the Flag: Only unlock if > 75% done
            ViewBag.UnlockProject = percentage >= 75;
            ViewBag.CurrentProgress = Math.Round(percentage, 1); // Pass the number so we can show them

            return View();
        }

        // =========================
        // POST: Save the Log
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProgressLog model)
        {
            var user = await _userManager.GetUserAsync(User);
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (student == null) return RedirectToAction("Login", "Account");

            // 1. Ignore fields the student doesn't fill manually
            ModelState.Remove("Student");
            ModelState.Remove("Module");
            ModelState.Remove("LoggedBy");
            ModelState.Remove("LoggedByUserId");

            if (!ModelState.IsValid)
            {
                // Reload dropdown if validation fails
                var modules = await _context.SyllabusModules
                    .Where(m => m.TrackId == student.TrackId && m.IsActive)
                    .OrderBy(m => m.DisplayOrder)
                    .ToListAsync();
                ViewBag.ModuleId = new SelectList(modules, "Id", "ModuleName");
                return View(model);
            }

            // 2. Set Automated Fields
            model.StudentId = student.Id;
            model.LoggedBy = "Student";
            model.LoggedByUserId = user.Id;
            model.CreatedAt = DateTime.UtcNow;
            model.UpdatedAt = DateTime.UtcNow;
            model.IsApproved = false; // Admin must verify later (optional rule)

            _context.ProgressLogs.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ Progress Logged Successfully!";
            return RedirectToAction("Dashboard", "Student");
        }
    }
}