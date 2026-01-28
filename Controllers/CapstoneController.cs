using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;
using SPT.Services;

namespace SPT.Controllers
{
    [Authorize]
    public class CapstoneController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuditService _auditService;

        public CapstoneController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, AuditService auditService)
        {
            _context = context;
            _userManager = userManager;
            _auditService = auditService;
        }

        // STUDENT: Submit Page
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (student == null)
                return RedirectToAction("Dashboard", "Student");

            var myProject = await _context.Capstones
                .FirstOrDefaultAsync(c => c.StudentId == student.Id);

 
            return View(myProject);
        }

        [HttpPost]
        [Authorize(Roles = "Student")]
        [HttpPost]
        public async Task<IActionResult> Submit(Capstone model)
        {
            var user = await _userManager.GetUserAsync(User);
            var student = await _context.Students.FirstAsync(s => s.UserId == user.Id);

            var existing = await _context.Capstones
                .FirstOrDefaultAsync(c => c.StudentId == student.Id);

            if (existing != null)
            {
                // RESUBMISSION
                existing.Title = model.Title;
                existing.Description = model.Description;
                existing.RepositoryUrl = model.RepositoryUrl;
                existing.LiveDemoUrl = model.LiveDemoUrl;
                existing.Status = CapstoneStatus.Pending;
                existing.MentorFeedback = null;
                existing.SubmittedAt = DateTime.UtcNow;
            }
            else
            {
                model.StudentId = student.Id;
                model.Status = CapstoneStatus.Pending;
                _context.Capstones.Add(model);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Capstone submitted successfully!";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Student")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var student = await _context.Students
                .Include(s => s.ModuleCompletions)
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (student == null)
                return RedirectToAction("Dashboard", "Student");

            // 🔒 Gate by module 19 (Project Module)
            bool projectUnlocked = student.ModuleCompletions
                .Any(mc => mc.ModuleId == 19 && mc.IsCompleted);

            if (!projectUnlocked)
                return RedirectToAction("Locked");

            return View();
        }

    }



}