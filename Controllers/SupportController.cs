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

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Index", "Home");

            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (student == null)
                return RedirectToAction("Dashboard", "Student");

            var model = new SupportDashboardViewModel
            {
                Tickets = await _context.SupportTickets
                    .Where(t => t.StudentId == student.Id)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync(),

                Reflections = await _context.StudentReflections
                    .Where(r => r.StudentId == student.Id)
                    .OrderByDescending(r => r.Date)
                    .Take(5)
                    .ToListAsync()
            };

            return View(model);
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

            // Manually bind the student ID
            if (student != null)
            {
                // Clear validation for Student navigation property
                ModelState.Remove("Student");

                model.StudentId = student.Id;
                model.Status = "Open";
               
                model.CreatedAt = DateTime.UtcNow;

                _context.SupportTickets.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Ticket submitted successfully!";
            }
            else
            {
                TempData["Error"] = "Could not identify student account.";
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

            ModelState.Remove("Student"); // Prevent validation error on navigation prop

            if (student != null)
            {
                model.StudentId = student.Id;
                model.Date = DateTime.UtcNow;
                model.CreatedAt = DateTime.UtcNow;

                _context.StudentReflections.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Reflection saved. Good job being self-aware!";
            }
            else
            {
                TempData["Error"] = "Failed to save reflection.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
