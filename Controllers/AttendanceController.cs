using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;
using SPT.Models.ViewModels;

namespace SPT.Controllers
{
    [Authorize(Roles = "Admin,Mentor")]
    public class AttendanceController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AttendanceController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================
        // 1. DASHBOARD / HISTORY
        // =========================
        public async Task<IActionResult>
    Index(
    DateTime? startDate,
    DateTime? endDate,
    int? cohortId)
        {
            DateTime from = startDate ?? DateTime.UtcNow.Date.AddDays(-6); // last 7 days
            DateTime to = endDate ?? DateTime.UtcNow.Date;

            var logsQuery = _context.ProgressLogs
            .Include(l => l.Student)
            .ThenInclude(s => s.Cohort)
            .Where(l => l.Date >= from && l.Date <= to);

            if (cohortId.HasValue)
            {
                logsQuery = logsQuery.Where(l => l.Student.CohortId == cohortId);
            }

            var logs = await logsQuery.ToListAsync();

            // Group by Student
            var grouped = logs
            .GroupBy(l => l.Student)
            .Select(g => new AttendanceSummaryViewModel
            {
                StudentId = g.Key.Id,
                StudentName = g.Key.FullName,
                CohortName = g.Key.Cohort?.Name ?? "N/A",

                PresentDays = g.Select(x => x.Date.Date).Distinct().Count(),
                OfficeDays = g.Where(x => x.Location == "Office")
            .Select(x => x.Date.Date)
            .Distinct()
            .Count(),
                RemoteDays = g.Where(x => x.Location == "Remote")
            .Select(x => x.Date.Date)
            .Distinct()
            .Count(),
                TotalLogs = g.Count()
            })
            .ToList();

            // Calculate absences (simple version: weekdays only)
            int totalDays = Enumerable
            .Range(0, (to - from).Days + 1)
            .Select(d => from.AddDays(d))
            .Count(d => d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday);

            foreach (var item in grouped)
            {
                item.AbsentDays = Math.Max(0, totalDays - item.PresentDays);
            }

            ViewBag.SelectedCohort = cohortId;
            ViewBag.Cohorts = new SelectList(await _context.Cohorts.ToListAsync(), "Id", "Name");
            ViewBag.From = from;
            ViewBag.To = to;

            return View(grouped);
        }


    }
}