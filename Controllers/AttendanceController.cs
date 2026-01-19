using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;

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
        public async Task<IActionResult> Index(DateTime? date, int? cohortId)
        {
            var today = date ?? DateTime.UtcNow.Date;

            var query = _context.Attendance
                .Include(a => a.Student)
                .ThenInclude(s => s.Cohort)
                .Where(a => a.Date == today)
                .AsQueryable();

            if (cohortId.HasValue)
            {
                query = query.Where(a => a.Student.CohortId == cohortId);
            }

            var records = await query.ToListAsync();

            ViewBag.Date = today;
            ViewBag.Cohorts = new SelectList(await _context.Cohorts.ToListAsync(), "Id", "Name");

            return View(records);
        }

        // =========================
        // 2. MARK ATTENDANCE (GET)
        // =========================
        [HttpGet]
        public async Task<IActionResult> Mark(int? cohortId)
        {
            // If no cohort selected, show empty selection page
            if (!cohortId.HasValue)
            {
                ViewBag.Cohorts = new SelectList(await _context.Cohorts.Where(c => c.IsActive).ToListAsync(), "Id", "Name");
                return View(new List<Student>());
            }

            // Get all active students in this cohort
            var students = await _context.Students
                .Where(s => s.CohortId == cohortId && s.EnrollmentStatus == "Active")
                .OrderBy(s => s.FullName)
                .ToListAsync();

            ViewBag.CohortId = cohortId;
            ViewBag.CohortName = (await _context.Cohorts.FindAsync(cohortId))?.Name;
            ViewBag.Cohorts = new SelectList(await _context.Cohorts.Where(c => c.IsActive).ToListAsync(), "Id", "Name");

            return View(students);
        }

        // =========================
        // 3. SAVE ATTENDANCE (POST)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAttendance(int cohortId, DateTime date, Dictionary<int, string> status, Dictionary<int, string> remarks)
        {
            // 1. Remove existing records for this cohort on this date (to prevent duplicates/allow updates)
            var existing = await _context.Attendance
                .Include(a => a.Student)
                .Where(a => a.Date == date && a.Student.CohortId == cohortId)
                .ToListAsync();

            _context.Attendance.RemoveRange(existing);

            // 2. Add new records
            var newRecords = new List<Attendance>();
            foreach (var studentId in status.Keys)
            {
                newRecords.Add(new Attendance
                {
                    StudentId = studentId,
                    Date = date,
                    Status = status[studentId], // Present, Absent, etc.
                    Remarks = remarks.ContainsKey(studentId) ? remarks[studentId] : null
                });
            }

            _context.Attendance.AddRange(newRecords);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Attendance marked for {newRecords.Count} students.";
            return RedirectToAction("Index", new { date = date, cohortId = cohortId });
        }
    }
}