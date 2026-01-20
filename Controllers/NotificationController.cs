using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPT.Data;

namespace SPT.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly Microsoft.AspNetCore.Identity.UserManager<SPT.Models.ApplicationUser> _userManager;

        public NotificationController(ApplicationDbContext context, Microsoft.AspNetCore.Identity.UserManager<SPT.Models.ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var userId = _userManager.GetUserId(User);
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(10) // Last 10
                .Select(n => new {
                    n.Id,
                    n.Title,
                    n.Message,
                    n.IsRead,
                    n.Url,
                    TimeAgo = TimeAgo(n.CreatedAt) // Helper method logic below
                })
                .ToListAsync();

            var unreadCount = await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

            return Json(new { count = unreadCount, notifications = notifications });
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var notif = await _context.Notifications.FindAsync(id);
            if (notif != null && notif.UserId == _userManager.GetUserId(User))
            {
                notif.IsRead = true;
                await _context.SaveChangesAsync();
            }
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllRead()
        {
            var userId = _userManager.GetUserId(User);
            var unread = await _context.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
            foreach (var n in unread) n.IsRead = true;
            await _context.SaveChangesAsync();
            return Ok();
        }

        // Simple Helper for "2 mins ago"
        private static string TimeAgo(DateTime date)
        {
            var span = DateTime.UtcNow - date;
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            return $"{(int)span.TotalDays}d ago";
        }
    }
}