using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using SPT.Data;
using SPT.Models;
using System.Security.Claims;

namespace SPT.Middleware
{
    public class AuditMiddleware
    {
        private readonly RequestDelegate _next;

        public AuditMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager)
        {
            // Ignore static files
            var path = context.Request.Path.Value?.ToLower();
            if (path != null &&
                (path.StartsWith("/css") ||
                 path.StartsWith("/js") ||
                 path.StartsWith("/images") ||
                 path.StartsWith("/lib")))
            {
                await _next(context);
                return;
            }

            var user = context.User;
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var username = user.Identity?.IsAuthenticated == true
                ? user.Identity.Name
                : "Anonymous";

            var role = "Anonymous";
            if (userId != null)
            {
                var appUser = await userManager.FindByIdAsync(userId);
                if (appUser != null)
                {
                    var roles = await userManager.GetRolesAsync(appUser);
                    role = roles.FirstOrDefault() ?? "User";
                }
            }

            var audit = new AuditLog
            {
                Action = "REQUEST",
                Details = $"{context.Request.Method} {context.Request.Path}",
                PerformedBy = username,
                EditedBy = username,
                EditedByUserId = userId ?? "N/A",
                IpAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                TableName = "HTTP",
                FieldName = role,
                RecordId = 0,
                // ✅ Correct
                Timestamp = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
            };

            db.AuditLogs.Add(audit);
            await db.SaveChangesAsync();

            await _next(context);
        }
    }
}
