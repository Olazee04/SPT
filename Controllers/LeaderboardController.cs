using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models.ViewModels;
using SPT.Models;

namespace SPT.Controllers
{
    [Authorize] // Everyone (Students, Mentors, Admins) can see this
    public class LeaderboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LeaderboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // 1. Get all active students
            var students = await _context.Students
                .Include(s => s.Track)
                .Include(s => s.Cohort)
                .Include(s => s.ProgressLogs)
                .Where(s => s.EnrollmentStatus == "Active")
                .ToListAsync();

            // 2. Calculate Scores in Memory
            var leaderboard = students.Select(s => new LeaderboardViewModel
            {
                StudentId = s.Id,
                FullName = s.FullName,
                ProfilePicture = s.ProfilePicture,
                Track = s.Track?.Code ?? "N/A",
                Cohort = s.Cohort?.Name ?? "N/A",

                // Score Logic: Sum of Approved Hours
                TotalHours = s.ProgressLogs.Where(l => l.IsApproved).Sum(l => l.Hours),

                LogCount = s.ProgressLogs.Count(l => l.IsApproved)
            })
            .OrderByDescending(x => x.TotalHours) // Highest score first
            .ToList();

            // 3. Assign Ranks (Handling ties if needed, but simple index for now)
            int rank = 1;
            foreach (var entry in leaderboard)
            {
                entry.Rank = rank++;
            }

            return View(leaderboard);
        }
    }

    
}