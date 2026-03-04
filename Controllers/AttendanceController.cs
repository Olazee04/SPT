using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
        private readonly UserManager<ApplicationUser> _userManager;

        public AttendanceController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(
            DateTime? startDate,
            DateTime? endDate,
            int? cohortId,
            string period = "week")
        {
            var today = DateTime.UtcNow.Date;
            int daysSinceMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var weekStart = today.AddDays(-daysSinceMonday);
            var monthStart = new DateTime(today.Year, today.Month, 1);

            DateTime from, to;
            switch (period)
            {
                case "today": from = to = today; break;
                case "month": from = monthStart; to = today; break;
                case "alltime": from = new DateTime(2020, 1, 1); to = today; break;
                case "custom": from = startDate ?? weekStart; to = endDate ?? today; break;
                default: from = weekStart; to = today; break;
            }

            // ── Scope students ──
            IQueryable<Student> studentScope = _context.Students
                .Include(s => s.Cohort)
                .Include(s => s.Track);

            if (User.IsInRole("Mentor"))
            {
                var userId = _userManager.GetUserId(User);
                var mentor = await _context.Mentors.FirstOrDefaultAsync(m => m.UserId == userId);
                if (mentor != null && mentor.Specialization != "General" && mentor.TrackId != null)
                    studentScope = studentScope.Where(s => s.MentorId == mentor.Id || s.TrackId == mentor.TrackId);
            }

            if (cohortId.HasValue)
                studentScope = studentScope.Where(s => s.CohortId == cohortId);

            var students = await studentScope.ToListAsync();
            var studentIds = students.Select(s => s.Id).ToList();

            var logs = await _context.ProgressLogs
                .Where(l => studentIds.Contains(l.StudentId) && l.Date.Date >= from && l.Date.Date <= to)
                .ToListAsync();

            int weekdayCount = Enumerable.Range(0, (to - from).Days + 1)
                .Select(i => from.AddDays(i))
                .Count(d => d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday);

            var summaryRows = students.Select(s =>
            {
                var sLogs = logs.Where(l => l.StudentId == s.Id).ToList();
                int presentDays = sLogs.Select(l => l.Date.Date).Distinct().Count();
                int officeDays = sLogs.Where(l => l.Location == "Office").Select(l => l.Date.Date).Distinct().Count();
                int remoteDays = sLogs.Where(l => l.Location == "Remote").Select(l => l.Date.Date).Distinct().Count();
                decimal totalHrs = sLogs.Where(l => l.IsApproved).Sum(l => l.Hours);

                return new AttendanceSummaryViewModel
                {
                    StudentId = s.Id,
                    StudentName = s.FullName,
                    CohortName = s.Cohort?.Name ?? "N/A",
                    TrackCode = s.Track?.Code ?? "N/A",
                    PresentDays = presentDays,
                    OfficeDays = officeDays,
                    RemoteDays = remoteDays,
                    AbsentDays = Math.Max(0, weekdayCount - presentDays),
                    TotalLogs = sLogs.Count,
                    TotalHours = totalHrs
                };
            }).OrderByDescending(x => x.PresentDays).ToList();

            int tp = summaryRows.Sum(x => x.PresentDays);
            int ta = summaryRows.Sum(x => x.AbsentDays);
            int tof = summaryRows.Sum(x => x.OfficeDays);
            int trm = summaryRows.Sum(x => x.RemoteDays);
            decimal th = summaryRows.Sum(x => x.TotalHours);

            ViewBag.TotalPresent = tp;
            ViewBag.TotalAbsent = ta;
            ViewBag.TotalOffice = tof;
            ViewBag.TotalRemote = trm;
            ViewBag.TotalHours = th;
            ViewBag.StudentCount = summaryRows.Count;
            ViewBag.AttendanceRate = (tp + ta) > 0 ? Math.Round((double)tp / (tp + ta) * 100, 1) : 0.0;

            // ── Chart: last 14 weekdays in range ──
            var chartFrom = (to - from).Days > 14 ? to.AddDays(-13) : from;
            var dailyLabels = new List<string>();
            var dailyPresent = new List<int>();
            var dailyAbsent = new List<int>();

            for (var d = chartFrom; d <= to; d = d.AddDays(1))
            {
                if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
                int pres = logs.Where(l => l.Date.Date == d).Select(l => l.StudentId).Distinct().Count();
                dailyLabels.Add(d.ToString("MMM dd"));
                dailyPresent.Add(pres);
                dailyAbsent.Add(Math.Max(0, students.Count - pres));
            }

            ViewBag.ChartLabels = dailyLabels;
            ViewBag.ChartPresent = dailyPresent;
            ViewBag.ChartAbsent = dailyAbsent;

            var allCohorts = await _context.Cohorts.ToListAsync();
            if (User.IsInRole("Mentor"))
            {
                var relIds = students.Where(s => s.CohortId.HasValue).Select(s => s.CohortId!.Value).Distinct().ToList();
                allCohorts = allCohorts.Where(c => relIds.Contains(c.Id)).ToList();
            }

            ViewBag.Cohorts = new SelectList(allCohorts, "Id", "Name", cohortId);
            ViewBag.SelectedCohortId = cohortId;
            ViewBag.Period = period;
            ViewBag.From = from;
            ViewBag.To = to;

            return View(summaryRows);
        }
    }
}