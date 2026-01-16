using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;

namespace SPT.Controllers
{
    [Authorize]
    public class LibraryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public LibraryController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            // 1. ADMIN CHECK: If Admin clicks "Library", send them to the Management page
            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return RedirectToAction("ManageLibrary", "Admin");
            }

            // 2. STUDENT CHECK: Get student profile to find their Track
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (student == null)
            {
                return RedirectToAction("Dashboard", "Student");
            }

            // 3. FETCH RESOURCES: Filter by the Student's TrackId
            // This replaces your old "ModuleResources" logic with the new "Resource" table
            var resources = await _context.Resources
                .Where(r => r.TrackId == student.TrackId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(resources);
        }
    }
}