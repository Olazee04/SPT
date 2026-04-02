// ==========================
//  MentorCapsoneController
// ==========================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;
using SPT.Services;

namespace SPT.Controllers
{
    [Authorize(Roles = "Mentor,Admin")]   
    public class MentorCapstoneController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuditService _auditService;

        public MentorCapstoneController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            AuditService auditService)
        {
            _context = context;
            _userManager = userManager;
            _auditService = auditService;
        }

        
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
        {
            var query = _context.Capstones
                .Include(c => c.Student).ThenInclude(s => s.Cohort)
                .Include(c => c.Student).ThenInclude(s => s.Track)
                .AsQueryable();

           
            if (!User.IsInRole("Admin"))
            {
                var userId = _userManager.GetUserId(User);
                var mentor = await _context.Mentors
                    .FirstOrDefaultAsync(m => m.UserId == userId);

                if (mentor != null && mentor.Specialization != "General" && mentor.TrackId.HasValue)
                {
                    query = query.Where(c =>
                        c.Student.MentorId == mentor.Id ||
                        c.Student.TrackId == mentor.TrackId);
                }
            }

            int total = await query.CountAsync();

            var capstones = await query
                .OrderByDescending(c => c.SubmittedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);

            return View(capstones);
        }

      
        [HttpGet]
        public async Task<IActionResult> Review(int id)
        {
            var capstone = await _context.Capstones
                .Include(c => c.Student).ThenInclude(s => s.Cohort)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (capstone == null) return NotFound();

            return View(capstone);
        }

      
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(int id, string status, string mentorFeedback)
        {
            var capstone = await _context.Capstones
                .Include(c => c.Student).ThenInclude(s => s.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (capstone == null) return NotFound();

            capstone.Status = Enum.Parse<CapstoneStatus>(status);
            capstone.MentorFeedback = mentorFeedback;
            capstone.ReviewedAt = DateTime.UtcNow;

          
            if (capstone.Student?.User != null)
            {
                string statusMsg = capstone.Status == CapstoneStatus.Approved
                    ? "✅ Your capstone has been approved! You can now receive your certificate."
                    : "❌ Your capstone was rejected. Please review the feedback and resubmit.";

                _context.Notifications.Add(new Notification
                {
                    UserId = capstone.Student.User.Id,
                    Title = $"Capstone {capstone.Status}",
                    Message = statusMsg,
                    Type = capstone.Status == CapstoneStatus.Approved ? "Success" : "Danger",
                    Url = "/Capstone/Index",
                    TargetPage = "Capstone",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                });
            }

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                "CAPSTONE_REVIEWED",
                $"Capstone #{id} for {capstone.Student?.FullName} marked as {status}",
                User.Identity?.Name ?? "System",
                _userManager.GetUserId(User));

            TempData["Success"] = $"Capstone {status.ToLower()} successfully!";
            return RedirectToAction(nameof(Index));
        }
    }
}