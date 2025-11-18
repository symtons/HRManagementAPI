using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using HRManagementAPI.Data;

namespace HRManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Dashboard/Stats
        [HttpGet("Stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var stats = new
            {
                totalEmployees = await _context.Employees.CountAsync(e => e.IsActive),
                activeEmployees = await _context.Employees.CountAsync(e => e.IsActive && e.EmploymentStatus == "Active"),
                pendingApprovals = 3, // TODO: Implement leave approval count
                activeShifts = 0, // TODO: Implement shift count
                systemHealth = "100%"
            };

            return Ok(stats);
        }

        // GET: api/Dashboard/RecentActivities
        [HttpGet("RecentActivities")]
        public async Task<IActionResult> GetRecentActivities()
        {
            var activities = await _context.AuditLogs
                .OrderByDescending(a => a.CreatedAt)
                .Take(10)
                .Select(a => new
                {
                    id = a.AuditLogId,
                    user = a.UserEmail,
                    action = a.Action,
                    entity = a.EntityName,
                    description = a.Description,
                    timestamp = a.CreatedAt,
                    status = a.Status
                })
                .ToListAsync();

            return Ok(activities);
        }

        // GET: api/Dashboard/QuickActions
        [HttpGet("QuickActions")]
        public IActionResult GetQuickActions()
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            var actions = new List<object>();

            if (userRole == "Admin" || userRole == "HRManager")
            {
                actions.Add(new
                {
                    id = "employee-profile",
                    title = "Employee Profile",
                    description = "Register new employee",
                    icon = "person_add",
                    route = "/employees/add"
                });
                actions.Add(new
                {
                    id = "generate-report",
                    title = "Generate Report",
                    description = "HR Action Form",
                    icon = "assessment",
                    route = "/reports"
                });
                actions.Add(new
                {
                    id = "review-approvals",
                    title = "Review Approvals",
                    description = "Check pending requests",
                    icon = "approval",
                    route = "/approvals"
                });
                actions.Add(new
                {
                    id = "system-health",
                    title = "System Health",
                    description = "View system status",
                    icon = "health_and_safety",
                    route = "/settings/health"
                });
            }
            else
            {
                actions.Add(new
                {
                    id = "my-profile",
                    title = "My Profile",
                    description = "View my information",
                    icon = "person",
                    route = "/profile"
                });
                actions.Add(new
                {
                    id = "request-leave",
                    title = "Request Leave",
                    description = "Submit leave request",
                    icon = "calendar_today",
                    route = "/leave/request"
                });
                actions.Add(new
                {
                    id = "clock-in",
                    title = "Clock In/Out",
                    description = "Record attendance",
                    icon = "schedule",
                    route = "/attendance"
                });
            }

            return Ok(actions);
        }
    }
}