using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;
using SPT.Services;

namespace SPT.Controllers
{
    [Authorize(Roles = "Student")]
    public class SupportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuditService _auditService;
        private readonly EmailNotificationService _emailNotif;
        private readonly IConfiguration _config;

        public SupportController(
     ApplicationDbContext context,
     AuditService auditService,
     UserManager<ApplicationUser> userManager,
     EmailNotificationService emailNotif,
     IConfiguration config)
        {
            _context = context;
            _userManager = userManager;
            _auditService = auditService;
            _emailNotif = emailNotif;
            _config = config;
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
            string adminEmailAddr = _config["Email:User"] ?? "";
            string? mentorEmailForTicket = null;

            if (student.MentorId.HasValue)
            {
                var mentor = await _context.Mentors
                    .Include(m => m.User)
                    .FirstOrDefaultAsync(m => m.Id == student.MentorId);
                mentorEmailForTicket = mentor?.User?.Email;
            }

            await _emailNotif.SendSupportTicketCreatedAsync(
                student.FullName,
                model.Subject,
                model.Message,
                adminEmailAddr,
                mentorEmailForTicket);
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
