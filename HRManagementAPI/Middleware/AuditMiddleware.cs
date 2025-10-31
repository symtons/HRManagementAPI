using HRManagementAPI.Data;
using HRManagementAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace HRManagementAPI.Middleware
{
    public class AuditMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuditMiddleware> _logger;

        public AuditMiddleware(RequestDelegate next, ILogger<AuditMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
        {
            var path = context.Request.Path.ToString();

            // Skip logging for swagger and static files only
            if (path.Contains("/swagger", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/weatherforecast", StringComparison.OrdinalIgnoreCase) ||
                path == "/")
            {
                await _next(context);
                return;
            }

            var startTime = DateTime.UtcNow;

            try
            {
                // Execute the request first
                await _next(context);

                // Log after request is processed
                var duration = DateTime.UtcNow - startTime;
                var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                _logger.LogInformation("Audit: Path={Path}, UserId={UserId}, Status={Status}",
                    path, userId ?? "Anonymous", context.Response.StatusCode);

                // Only log if we have a valid userId
                if (!string.IsNullOrEmpty(userId) && int.TryParse(userId, out int userIdInt))
                {
                    try
                    {
                        string action = DetermineAction(context.Request.Method, path);
                        string entityName = DetermineEntity(path);
                        string status = context.Response.StatusCode >= 200 && context.Response.StatusCode < 300 ? "Success" : "Failed";
                        string description = $"{context.Request.Method} {path} - Status: {context.Response.StatusCode}";

                        // Get user details
                        var user = await dbContext.Users
                            .AsNoTracking()
                            .Include(u => u.Role)
                            .FirstOrDefaultAsync(u => u.UserId == userIdInt);

                        if (user != null)
                        {
                            var auditLog = new AuditLog
                            {
                                UserId = userIdInt,
                                UserEmail = user.Email,
                                UserRole = user.Role?.RoleName,
                                Action = action,
                                EntityName = entityName,
                                Description = description,
                                Endpoint = path,
                                HttpMethod = context.Request.Method,
                                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                                UserAgent = context.Request.Headers["User-Agent"].ToString(),
                                Status = status,
                                ErrorMessage = status == "Failed" ? $"HTTP {context.Response.StatusCode}" : null,
                                CreatedAt = DateTime.UtcNow
                            };

                            dbContext.AuditLogs.Add(auditLog);
                            await dbContext.SaveChangesAsync();

                            _logger.LogInformation("Audit log saved: User={Email}, Action={Action}, Entity={Entity}",
                                user.Email, action, entityName);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error saving audit log for user {UserId}", userIdInt);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AuditMiddleware");
                throw;
            }
        }

        private string DetermineAction(string httpMethod, string path)
        {
            var lowerPath = path.ToLower();

            if (lowerPath.Contains("/login")) return "LOGIN";
            if (lowerPath.Contains("/logout")) return "LOGOUT";
            if (lowerPath.Contains("/register")) return "CREATE";

            return httpMethod.ToUpper() switch
            {
                "GET" => "READ",
                "POST" => "CREATE",
                "PUT" => "UPDATE",
                "PATCH" => "UPDATE",
                "DELETE" => "DELETE",
                _ => "UNKNOWN"
            };
        }

        private string DetermineEntity(string path)
        {
            var lowerPath = path.ToLower();

            if (lowerPath.Contains("/employee")) return "Employee";
            if (lowerPath.Contains("/user")) return "User";
            if (lowerPath.Contains("/department")) return "Department";
            if (lowerPath.Contains("/role")) return "Role";
            if (lowerPath.Contains("/menu")) return "MenuItem";
            if (lowerPath.Contains("/leave")) return "Leave";
            if (lowerPath.Contains("/attendance")) return "Attendance";
            if (lowerPath.Contains("/shift")) return "Shift";
            if (lowerPath.Contains("/auth")) return "Authentication";

            return "System";
        }
    }

    public static class AuditMiddlewareExtensions
    {
        public static IApplicationBuilder UseAuditLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AuditMiddleware>();
        }
    }
}