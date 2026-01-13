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
        // GET: Show the Logging Form (With Barrier Logic)
        // =========================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            var student = await _context.Students
                .Include(s => s.Track)
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (student == null) return RedirectToAction("Dashboard", "Student");
            var assignmentModuleIds = await _context.SyllabusModules
                    .Where(m => m.TrackId == student.TrackId && m.HasProject)
                    .Select(m => m.Id)
                    .ToListAsync();

            ViewBag.AssignmentModules = assignmentModuleIds;

            var allModules = await _context.SyllabusModules
                .Where(m => m.TrackId == student.TrackId && m.IsActive)
                .OrderBy(m => m.DisplayOrder)
                .ToListAsync();

            var completedIds = await _context.ModuleCompletions
                .Where(mc => mc.StudentId == student.Id && mc.IsCompleted)
                .Select(mc => mc.ModuleId)
                .ToListAsync();

            
            var allowedModules = new List<SyllabusModule>();
            bool unlockNext = true;

            foreach (var module in allModules)
            {
                if (unlockNext)
                {
                    allowedModules.Add(module);

                    if (!completedIds.Contains(module.Id))
                    {
                        unlockNext = false;
                    }
                }
            }

            var dropdownList = allowedModules.Select(m => new
            {
                Id = m.Id,
                DisplayText = $"{m.ModuleCode}: {m.ModuleName} ({m.DifficultyLevel})"
            });

            ViewBag.ModuleId = new SelectList(dropdownList, "Id", "DisplayText");
            ViewBag.StudentName = student.FullName;

           
            ViewBag.UnlockProject = false;
            ViewBag.CurrentProgress = 0;

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

            ModelState.Remove("Student");
            ModelState.Remove("Module");
            ModelState.Remove("LoggedBy");
            ModelState.Remove("LoggedByUserId");

            var module = await _context.SyllabusModules.FindAsync(model.ModuleId);

            if (module != null && module.HasProject) // If module requires assignment
            {
                if (string.IsNullOrWhiteSpace(model.EvidenceLink))
                {
                    ModelState.AddModelError("EvidenceLink", "⚠️ You cannot submit this log without an Evidence Link because this module has a required assignment.");
                }
            }

            if (!ModelState.IsValid)
            {
                var modules = await _context.SyllabusModules
                    .Where(m => m.TrackId == student.TrackId && m.IsActive)
                    .OrderBy(m => m.DisplayOrder)
                    .ToListAsync();
                ViewBag.ModuleId = new SelectList(modules, "Id", "ModuleName");
                return View(model);
            }

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