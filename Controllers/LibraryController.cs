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
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);

            // Get all modules with their resources
            // If Student: Filter by Track. If Admin: Show All.
            var modulesQuery = _context.SyllabusModules
                .Include(m => m.Track)
                .AsQueryable();

            if (User.IsInRole("Student") && student != null)
            {
                modulesQuery = modulesQuery.Where(m => m.TrackId == student.TrackId);
            }

            // Fetch resources linked to these modules
            var libraryData = await modulesQuery
                .OrderBy(m => m.DisplayOrder)
                .Select(m => new
                {
                    Module = m,
                    Resources = _context.ModuleResources.Where(r => r.ModuleId == m.Id && r.IsActive).ToList()
                })
                .ToListAsync();

            // We use ViewBag to pass this anonymous object list comfortably
            ViewBag.Library = libraryData;
            return View();
        }
    }
}