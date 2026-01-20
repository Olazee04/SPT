using SPT.Data;
using SPT.Models;

namespace SPT.Services
{
    public class AuditService
    {
        private readonly ApplicationDbContext _context;

        public AuditService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(string action, string details, string user, string ip = "N/A")
        {
            _context.AuditLogs.Add(new AuditLog
            {
                Action = action,
                Details = details,
                PerformedBy = user,
                IpAddress = ip,
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }
    }
}