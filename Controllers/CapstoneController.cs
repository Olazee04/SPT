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

        public CapstoneController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            AuditService auditService)
        {
            _context = context;
            _userManager = userManager;
            _auditService = auditService;
        }

       
        [Authorize(Roles = "Student")]
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

           
            var miniProjectModule = await _context.SyllabusModules
                .FirstOrDefaultAsync(m => m.TrackId == student.TrackId && m.IsMiniProject);

            if (miniProjectModule == null)
                return RedirectToAction("Locked");

            bool projectUnlocked = student.ModuleCompletions
                .Any(mc => mc.ModuleId == miniProjectModule.Id && mc.IsCompleted);

            if (!projectUnlocked)
                return RedirectToAction("Locked");

            return View();
        }

       
        [Authorize(Roles = "Student")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(Capstone model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (student == null) return NotFound();

            var existing = await _context.Capstones
                .FirstOrDefaultAsync(c => c.StudentId == student.Id);

            if (existing != null)
            {
                
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

            await _auditService.LogAsync(
                "CAPSTONE_SUBMITTED",
                $"Capstone submitted by {student.FullName}",
                user.UserName ?? "Student",
                user.Id);

            TempData["Success"] = "Capstone submitted successfully! Your mentor will review it shortly.";
            return RedirectToAction(nameof(Index));
        }

      
        [Authorize(Roles = "Student")]
        public IActionResult Locked()
        {
            return View();
        }
    }
}