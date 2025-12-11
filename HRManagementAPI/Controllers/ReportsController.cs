// HRManagementAPI/Controllers/ReportsController.cs
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
    public class ReportsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(ApplicationDbContext context, ILogger<ReportsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // =============================================
        // HELPER: Get User Info
        // =============================================
        private (int userId, string userRole, int? departmentId) GetUserInfo()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var userRoleClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(userRoleClaim))
            {
                throw new UnauthorizedAccessException("User information not found");
            }

            int userId = int.Parse(userIdClaim);

            // Get user's department (for Directors)
            var employee = _context.Employees
                .FirstOrDefault(e => e.UserId == userId);

            return (userId, userRoleClaim, employee?.DepartmentId);
        }

        // =============================================
        // 1. WORKFORCE REPORTS
        // =============================================

        [HttpGet("workforce/summary")]
        public async Task<IActionResult> GetWorkforceSummary([FromQuery] int? departmentId = null)
        {
            try
            {
                var (userId, userRole, userDeptId) = GetUserInfo();

                // Role-based filtering
                int? filterDeptId = null;
                if (userRole == "Director")
                {
                    filterDeptId = userDeptId; // Directors see only their department
                }
                else if (departmentId.HasValue)
                {
                    filterDeptId = departmentId; // Admin/Executive can filter by department
                }

                var query = @"
                    SELECT 
                        EmployeeId, FirstName, LastName, EmployeeCode, JobTitle, 
                        EmployeeType, EmploymentStatus, HireDate, Salary, 
                        DepartmentId, DepartmentName, Email, RoleName, RoleLevel,
                        YearsOfService, MonthsOfService, Age, IsActive
                    FROM vw_WorkforceSummary
                    WHERE (@DepartmentId IS NULL OR DepartmentId = @DepartmentId)
                    ORDER BY LastName, FirstName";

                var result = await _context.Database
                    .SqlQueryRaw<dynamic>(query,
                        new Microsoft.Data.SqlClient.SqlParameter("@DepartmentId", (object)filterDeptId ?? DBNull.Value))
                    .ToListAsync();

                return Ok(new { employees = result, count = result.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workforce summary");
                return StatusCode(500, new { message = "Error retrieving workforce summary", error = ex.Message });
            }
        }

        [HttpGet("workforce/headcount")]
        public async Task<IActionResult> GetHeadcountByDepartment()
        {
            try
            {
                var (userId, userRole, userDeptId) = GetUserInfo();

                var query = @"
                    SELECT 
                        DepartmentId, DepartmentName, TotalEmployees, 
                        AdminStaffCount, FieldStaffCount, ActiveEmployees, 
                        OnLeaveEmployees, TerminatedEmployees
                    FROM vw_HeadcountByDepartment
                    WHERE (@DepartmentId IS NULL OR DepartmentId = @DepartmentId)
                    ORDER BY DepartmentName";

                var deptFilter = userRole == "Director" ? userDeptId : (int?)null;

                var result = await _context.Database
                    .SqlQueryRaw<dynamic>(query,
                        new Microsoft.Data.SqlClient.SqlParameter("@DepartmentId", (object)deptFilter ?? DBNull.Value))
                    .ToListAsync();

                return Ok(new { departments = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting headcount");
                return StatusCode(500, new { message = "Error retrieving headcount", error = ex.Message });
            }
        }

        [HttpGet("workforce/turnover")]
        public async Task<IActionResult> GetTurnoverAnalysis([FromQuery] int? year = null)
        {
            try
            {
                var (userId, userRole, userDeptId) = GetUserInfo();

                var query = @"
                    SELECT 
                        EmployeeId, FirstName, LastName, DepartmentId, DepartmentName,
                        JobTitle, HireDate, TerminationDate, DaysEmployed, MonthsEmployed,
                        TerminationYear, TerminationMonth, EmploymentStatus
                    FROM vw_TurnoverAnalysis
                    WHERE (@DepartmentId IS NULL OR DepartmentId = @DepartmentId)
                      AND (@Year IS NULL OR TerminationYear = @Year)
                    ORDER BY TerminationDate DESC";

                var deptFilter = userRole == "Director" ? userDeptId : (int?)null;

                var result = await _context.Database
                    .SqlQueryRaw<dynamic>(query,
                        new Microsoft.Data.SqlClient.SqlParameter("@DepartmentId", (object)deptFilter ?? DBNull.Value),
                        new Microsoft.Data.SqlClient.SqlParameter("@Year", (object)year ?? DBNull.Value))
                    .ToListAsync();

                return Ok(new { turnover = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting turnover analysis");
                return StatusCode(500, new { message = "Error retrieving turnover analysis", error = ex.Message });
            }
        }

        // =============================================
        // 2. PAYROLL REPORTS
        // =============================================

        [HttpGet("payroll/by-department")]
        public async Task<IActionResult> GetPayrollByDepartment()
        {
            try
            {
                var (userId, userRole, userDeptId) = GetUserInfo();

                var query = @"
                    SELECT 
                        DepartmentId, DepartmentName, EmployeeCount, 
                        TotalPayroll, AvgSalary, MinSalary, MaxSalary,
                        AdminStaffPayroll, FieldStaffPayroll
                    FROM vw_PayrollByDepartment
                    WHERE (@DepartmentId IS NULL OR DepartmentId = @DepartmentId)
                    ORDER BY TotalPayroll DESC";

                var deptFilter = userRole == "Director" ? userDeptId : (int?)null;

                var result = await _context.Database
                    .SqlQueryRaw<dynamic>(query,
                        new Microsoft.Data.SqlClient.SqlParameter("@DepartmentId", (object)deptFilter ?? DBNull.Value))
                    .ToListAsync();

                return Ok(new { payroll = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payroll report");
                return StatusCode(500, new { message = "Error retrieving payroll report", error = ex.Message });
            }
        }

        [HttpGet("payroll/by-role")]
        public async Task<IActionResult> GetSalaryByRole()
        {
            try
            {
                var (userId, userRole, userDeptId) = GetUserInfo();

                var query = @"
                    SELECT 
                        JobTitle, DepartmentName, EmployeeCount, 
                        AvgSalary, MinSalary, MaxSalary
                    FROM vw_SalaryByRole
                    WHERE (@DepartmentId IS NULL OR DepartmentId = (SELECT DepartmentId FROM Departments WHERE DepartmentName = @DepartmentName))
                    ORDER BY AvgSalary DESC";

                // For directors, filter by their department name
                string deptName = null;
                if (userRole == "Director" && userDeptId.HasValue)
                {
                    var dept = await _context.Departments.FindAsync(userDeptId.Value);
                    deptName = dept?.DepartmentName;
                }

                var result = await _context.Database
                    .SqlQueryRaw<dynamic>(query,
                        new Microsoft.Data.SqlClient.SqlParameter("@DepartmentId", (object)userDeptId ?? DBNull.Value),
                        new Microsoft.Data.SqlClient.SqlParameter("@DepartmentName", (object)deptName ?? DBNull.Value))
                    .ToListAsync();

                return Ok(new { salaries = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting salary by role");
                return StatusCode(500, new { message = "Error retrieving salary by role", error = ex.Message });
            }
        }

        // =============================================
        // 3. LEAVE REPORTS
        // =============================================

        [HttpGet("leave/summary")]
        public async Task<IActionResult> GetLeaveSummary([FromQuery] int? year = null)
        {
            try
            {
                var (userId, userRole, userDeptId) = GetUserInfo();

                var currentYear = year ?? DateTime.Now.Year;

                var query = @"
                    SELECT 
                        LeaveRequestId, EmployeeId, FirstName, LastName, EmployeeCode,
                        DepartmentId, DepartmentName, JobTitle, LeaveTypeId, LeaveType,
                        LeaveTypeColor, StartDate, EndDate, TotalDays, Status, Reason,
                        CreatedAt, ApprovedAt, ApprovedBy, LeaveYear, LeaveMonth, LeaveMonthName
                    FROM vw_LeaveSummary
                    WHERE LeaveYear = @Year
                      AND (@DepartmentId IS NULL OR DepartmentId = @DepartmentId)
                    ORDER BY StartDate DESC";

                var deptFilter = userRole == "Director" ? userDeptId : (int?)null;

                var result = await _context.Database
                    .SqlQueryRaw<dynamic>(query,
                        new Microsoft.Data.SqlClient.SqlParameter("@Year", currentYear),
                        new Microsoft.Data.SqlClient.SqlParameter("@DepartmentId", (object)deptFilter ?? DBNull.Value))
                    .ToListAsync();

                return Ok(new { leave = result, year = currentYear });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting leave summary");
                return StatusCode(500, new { message = "Error retrieving leave summary", error = ex.Message });
            }
        }

        [HttpGet("leave/usage-by-department")]
        public async Task<IActionResult> GetLeaveUsageByDepartment()
        {
            try
            {
                var (userId, userRole, userDeptId) = GetUserInfo();

                var query = @"
                    SELECT 
                        DepartmentId, DepartmentName, LeaveType, RequestCount,
                        TotalDaysUsed, AvgDaysPerRequest, ApprovedDays, PendingDays
                    FROM vw_LeaveUsageByDepartment
                    WHERE (@DepartmentId IS NULL OR DepartmentId = @DepartmentId)
                    ORDER BY DepartmentName, LeaveType";

                var deptFilter = userRole == "Director" ? userDeptId : (int?)null;

                var result = await _context.Database
                    .SqlQueryRaw<dynamic>(query,
                        new Microsoft.Data.SqlClient.SqlParameter("@DepartmentId", (object)deptFilter ?? DBNull.Value))
                    .ToListAsync();

                return Ok(new { usage = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting leave usage");
                return StatusCode(500, new { message = "Error retrieving leave usage", error = ex.Message });
            }
        }

        [HttpGet("leave/pto-balances")]
        public async Task<IActionResult> GetPTOBalances()
        {
            try
            {
                var (userId, userRole, userDeptId) = GetUserInfo();

                var query = @"
                    SELECT 
                        EmployeeId, FirstName, LastName, EmployeeCode,
                        DepartmentId, DepartmentName, PTOBalance, IsEligibleForPTO,
                        PTOUsedThisYear
                    FROM vw_PTOBalanceSummary
                    WHERE (@DepartmentId IS NULL OR DepartmentId = @DepartmentId)
                    ORDER BY DepartmentName, LastName";

                var deptFilter = userRole == "Director" ? userDeptId : (int?)null;

                var result = await _context.Database
                    .SqlQueryRaw<dynamic>(query,
                        new Microsoft.Data.SqlClient.SqlParameter("@DepartmentId", (object)deptFilter ?? DBNull.Value))
                    .ToListAsync();

                return Ok(new { balances = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting PTO balances");
                return StatusCode(500, new { message = "Error retrieving PTO balances", error = ex.Message });
            }
        }

        // =============================================
        // 4. PERFORMANCE REPORTS
        // =============================================

        [HttpGet("performance/summary")]
        public async Task<IActionResult> GetPerformanceSummary([FromQuery] int? periodId = null)
        {
            try
            {
                var (userId, userRole, userDeptId) = GetUserInfo();

                var query = @"
                    SELECT 
                        EmployeeReviewId, PeriodId, EmployeeId, FirstName, LastName,
                        EmployeeCode, DepartmentId, DepartmentName, JobTitle, PeriodName,
                        PeriodType, StartDate, EndDate, OverallRating, Status, ReviewDate,
                        CompanyWideRank, DepartmentRank, RoleRank
                    FROM vw_PerformanceSummary
                    WHERE (@PeriodId IS NULL OR PeriodId = @PeriodId)
                      AND (@DepartmentId IS NULL OR DepartmentId = @DepartmentId)
                    ORDER BY OverallRating DESC";

                var deptFilter = userRole == "Director" ? userDeptId : (int?)null;

                var result = await _context.Database
                    .SqlQueryRaw<dynamic>(query,
                        new Microsoft.Data.SqlClient.SqlParameter("@PeriodId", (object)periodId ?? DBNull.Value),
                        new Microsoft.Data.SqlClient.SqlParameter("@DepartmentId", (object)deptFilter ?? DBNull.Value))
                    .ToListAsync();

                return Ok(new { reviews = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting performance summary");
                return StatusCode(500, new { message = "Error retrieving performance summary", error = ex.Message });
            }
        }

        [HttpGet("performance/by-department")]
        public async Task<IActionResult> GetPerformanceByDepartment([FromQuery] int? periodId = null)
        {
            try
            {
                var (userId, userRole, userDeptId) = GetUserInfo();

                var query = @"
                    SELECT 
                        DepartmentId, DepartmentName, PeriodId, PeriodName,
                        TotalReviews, AvgRating, MinRating, MaxRating,
                        CompletedReviews, InProgressReviews, OpenReviews
                    FROM vw_PerformanceByDepartment
                    WHERE (@PeriodId IS NULL OR PeriodId = @PeriodId)
                      AND (@DepartmentId IS NULL OR DepartmentId = @DepartmentId)
                    ORDER BY AvgRating DESC";

                var deptFilter = userRole == "Director" ? userDeptId : (int?)null;

                var result = await _context.Database
                    .SqlQueryRaw<dynamic>(query,
                        new Microsoft.Data.SqlClient.SqlParameter("@PeriodId", (object)periodId ?? DBNull.Value),
                        new Microsoft.Data.SqlClient.SqlParameter("@DepartmentId", (object)deptFilter ?? DBNull.Value))
                    .ToListAsync();

                return Ok(new { performance = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting performance by department");
                return StatusCode(500, new { message = "Error retrieving performance by department", error = ex.Message });
            }
        }

        [HttpGet("performance/goals")]
        public async Task<IActionResult> GetGoalCompletion()
        {
            try
            {
                var (userId, userRole, userDeptId) = GetUserInfo();

                var query = @"
                    SELECT 
                        EmployeeId, FirstName, LastName, DepartmentId, DepartmentName,
                        TotalGoals, ActiveGoals, CompletedGoals, AvgProgress
                    FROM vw_GoalCompletionSummary
                    WHERE (@DepartmentId IS NULL OR DepartmentId = @DepartmentId)
                    ORDER BY AvgProgress DESC";

                var deptFilter = userRole == "Director" ? userDeptId : (int?)null;

                var result = await _context.Database
                    .SqlQueryRaw<dynamic>(query,
                        new Microsoft.Data.SqlClient.SqlParameter("@DepartmentId", (object)deptFilter ?? DBNull.Value))
                    .ToListAsync();

                return Ok(new { goals = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting goal completion");
                return StatusCode(500, new { message = "Error retrieving goal completion", error = ex.Message });
            }
        }

        // =============================================
        // 5. RECRUITMENT REPORTS
        // =============================================

        [HttpGet("recruitment/applications")]
        public async Task<IActionResult> GetApplicationSummary([FromQuery] int? year = null)
        {
            try
            {
                var currentYear = year ?? DateTime.Now.Year;

                var query = @"
                    SELECT 
                        ApplicationId, FirstName, LastName, Email, PhoneNumber,
                        Position1, ApprovalStatus, SubmittedAt, ReviewedAt, ReviewedBy,
                        DaysToReview, ApplicationYear, ApplicationMonth, ApplicationMonthName
                    FROM vw_ApplicationSummary
                    WHERE ApplicationYear = @Year
                    ORDER BY SubmittedAt DESC";

                var result = await _context.Database
                    .SqlQueryRaw<dynamic>(query,
                        new Microsoft.Data.SqlClient.SqlParameter("@Year", currentYear))
                    .ToListAsync();

                return Ok(new { applications = result, year = currentYear });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting application summary");
                return StatusCode(500, new { message = "Error retrieving application summary", error = ex.Message });
            }
        }

        [HttpGet("recruitment/hiring-funnel")]
        public async Task<IActionResult> GetHiringFunnel([FromQuery] int? year = null)
        {
            try
            {
                var currentYear = year ?? DateTime.Now.Year;

                var query = @"
                    SELECT 
                        ApprovalStatus, ApplicationCount, AvgDaysToReview, ApplicationYear
                    FROM vw_HiringFunnel
                    WHERE ApplicationYear = @Year
                    ORDER BY ApplicationCount DESC";

                var result = await _context.Database
                    .SqlQueryRaw<dynamic>(query,
                        new Microsoft.Data.SqlClient.SqlParameter("@Year", currentYear))
                    .ToListAsync();

                return Ok(new { funnel = result, year = currentYear });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting hiring funnel");
                return StatusCode(500, new { message = "Error retrieving hiring funnel", error = ex.Message });
            }
        }

        // =============================================
        // 6. HR ACTIONS REPORTS
        // =============================================

        [HttpGet("hr-actions/summary")]
        public async Task<IActionResult> GetHRActionsSummary([FromQuery] int? year = null)
        {
            try
            {
                var (userId, userRole, userDeptId) = GetUserInfo();
                var currentYear = year ?? DateTime.Now.Year;

                var query = @"
                    SELECT 
                        RequestId, RequestNumber, EmployeeId, FirstName, LastName,
                        DepartmentId, DepartmentName, ActionType, Status, SubmittedDate,
                        EffectiveDate, ProcessingDays, RequestYear, RequestMonth
                    FROM vw_HRActionsSummary
                    WHERE RequestYear = @Year
                      AND (@DepartmentId IS NULL OR DepartmentId = @DepartmentId)
                    ORDER BY SubmittedDate DESC";

                var deptFilter = userRole == "Director" ? userDeptId : (int?)null;

                var result = await _context.Database
                    .SqlQueryRaw<dynamic>(query,
                        new Microsoft.Data.SqlClient.SqlParameter("@Year", currentYear),
                        new Microsoft.Data.SqlClient.SqlParameter("@DepartmentId", (object)deptFilter ?? DBNull.Value))
                    .ToListAsync();

                return Ok(new { hrActions = result, year = currentYear });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting HR actions summary");
                return StatusCode(500, new { message = "Error retrieving HR actions summary", error = ex.Message });
            }
        }

        [HttpGet("hr-actions/by-type")]
        public async Task<IActionResult> GetHRActionsByType([FromQuery] int? year = null)
        {
            try
            {
                var currentYear = year ?? DateTime.Now.Year;

                var query = @"
                    SELECT 
                        ActionType, Status, RequestCount, AvgProcessingDays, RequestYear
                    FROM vw_HRActionsByType
                    WHERE RequestYear = @Year
                    ORDER BY RequestCount DESC";

                var result = await _context.Database
                    .SqlQueryRaw<dynamic>(query,
                        new Microsoft.Data.SqlClient.SqlParameter("@Year", currentYear))
                    .ToListAsync();

                return Ok(new { actionTypes = result, year = currentYear });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting HR actions by type");
                return StatusCode(500, new { message = "Error retrieving HR actions by type", error = ex.Message });
            }
        }

        // =============================================
        // DASHBOARD OVERVIEW
        // =============================================

        [HttpGet("overview")]
        public async Task<IActionResult> GetReportsOverview()
        {
            try
            {
                var (userId, userRole, userDeptId) = GetUserInfo();

                // Get key metrics
                var totalEmployees = await _context.Employees
                    .Where(e => e.IsActive && e.EmploymentStatus == "Active")
                    .Where(e => userRole != "Director" || e.DepartmentId == userDeptId)
                    .CountAsync();

                var totalDepartments = userRole == "Director" ? 1 :
                    await _context.Departments.Where(d => d.IsActive).CountAsync();

                var activeLeaveDays = await _context.LeaveRequests
                    .Where(lr => lr.Status == "Approved" &&
                                 lr.StartDate.Year == DateTime.Now.Year)
                    .Join(_context.Employees, lr => lr.EmployeeId, e => e.EmployeeId, (lr, e) => new { lr, e })
                    .Where(x => userRole != "Director" || x.e.DepartmentId == userDeptId)
                    .SumAsync(x => x.lr.TotalDays);

                return Ok(new
                {
                    totalEmployees,
                    totalDepartments,
                    activeLeaveDays,
                    userRole,
                    canViewAllData = userRole == "Admin" || userRole == "Executive"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting reports overview");
                return StatusCode(500, new { message = "Error retrieving reports overview", error = ex.Message });
            }
        }
    }
}