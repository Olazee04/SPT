using Microsoft.AspNetCore.Authorization;
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

        public MentorCapstoneController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================
        // LIST ALL CAPSTONES
        // ============================
        public async Task<IActionResult> Index()
        {
            var capstones = await _context.Capstones
                .Include(c => c.Student)
                    .ThenInclude(s => s.Cohort)
                .OrderByDescending(c => c.SubmittedAt)
                .ToListAsync();

            return View(capstones);
        }
        // =========================
        // GET: REVIEW CAPSTONE
        // =========================
        [HttpGet]
        public async Task<IActionResult> Review(int id)
        {
            var capstone = await _context.Capstones
                .Include(c => c.Student)
                .ThenInclude(s => s.Cohort)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (capstone == null)
                return NotFound();

            return View(capstone);
        }

        // =========================
        // POST: SUBMIT REVIEW
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(
            int id,
            CapstoneStatus status,
            string mentorFeedback)
        {
            var capstone = await _context.Capstones.FindAsync(id);
            if (capstone == null)
                return NotFound();

            capstone.Status = status;
            capstone.MentorFeedback = mentorFeedback;
            capstone.ReviewedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Capstone review saved successfully.";
            return RedirectToAction(nameof(Index));
        }

    }
}
