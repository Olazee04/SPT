using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;
using SPT.Models.ViewModels; // ✅ FIXED: Added this namespace

namespace SPT.Controllers
{
    [Authorize]
    public class LeaderboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public LeaderboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var currentUserId = user?.Id;

            // 1. Get All Active Students with their Logs
            var students = await _context.Students
                .Include(s => s.ProgressLogs)
                .Include(s => s.Track)
                .Where(s => s.EnrollmentStatus == "Active")
                .ToListAsync();

            // 2. Transform into Leaderboard Data
            var leaderboard = students.Select(s => new LeaderboardViewModel
            {
                FullName = s.FullName, // ✅ FIXED: Changed StudentName to FullName to match ViewModel
                TrackCode = s.Track?.Code ?? "N/A",
                ProfilePicture = s.ProfilePicture,
                StudentId = s.Id,

                // Only sum APPROVED logs
                TotalHours = s.ProgressLogs.Where(l => l.IsApproved).Sum(l => l.Hours),

                // Calculate Consistency
                ConsistencyScore = CalculateConsistency(s.ProgressLogs.Where(l => l.IsApproved).ToList(), s.TargetHoursPerWeek),

                IsCurrentUser = s.UserId == currentUserId
            })
            .OrderByDescending(x => x.TotalHours) // Highest hours first
            .ThenByDescending(x => x.ConsistencyScore) // Tie-breaker
            .ToList();

            // 3. Assign Rank Numbers (1, 2, 3...)
            for (int i = 0; i < leaderboard.Count; i++)
            {
                leaderboard[i].Rank = i + 1;
            }

            return View(leaderboard);
        }

        // Helper to calculate score quickly
        private int CalculateConsistency(List<ProgressLog> logs, int targetPerWeek)
        {
            if (targetPerWeek == 0) return 0;
            var last28Days = DateTime.UtcNow.Date.AddDays(-28);
            var recentHours = logs.Where(l => l.Date >= last28Days).Sum(l => l.Hours);
            var target = targetPerWeek * 4;
            var score = (int)((recentHours / target) * 100);
            return score > 100 ? 100 : score;
        }
    }
}