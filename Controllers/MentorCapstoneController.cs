using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;

namespace SPT.Controllers
{
    [Authorize(Roles = "Admin,Mentor")]
    public class MentorCapstoneController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MentorCapstoneController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            IQueryable<Capstone> query = _context.Capstones
                .Include(c => c.Student)
                    .ThenInclude(s => s.Track);

            if (User.IsInRole("Mentor"))
            {
                var mentor = await _context.Mentors
                    .FirstOrDefaultAsync(m => m.UserId == user.Id);

                if (mentor != null)
                    query = query.Where(c => c.Student.MentorId == mentor.Id);
            }
            // Admin sees all — no filter

            var capstones = await query
                .OrderByDescending(c => c.SubmittedAt)
                .ToListAsync();

            return View(capstones);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(int id, string status, string feedback)
        {
            var capstone = await _context.Capstones
                .Include(c => c.Student)
                    .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (capstone == null) return NotFound();

            // Mentor can only review their own students
            if (User.IsInRole("Mentor"))
            {
                var user = await _userManager.GetUserAsync(User);
                var mentor = await _context.Mentors.FirstOrDefaultAsync(m => m.UserId == user.Id);
                if (mentor == null || capstone.Student.MentorId != mentor.Id)
                    return Forbid();
            }

            capstone.Status = Enum.Parse<CapstoneStatus>(status);
            capstone.MentorFeedback = feedback;

            if (capstone.Student?.User != null)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = capstone.Student.User.Id,
                    Title = "Capstone Review",
                    Message = $"Your capstone '{capstone.Title}' has been marked as {status}.",
                    Type = status == "Approved" ? "Success" : "Warning",
                    Url = "/Capstone/Index",
                    TargetPage = "Dashboard",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                });
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Capstone marked as {status}.";
            return RedirectToAction(nameof(Index));
        }
    }
}