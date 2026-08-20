using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;

namespace SPT.Filters
{
    /// <summary>
    /// Runs before every action, for every controller. If the current user is
    /// a Student, it computes ViewBag.CapstoneUnlocked and ViewBag.CanViewCertificate
    /// once here — so the sidebar (_Layout.cshtml) shows the correct Capstone/
    /// Certificate links on EVERY student page (Curriculum, Leaderboard, Resources,
    /// etc.), not just on /Student/Dashboard.
    ///
    /// Previously this was only computed inside StudentController.Dashboard(),
    /// so the Capstone nav link only ever appeared on that one page. It also used
    /// a hardcoded ModuleId == 19 check (FEJ's capstone ID), so it was wrong for
    /// every other track. This filter fixes both issues in one place.
    ///
    /// If an action sets these ViewBag values itself AFTER this filter runs, its
    /// value wins (this only sets them before the action executes) — so remove
    /// any duplicate/hardcoded versions from individual actions to avoid them
    /// silently overriding the correct value computed here.
    /// </summary>
    public class StudentNavContextFilter : IAsyncActionFilter
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public StudentNavContextFilter(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context.HttpContext.User.Identity?.IsAuthenticated == true &&
                context.HttpContext.User.IsInRole("Student") &&
                context.Controller is Controller controller)
            {
                var user = await _userManager.GetUserAsync(context.HttpContext.User);
                if (user != null)
                {
                    var student = await _context.Students
                        .Include(s => s.ModuleCompletions)
                        .FirstOrDefaultAsync(s => s.UserId == user.Id);

                    if (student != null)
                    {
                        var totalModules = await _context.SyllabusModules
                            .CountAsync(m => m.TrackId == student.TrackId && m.IsActive);
                        var completedModules = student.ModuleCompletions.Count(mc => mc.IsCompleted);

                        // Use IsMiniProject lookup, NOT a hardcoded ModuleId — the
                        // capstone module's actual Id differs per track.
                        var miniProjectModule = await _context.SyllabusModules
                            .FirstOrDefaultAsync(m => m.TrackId == student.TrackId && m.IsMiniProject);

                        bool capstoneUnlocked = miniProjectModule != null &&
                            student.ModuleCompletions.Any(mc => mc.ModuleId == miniProjectModule.Id && mc.IsCompleted);

                        controller.ViewBag.CapstoneUnlocked = capstoneUnlocked;
                        controller.ViewBag.CanViewCertificate = completedModules == totalModules && totalModules > 0;
                    }
                }
            }

            await next();
        }
    }
}