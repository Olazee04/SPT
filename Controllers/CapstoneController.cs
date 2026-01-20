using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;

namespace SPT.Controllers
{
    [Authorize]
    public class CapstoneController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CapstoneController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // STUDENT: Submit Page
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);

            // Allow mentors to see list, students to see their own
            if (User.IsInRole("Mentor") || User.IsInRole("Admin"))
            {
                var all = await _context.Capstones.Include(c => c.Student).ToListAsync();
                return View("MentorIndex", all);
            }

            var myProject = await _context.Capstones.FirstOrDefaultAsync(c => c.StudentId == student.Id);
            return View(myProject);
        }

        [HttpPost]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Submit(Capstone model)
        {
            var user = await _userManager.GetUserAsync(User);
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);

            model.StudentId = student.Id;
            model.Status = "Submitted";
            model.SubmittedAt = DateTime.UtcNow;

            _context.Capstones.Add(model);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        // MENTOR: Review Action
        [HttpPost]
        [Authorize(Roles = "Mentor,Admin")]
        public async Task<IActionResult> Review(int id, string action, string feedback)
        {
            var project = await _context.Capstones.FindAsync(id);
            if (project == null) return NotFound();

            if (action == "Approve")
            {
                project.ApprovalCount++;
                project.MentorFeedback = feedback;

                // Logic: Needs 2 approvals? Or just 1 for now?
                if (project.ApprovalCount >= 1)
                {
                    project.Status = "Approved";
                }
                else
                {
                    project.Status = "1/2 Approvals";
                }
            }
            else if (action == "RequestChanges")
            {
                project.Status = "Changes Requested";
                project.MentorFeedback = feedback;
                project.ApprovalCount = 0; // Reset
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }
    }
}