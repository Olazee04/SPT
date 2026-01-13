using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;

namespace SPT.Controllers
{
    [Authorize(Roles = "Student")]
    public class SupportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SupportController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // =========================
        // 1. INDEX (Dashboard for Support)
        // =========================
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);
            if (student == null) return RedirectToAction("Dashboard", "Student");

            var tickets = await _context.SupportTickets
                .Where(t => t.StudentId == student.Id)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var recentReflections = await _context.StudentReflections
                .Where(r => r.StudentId == student.Id)
                .OrderByDescending(r => r.Date)
                .Take(5)
                .ToListAsync();

            ViewBag.Tickets = tickets;
            ViewBag.Reflections = recentReflections;

            return View();
        }

        // =========================
        // 2. CREATE TICKET
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTicket(SupportTicket model)
        {
            var user = await _userManager.GetUserAsync(User);
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (ModelState.IsValid)
            {
                model.StudentId = student.Id;
                model.Status = "Open";
                model.CreatedAt = DateTime.UtcNow;

                _context.SupportTickets.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Ticket submitted! A mentor will review it soon.";
            }
            else
            {
                TempData["Error"] = "Please fill in all fields.";
            }
            return RedirectToAction(nameof(Index));
        }

        // =========================
        // 3. CREATE REFLECTION
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReflection(StudentReflection model)
        {
            var user = await _userManager.GetUserAsync(User);
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);

            // Remove validation for Student relation
            ModelState.Remove("Student");

            if (ModelState.IsValid)
            {
                model.StudentId = student.Id;
                model.Date = DateTime.UtcNow;

                _context.StudentReflections.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Reflection saved. Good job being self-aware!";
            }
            else
            {
                var errors = string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                TempData["Error"] = "Failed to save reflection: " + errors;
            }
            return RedirectToAction(nameof(Index));
        }
    }
}