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
            var today = DateTime.UtcNow.Date;
            int daysSinceMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var weekStart = today.AddDays(-daysSinceMonday);
            var monthStart = new DateTime(today.Year, today.Month, 1);

            var students = await _context.Students
                .Include(s => s.Track)
                .Include(s => s.Cohort)
                .Include(s => s.ModuleCompletions)
                .Include(s => s.ProgressLogs)
                .ToListAsync();

            var tracks = await _context.Tracks.ToListAsync();

            // ── 1. COMPLETED MODULES ──
            var completedRows = students
                .Select(s => new LeaderboardRow
                {
                    FullName = s.FullName,
                    Cohort = s.Cohort?.Name ?? "N/A",
                    TrackCode = s.Track?.Code ?? "N/A",
                    Score = s.ModuleCompletions.Count(mc => mc.IsCompleted)
                })
                .OrderByDescending(x => x.Score)
                .ToList();

            var completedByTrack = tracks
                .Select(t => new TrackLeaderboardGroup
                {
                    TrackCode = t.Code,
                    Rows = completedRows.Where(r => r.TrackCode == t.Code).Take(3).ToList()
                })
                .Where(g => g.Rows.Any())
                .ToList();

            // ── 2. ACTIVE THIS WEEK (log count, Mon–Sun) ──
            var weekRows = students
                .Select(s => new LeaderboardRow
                {
                    FullName = s.FullName,
                    Cohort = s.Cohort?.Name ?? "N/A",
                    TrackCode = s.Track?.Code ?? "N/A",
                    Score = s.ProgressLogs.Count(l => l.Date.Date >= weekStart)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ToList();

            var weekByTrack = tracks
                .Select(t => new TrackLeaderboardGroup
                {
                    TrackCode = t.Code,
                    Rows = weekRows.Where(r => r.TrackCode == t.Code).Take(3).ToList()
                })
                .Where(g => g.Rows.Any())
                .ToList();

            // ── 3. ACTIVE THIS MONTH (approved hours) ──
            var monthRows = students
                .Select(s => new LeaderboardRow
                {
                    FullName = s.FullName,
                    Cohort = s.Cohort?.Name ?? "N/A",
                    TrackCode = s.Track?.Code ?? "N/A",
                    Score = (int)s.ProgressLogs
                                    .Where(l => l.Date.Date >= monthStart && l.IsApproved)
                                    .Sum(l => l.Hours)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ToList();

            var monthByTrack = tracks
                .Select(t => new TrackLeaderboardGroup
                {
                    TrackCode = t.Code,
                    Rows = monthRows.Where(r => r.TrackCode == t.Code).Take(3).ToList()
                })
                .Where(g => g.Rows.Any())
                .ToList();

            // ── 4. ACTIVE TODAY (all who logged today) ──
            var todayRows = students
                .Where(s => s.ProgressLogs.Any(l => l.Date.Date == today))
                .Select(s => new LeaderboardRow
                {
                    FullName = s.FullName,
                    Cohort = s.Cohort?.Name ?? "N/A",
                    TrackCode = s.Track?.Code ?? "N/A",
                    Score = s.ProgressLogs.Count(l => l.Date.Date == today)
                })
                .OrderByDescending(x => x.Score)
                .ToList();

            // ── 5. CONSISTENCY (distinct approved log days, all time) ──
            var consistencyRows = students
                .Select(s => new LeaderboardRow
                {
                    FullName = s.FullName,
                    Cohort = s.Cohort?.Name ?? "N/A",
                    TrackCode = s.Track?.Code ?? "N/A",
                    Score = s.ProgressLogs
                                  .Where(l => l.IsApproved)
                                  .Select(l => l.Date.Date)
                                  .Distinct()
                                  .Count()
                })
                .OrderByDescending(x => x.Score)
                .ToList();

            var model = new LeaderboardDashboardViewModel
            {
                CompletedModules = completedRows.Take(3).ToList(),
                CompletedByTrack = completedByTrack,
                ActiveThisWeek = weekRows.Take(3).ToList(),
                ActiveWeekByTrack = weekByTrack,
                ActiveThisMonth = monthRows.Take(3).ToList(),
                ActiveMonthByTrack = monthByTrack,
                ActiveToday = todayRows,
                Consistency = consistencyRows.Take(3).ToList()
            };

            return View(model);
        }
    }
}