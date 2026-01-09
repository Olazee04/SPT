using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;

namespace SPT.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public StudentController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // =========================
        // STUDENT DASHBOARD
        // =========================

        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);

            // SAFETY CHECK: If session is lost, force login
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var student = await _context.Students
                .Include(s => s.Track)
                .Include(s => s.Cohort)
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (student == null)
            {
                return View("ProfilePending");
            }

            return View(student);
        }

    }
}
