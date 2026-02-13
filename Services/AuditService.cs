using Microsoft.AspNetCore.Http;
using SPT.Data;
using SPT.Models;

namespace SPT.Services
{
    public class AuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _http;

        public AuditService(ApplicationDbContext context, IHttpContextAccessor http)
        {
            _context = context;
            _http = http;
        }

        public async Task LogAsync(
            string action,
            string details,
            string performedBy,
            string? userId = null)
        {
            var ip = _http.HttpContext?.Connection?.RemoteIpAddress?.ToString();

            var log = new AuditLog
            {
                Action = action,
                Details = details,
                PerformedBy = performedBy,
                EditedBy = performedBy,
                EditedByUserId = userId ?? "",
                IpAddress = ip ?? "unknown",
                TableName = "SYSTEM",
                FieldName = "-",
                RecordId = 0,
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}
