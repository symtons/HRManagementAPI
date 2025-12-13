using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRManagementAPI.Data;
using System.Data;

namespace HRManagementAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(ApplicationDbContext context, ILogger<DashboardController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // =============================================
        // HELPER METHOD - Execute View Query
        // =============================================
        private async Task<List<Dictionary<string, object?>>> ExecuteViewQuery(string query, params object[] parameters)
        {
            var result = new List<Dictionary<string, object?>>();

            try
            {
                using var command = _context.Database.GetDbConnection().CreateCommand();
                command.CommandText = query;
                command.CommandType = CommandType.Text;

                // Add parameters if provided
                if (parameters != null && parameters.Length > 0)
                {
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var param = command.CreateParameter();
                        param.ParameterName = $"@p{i}";
                        param.Value = parameters[i] ?? DBNull.Value;
                        command.Parameters.Add(param);
                    }
                }

                await _context.Database.OpenConnectionAsync();

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }
                    result.Add(row);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing view query: {Query}", query);
                throw;
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }

            return result;
        }

        // =============================================
        // 1. ADMIN DASHBOARD ENDPOINTS
        // =============================================

        /// <summary>
        /// Get Admin Dashboard Metrics
        /// </summary>
        [HttpGet("admin/metrics")]
        public async Task<IActionResult> GetAdminMetrics()
        {
            try
            {
                var query = "SELECT * FROM vw_AdminDashboardMetrics";
                var metrics = await ExecuteViewQuery(query);

                if (metrics.Count == 0)
                {
                    return Ok(new Dictionary<string, object>());
                }

                return Ok(metrics[0]);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching admin dashboard metrics");
                return StatusCode(500, new { message = "Error fetching admin dashboard metrics" });
            }
        }

        /// <summary>
        /// Get Admin Department Breakdown
        /// </summary>
        [HttpGet("admin/departments")]
        public async Task<IActionResult> GetAdminDepartments()
        {
            try
            {
                var query = "SELECT * FROM vw_AdminDepartmentBreakdown ORDER BY DepartmentName";
                var departments = await ExecuteViewQuery(query);

                return Ok(new { departments });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching admin department breakdown");
                return StatusCode(500, new { message = "Error fetching department breakdown" });
            }
        }

        /// <summary>
        /// Get Admin Recent Activity
        /// </summary>
        [HttpGet("admin/activity")]
        public async Task<IActionResult> GetAdminActivity()
        {
            try
            {
                var query = "SELECT TOP 20 * FROM vw_AdminRecentActivity ORDER BY ActivityDate DESC";
                var activity = await ExecuteViewQuery(query);

                return Ok(new { activity });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching admin recent activity");
                return StatusCode(500, new { message = "Error fetching recent activity" });
            }
        }

        /// <summary>
        /// Get Complete Admin Dashboard Data
        /// </summary>
        [HttpGet("admin")]
        public async Task<IActionResult> GetAdminDashboard()
        {
            try
            {
                var metricsQuery = "SELECT * FROM vw_AdminDashboardMetrics";
                var deptQuery = "SELECT * FROM vw_AdminDepartmentBreakdown ORDER BY DepartmentName";
                var activityQuery = "SELECT TOP 20 * FROM vw_AdminRecentActivity ORDER BY ActivityDate DESC";

                var metrics = await ExecuteViewQuery(metricsQuery);
                var departments = await ExecuteViewQuery(deptQuery);
                var activity = await ExecuteViewQuery(activityQuery);

                var response = new
                {
                    metrics = metrics.Count > 0 ? metrics[0] : new Dictionary<string, object?>(),
                    departments = departments,
                    activity = activity
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching admin dashboard");
                return StatusCode(500, new { message = "Error fetching admin dashboard" });
            }
        }

        // =============================================
        // 2. EXECUTIVE DASHBOARD ENDPOINTS
        // =============================================

        /// <summary>
        /// Get Executive Dashboard Metrics
        /// </summary>
        [HttpGet("executive/metrics")]
        public async Task<IActionResult> GetExecutiveMetrics()
        {
            try
            {
                var query = "SELECT * FROM vw_ExecutiveDashboardMetrics";
                var metrics = await ExecuteViewQuery(query);

                if (metrics.Count == 0)
                {
                    return Ok(new Dictionary<string, object>());
                }

                return Ok(metrics[0]);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching executive dashboard metrics");
                return StatusCode(500, new { message = "Error fetching executive metrics" });
            }
        }

        /// <summary>
        /// Get Executive Headcount Trend
        /// </summary>
        [HttpGet("executive/headcount-trend")]
        public async Task<IActionResult> GetExecutiveHeadcountTrend()
        {
            try
            {
                var query = "SELECT * FROM vw_ExecutiveHeadcountTrend";
                var trend = await ExecuteViewQuery(query);

                return Ok(new { trend });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching headcount trend");
                return StatusCode(500, new { message = "Error fetching headcount trend" });
            }
        }

        /// <summary>
        /// Get Executive Department Performance
        /// </summary>
        [HttpGet("executive/department-performance")]
        public async Task<IActionResult> GetExecutiveDepartmentPerformance()
        {
            try
            {
                var query = "SELECT * FROM vw_ExecutiveDepartmentPerformance ORDER BY DepartmentName";
                var performance = await ExecuteViewQuery(query);

                return Ok(new { performance });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching department performance");
                return StatusCode(500, new { message = "Error fetching department performance" });
            }
        }

        /// <summary>
        /// Get Complete Executive Dashboard Data
        /// </summary>
        [HttpGet("executive")]
        public async Task<IActionResult> GetExecutiveDashboard()
        {
            try
            {
                var metricsQuery = "SELECT * FROM vw_ExecutiveDashboardMetrics";
                var trendQuery = "SELECT * FROM vw_ExecutiveHeadcountTrend";
                var perfQuery = "SELECT * FROM vw_ExecutiveDepartmentPerformance ORDER BY DepartmentName";

                var metrics = await ExecuteViewQuery(metricsQuery);
                var trend = await ExecuteViewQuery(trendQuery);
                var performance = await ExecuteViewQuery(perfQuery);

                var response = new
                {
                    metrics = metrics.Count > 0 ? metrics[0] : new Dictionary<string, object?>(),
                    trend = trend,
                    performance = performance
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching executive dashboard");
                return StatusCode(500, new { message = "Error fetching executive dashboard" });
            }
        }

        // =============================================
        // 3. DIRECTOR DASHBOARD ENDPOINTS
        // =============================================

        /// <summary>
        /// Get Director Dashboard Metrics
        /// </summary>
        [HttpGet("director/{departmentId}/metrics")]
        public async Task<IActionResult> GetDirectorMetrics(int departmentId)
        {
            try
            {
                var query = "SELECT * FROM vw_DirectorDashboardMetrics WHERE DepartmentId = @p0";
                var metrics = await ExecuteViewQuery(query, departmentId);

                if (metrics.Count == 0)
                {
                    return NotFound(new { message = "Department not found" });
                }

                return Ok(metrics[0]);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching director metrics for department {DepartmentId}", departmentId);
                return StatusCode(500, new { message = "Error fetching director metrics" });
            }
        }

        /// <summary>
        /// Get Director Pending Approvals
        /// </summary>
        [HttpGet("director/{departmentId}/pending-approvals")]
        public async Task<IActionResult> GetDirectorPendingApprovals(int departmentId)
        {
            try
            {
                var query = "SELECT * FROM vw_DirectorPendingApprovals WHERE DepartmentId = @p0 ORDER BY RequestedAt DESC";
                var approvals = await ExecuteViewQuery(query, departmentId);

                return Ok(new { approvals });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching pending approvals for department {DepartmentId}", departmentId);
                return StatusCode(500, new { message = "Error fetching pending approvals" });
            }
        }

        /// <summary>
        /// Get Director Team Status
        /// </summary>
        [HttpGet("director/{departmentId}/team-status")]
        public async Task<IActionResult> GetDirectorTeamStatus(int departmentId)
        {
            try
            {
                var query = "SELECT * FROM vw_DirectorTeamStatus WHERE DepartmentId = @p0 ORDER BY LastName, FirstName";
                var team = await ExecuteViewQuery(query, departmentId);

                return Ok(new { team });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching team status for department {DepartmentId}", departmentId);
                return StatusCode(500, new { message = "Error fetching team status" });
            }
        }

        /// <summary>
        /// Get Complete Director Dashboard Data
        /// </summary>
        [HttpGet("director/{departmentId}")]
        public async Task<IActionResult> GetDirectorDashboard(int departmentId)
        {
            try
            {
                var metricsQuery = "SELECT * FROM vw_DirectorDashboardMetrics WHERE DepartmentId = @p0";
                var approvalsQuery = "SELECT * FROM vw_DirectorPendingApprovals WHERE DepartmentId = @p0 ORDER BY RequestedAt DESC";
                var teamQuery = "SELECT * FROM vw_DirectorTeamStatus WHERE DepartmentId = @p0 ORDER BY LastName, FirstName";

                var metrics = await ExecuteViewQuery(metricsQuery, departmentId);
                var approvals = await ExecuteViewQuery(approvalsQuery, departmentId);
                var team = await ExecuteViewQuery(teamQuery, departmentId);

                if (metrics.Count == 0)
                {
                    return NotFound(new { message = "Department not found" });
                }

                var response = new
                {
                    metrics = metrics[0],
                    approvals = approvals,
                    team = team
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching director dashboard for department {DepartmentId}", departmentId);
                return StatusCode(500, new { message = "Error fetching director dashboard" });
            }
        }

        // =============================================
        // 4. COORDINATOR DASHBOARD ENDPOINTS
        // =============================================

        /// <summary>
        /// Get Coordinator Dashboard Metrics
        /// </summary>
        [HttpGet("coordinator/{employeeId}")]
        public async Task<IActionResult> GetCoordinatorDashboard(int employeeId)
        {
            try
            {
                var query = "SELECT * FROM vw_CoordinatorDashboardMetrics WHERE CoordinatorId = @p0";
                var metrics = await ExecuteViewQuery(query, employeeId);

                if (metrics.Count == 0)
                {
                    return NotFound(new { message = "Coordinator not found" });
                }

                var response = new
                {
                    metrics = metrics[0]
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching coordinator dashboard for employee {EmployeeId}", employeeId);
                return StatusCode(500, new { message = "Error fetching coordinator dashboard" });
            }
        }

        // =============================================
        // 5. MANAGER DASHBOARD ENDPOINTS
        // =============================================

        /// <summary>
        /// Get Manager Dashboard Metrics
        /// </summary>
        [HttpGet("manager/{employeeId}")]
        public async Task<IActionResult> GetManagerDashboard(int employeeId)
        {
            try
            {
                var query = "SELECT * FROM vw_ManagerDashboardMetrics WHERE ManagerId = @p0";
                var metrics = await ExecuteViewQuery(query, employeeId);

                if (metrics.Count == 0)
                {
                    return NotFound(new { message = "Manager not found" });
                }

                var response = new
                {
                    metrics = metrics[0]
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching manager dashboard for employee {EmployeeId}", employeeId);
                return StatusCode(500, new { message = "Error fetching manager dashboard" });
            }
        }

        // =============================================
        // 6. EMPLOYEE DASHBOARD ENDPOINTS
        // =============================================

        /// <summary>
        /// Get Employee Dashboard Metrics
        /// </summary>
        [HttpGet("employee/{employeeId}/metrics")]
        public async Task<IActionResult> GetEmployeeMetrics(int employeeId)
        {
            try
            {
                var query = "SELECT * FROM vw_EmployeeDashboardMetrics WHERE EmployeeId = @p0";
                var metrics = await ExecuteViewQuery(query, employeeId);

                if (metrics.Count == 0)
                {
                    return NotFound(new { message = "Employee not found" });
                }

                return Ok(metrics[0]);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employee metrics for {EmployeeId}", employeeId);
                return StatusCode(500, new { message = "Error fetching employee metrics" });
            }
        }

        /// <summary>
        /// Get Employee Leave Requests
        /// </summary>
        [HttpGet("employee/{employeeId}/leave-requests")]
        public async Task<IActionResult> GetEmployeeLeaveRequests(int employeeId)
        {
            try
            {
                var query = "SELECT * FROM vw_EmployeeLeaveRequests WHERE EmployeeId = @p0 ORDER BY RequestedAt DESC";
                var requests = await ExecuteViewQuery(query, employeeId);

                return Ok(new { requests });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching leave requests for employee {EmployeeId}", employeeId);
                return StatusCode(500, new { message = "Error fetching leave requests" });
            }
        }

        /// <summary>
        /// Get Employee Upcoming Events
        /// </summary>
        [HttpGet("employee/{employeeId}/upcoming-events")]
        public async Task<IActionResult> GetEmployeeUpcomingEvents(int employeeId)
        {
            try
            {
                var query = "SELECT * FROM vw_EmployeeUpcomingEvents WHERE EmployeeId = @p0 ORDER BY EventDate";
                var events = await ExecuteViewQuery(query, employeeId);

                return Ok(new { events });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching upcoming events for employee {EmployeeId}", employeeId);
                return StatusCode(500, new { message = "Error fetching upcoming events" });
            }
        }

        /// <summary>
        /// Get Complete Employee Dashboard Data
        /// </summary>
        [HttpGet("employee/{employeeId}")]
        public async Task<IActionResult> GetEmployeeDashboard(int employeeId)
        {
            try
            {
                var metricsQuery = "SELECT * FROM vw_EmployeeDashboardMetrics WHERE EmployeeId = @p0";
                var requestsQuery = "SELECT * FROM vw_EmployeeLeaveRequests WHERE EmployeeId = @p0 ORDER BY RequestedAt DESC";
                var eventsQuery = "SELECT * FROM vw_EmployeeUpcomingEvents WHERE EmployeeId = @p0 ORDER BY EventDate";

                var metrics = await ExecuteViewQuery(metricsQuery, employeeId);
                var requests = await ExecuteViewQuery(requestsQuery, employeeId);
                var events = await ExecuteViewQuery(eventsQuery, employeeId);

                if (metrics.Count == 0)
                {
                    return NotFound(new { message = "Employee not found" });
                }

                var response = new
                {
                    metrics = metrics[0],
                    requests = requests,
                    events = events
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employee dashboard for {EmployeeId}", employeeId);
                return StatusCode(500, new { message = "Error fetching employee dashboard" });
            }
        }
    }
}