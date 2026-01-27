using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models.ViewModels;

namespace SPT.Controllers
{
    [Authorize]
    public class LeaderboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LeaderboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var model = new LeaderboardDashboardViewModel
            {
                CompletedModules = await CompletedModules(),
                ActiveThisWeek = await ActiveThisWeek(),
                ActiveToday = await ActiveToday(),
                ActiveThisMonth = await ActiveThisMonth(),
                Consistency = await Consistency(),
                TopPerCohort = await TopPerCohort()
            };

            return View(model);
        }

        // ---------- PRIVATE METHODS (NOT CONTROLLERS) ----------

        private async Task<List<LeaderboardRow>> CompletedModules()
        {
            return await _context.Students
                .Select(s => new LeaderboardRow
                {
                    FullName = s.FullName,
                    Cohort = s.Cohort!.Name,
                    Score = s.ModuleCompletions.Count(mc => mc.IsCompleted)
                })
                .OrderByDescending(x => x.Score)
                .Take(5)
                .ToListAsync();
        }

        private async Task<List<LeaderboardRow>> ActiveThisWeek()
        {
            var start = DateTime.UtcNow.Date.AddDays(-7);

            return await _context.ProgressLogs
                .Where(l => l.Date >= start)
                .GroupBy(l => l.Student)
                .Select(g => new LeaderboardRow
                {
                    FullName = g.Key.FullName,
                    Cohort = g.Key.Cohort!.Name,
                    Score = g.Count()
                })
                .OrderByDescending(x => x.Score)
                .Take(5)
                .ToListAsync();
        }

        private async Task<List<LeaderboardRow>> ActiveToday()
        {
            var today = DateTime.UtcNow.Date;

            return await _context.ProgressLogs
                .Where(l => l.Date.Date == today)
                .GroupBy(l => l.Student)
                .Select(g => new LeaderboardRow
                {
                    FullName = g.Key.FullName,
                    Cohort = g.Key.Cohort!.Name,
                    Score = g.Count()
                })
                .OrderByDescending(x => x.Score)
                .Take(5)
                .ToListAsync();
        }

        private async Task<List<LeaderboardRow>> ActiveThisMonth()
        {
            var start = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

            return await _context.ProgressLogs
                .Where(l => l.Date >= start)
                .GroupBy(l => l.Student)
                .Select(g => new LeaderboardRow
                {
                    FullName = g.Key.FullName,
                    Cohort = g.Key.Cohort!.Name,
                    Score = g.Count()
                })
                .OrderByDescending(x => x.Score)
                .Take(5)
                .ToListAsync();
        }

        private async Task<List<LeaderboardRow>> Consistency()
        {
            return await _context.Students
                .Select(s => new LeaderboardRow
                {
                    FullName = s.FullName,
                    Cohort = s.Cohort!.Name,
                    Score = s.ProgressLogs
                        .Select(l => l.Date.Date)
                        .Distinct()
                        .Count()
                })
                .OrderByDescending(x => x.Score)
                .Take(5)
                .ToListAsync();
        }

        private async Task<List<LeaderboardRow>> TopPerCohort()
        {
            return await _context.Students
                .GroupBy(s => s.Cohort!.Name)
                .Select(g => g
                    .OrderByDescending(s => s.ProgressLogs.Count)
                    .Select(s => new LeaderboardRow
                    {
                        FullName = s.FullName,
                        Cohort = g.Key,
                        Score = s.ProgressLogs.Count
                    })
                    .First())
                .ToListAsync();
        }
    }
}
