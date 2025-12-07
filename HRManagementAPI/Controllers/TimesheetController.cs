using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRManagementAPI.Data;
using HRManagementAPI.Models;
using System.Security.Claims;

namespace HRManagementAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TimesheetController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public TimesheetController(ApplicationDbContext context)
        {
            _dbContext = context;
        }

        // ============================================
        // HELPER METHODS
        // ============================================

        private async Task<Employee?> GetCurrentEmployee()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            return await _dbContext.Users
                .Where(u => u.UserId == userId)
                .Select(u => u.Employee)
                .FirstOrDefaultAsync();
        }

        private List<string> GetUserRoles()
        {
            return User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        }

        private bool IsAdminOrExecutive()
        {
            var roles = GetUserRoles();
            return roles.Contains("Admin") || roles.Contains("Executive");
        }

        private bool IsDirector()
        {
            var roles = GetUserRoles();
            return roles.Contains("Director");
        }

        // ============================================
        // GET MY TIMESHEETS (Employee View)
        // ============================================
        [HttpGet("MyTimesheets")]
        public async Task<IActionResult> GetMyTimesheets(
            [FromQuery] string? status = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var employee = await GetCurrentEmployee();
                if (employee == null)
                {
                    return BadRequest(new { message = "Employee record not found" });
                }

                var query = _dbContext.Timesheets
                    .Where(t => t.EmployeeId == employee.EmployeeId);

                // Filter by status
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(t => t.Status == status);
                }

                // Filter by date range
                if (startDate.HasValue)
                {
                    query = query.Where(t => t.StartDate >= startDate.Value);
                }
                if (endDate.HasValue)
                {
                    query = query.Where(t => t.EndDate <= endDate.Value);
                }

                var timesheets = await query
                    .OrderByDescending(t => t.StartDate)
                    .Select(t => new
                    {
                        t.TimesheetId,
                        t.StartDate,
                        t.EndDate,
                        t.TotalHours,
                        t.RegularHours,
                        t.OvertimeHours,
                        t.Status,
                        t.SubmittedAt,
                        t.ApprovedAt,
                        t.RejectionReason,
                        ApprovedByName = t.ApprovedBy != null
                            ? _dbContext.Users
                                .Where(u => u.UserId == t.ApprovedBy)
                                .Select(u => u.Employee.FirstName + " " + u.Employee.LastName)
                                .FirstOrDefault()
                            : null,
                        EntryCount = t.TimesheetEntries.Count
                    })
                    .ToListAsync();

                return Ok(timesheets);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving timesheets", error = ex.Message });
            }
        }

        // ============================================
        // GET TIMESHEET DETAILS (with daily breakdown)
        // ============================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTimesheetDetails(int id)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var user = await _dbContext.Users
                    .Include(u => u.Employee)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user?.Employee == null)
                {
                    return BadRequest(new { message = "Employee record not found" });
                }

                var timesheet = await _dbContext.Timesheets
                    .Include(t => t.Employee)
                        .ThenInclude(e => e.Department)
                    .Include(t => t.TimesheetEntries)
                        .ThenInclude(te => te.TimeEntry)
                    .FirstOrDefaultAsync(t => t.TimesheetId == id);

                if (timesheet == null)
                {
                    return NotFound(new { message = "Timesheet not found" });
                }

                // Authorization check
                var roles = GetUserRoles();
                var isAdminOrExec = roles.Contains("Admin") || roles.Contains("Executive");
                var isDirector = roles.Contains("Director");
                var isOwner = timesheet.EmployeeId == user.Employee.EmployeeId;
                var isSameDepartment = isDirector && timesheet.Employee.DepartmentId == user.Employee.DepartmentId;

                if (!isOwner && !isAdminOrExec && !isSameDepartment)
                {
                    return Forbid();
                }

                // Build response
                var response = new
                {
                    timesheet.TimesheetId,
                    timesheet.EmployeeId,
                    EmployeeName = $"{timesheet.Employee.FirstName} {timesheet.Employee.LastName}",
                    timesheet.Employee.EmployeeCode,
                    timesheet.Employee.JobTitle,
                    DepartmentName = timesheet.Employee.Department?.DepartmentName,
                    timesheet.StartDate,
                    timesheet.EndDate,
                    timesheet.TotalHours,
                    timesheet.RegularHours,
                    timesheet.OvertimeHours,
                    timesheet.Status,
                    timesheet.SubmittedAt,
                    timesheet.ApprovedAt,
                    timesheet.RejectionReason,
                    ApprovedByName = timesheet.ApprovedBy != null
                        ? await _dbContext.Users
                            .Where(u => u.UserId == timesheet.ApprovedBy)
                            .Select(u => u.Employee.FirstName + " " + u.Employee.LastName)
                            .FirstOrDefaultAsync()
                        : null,
                    Entries = timesheet.TimesheetEntries
                        .OrderBy(e => e.WorkDate)
                        .Select(e => new
                        {
                            e.TimesheetEntryId,
                            e.WorkDate,
                            e.StartTime,
                            e.EndTime,
                            e.Hours,
                            e.TaskDescription,
                            e.IsBillable,
                            // Include actual clock in/out if linked
                            ActualClockIn = e.TimeEntry?.ClockInTime,
                            ActualClockOut = e.TimeEntry?.ClockOutTime,
                            ActualHours = e.TimeEntry?.TotalHours,
                            BreakMinutes = e.TimeEntry?.BreakMinutes
                        })
                        .ToList()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving timesheet details", error = ex.Message });
            }
        }

        // ============================================
        // GET PENDING TIMESHEETS (Role-Based)
        // ============================================
        [HttpGet("Pending")]
        public async Task<IActionResult> GetPendingTimesheets(
            [FromQuery] int? departmentId = null)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var user = await _dbContext.Users
                    .Include(u => u.Employee)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user?.Employee == null)
                {
                    return BadRequest(new { message = "Employee record not found" });
                }

                var roles = GetUserRoles();
                var isAdminOrExec = roles.Contains("Admin") || roles.Contains("Executive");
                var isDirector = roles.Contains("Director");

                // Only Directors, Executives, and Admins can access
                if (!isDirector && !isAdminOrExec)
                {
                    return Forbid();
                }

                var query = _dbContext.Timesheets
                    .Include(t => t.Employee)
                        .ThenInclude(e => e.Department)
                    .Where(t => t.Status == "Submitted");

                // Directors see only their department (unless they're also Admin/Executive)
                if (isDirector && !isAdminOrExec)
                {
                    query = query.Where(t => t.Employee.DepartmentId == user.Employee.DepartmentId);
                }

                // Optional department filter (for Admin/Executive)
                if (departmentId.HasValue && isAdminOrExec)
                {
                    query = query.Where(t => t.Employee.DepartmentId == departmentId.Value);
                }

                var timesheets = await query
                    .OrderBy(t => t.Employee.Department.DepartmentName)
                    .ThenBy(t => t.SubmittedAt)
                    .Select(t => new
                    {
                        t.TimesheetId,
                        t.EmployeeId,
                        EmployeeName = t.Employee.FirstName + " " + t.Employee.LastName,
                        t.Employee.EmployeeCode,
                        t.Employee.JobTitle,
                        t.Employee.DepartmentId,
                        DepartmentName = t.Employee.Department.DepartmentName,
                        t.StartDate,
                        t.EndDate,
                        t.TotalHours,
                        t.RegularHours,
                        t.OvertimeHours,
                        t.Status,
                        t.SubmittedAt,
                        EntryCount = t.TimesheetEntries.Count,
                        DaysWorked = t.TimesheetEntries.Count(e => e.Hours > 0)
                    })
                    .ToListAsync();

                return Ok(new
                {
                    timesheets,
                    totalCount = timesheets.Count,
                    userRole = isAdminOrExec ? "Admin/Executive" : "Director",
                    canApproveAll = isAdminOrExec
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving pending timesheets", error = ex.Message });
            }
        }

        // ============================================
        // SUBMIT TIMESHEET FOR APPROVAL
        // ============================================
        [HttpPost("{id}/Submit")]
        public async Task<IActionResult> SubmitTimesheet(int id)
        {
            try
            {
                var employee = await GetCurrentEmployee();
                if (employee == null)
                {
                    return BadRequest(new { message = "Employee record not found" });
                }

                var timesheet = await _dbContext.Timesheets
                    .Include(t => t.TimesheetEntries)
                    .FirstOrDefaultAsync(t => t.TimesheetId == id);

                if (timesheet == null)
                {
                    return NotFound(new { message = "Timesheet not found" });
                }

                // Check ownership
                if (timesheet.EmployeeId != employee.EmployeeId)
                {
                    return Forbid();
                }

                // Check status
                if (timesheet.Status != "Draft" && timesheet.Status != "Rejected")
                {
                    return BadRequest(new { message = "Only Draft or Rejected timesheets can be submitted" });
                }

                // Validate timesheet has entries
                if (!timesheet.TimesheetEntries.Any())
                {
                    return BadRequest(new { message = "Cannot submit empty timesheet" });
                }

                // Update status
                timesheet.Status = "Submitted";
                timesheet.SubmittedAt = DateTime.UtcNow;
                timesheet.UpdatedAt = DateTime.UtcNow;
                timesheet.RejectionReason = null; // Clear any previous rejection reason

                await _dbContext.SaveChangesAsync();

                // TODO: Send notification to manager/director

                return Ok(new
                {
                    message = "Timesheet submitted successfully",
                    timesheetId = timesheet.TimesheetId,
                    status = timesheet.Status,
                    submittedAt = timesheet.SubmittedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error submitting timesheet", error = ex.Message });
            }
        }

        // ============================================
        // APPROVE TIMESHEET
        // ============================================
        [HttpPost("{id}/Approve")]
        public async Task<IActionResult> ApproveTimesheet(int id, [FromBody] TimesheetApprovalRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var user = await _dbContext.Users
                    .Include(u => u.Employee)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user?.Employee == null)
                {
                    return BadRequest(new { message = "Employee record not found" });
                }

                var roles = GetUserRoles();
                var isAdminOrExec = roles.Contains("Admin") || roles.Contains("Executive");
                var isDirector = roles.Contains("Director");

                // Only Directors, Executives, and Admins can approve
                if (!isDirector && !isAdminOrExec)
                {
                    return Forbid();
                }

                var timesheet = await _dbContext.Timesheets
                    .Include(t => t.Employee)
                    .FirstOrDefaultAsync(t => t.TimesheetId == id);

                if (timesheet == null)
                {
                    return NotFound(new { message = "Timesheet not found" });
                }

                // Check status
                if (timesheet.Status != "Submitted")
                {
                    return BadRequest(new { message = "Only Submitted timesheets can be approved" });
                }

                // Authorization: Directors can only approve their department
                if (isDirector && !isAdminOrExec)
                {
                    if (timesheet.Employee.DepartmentId != user.Employee.DepartmentId)
                    {
                        return Forbid();
                    }
                }

                // Update status
                timesheet.Status = "Approved";
                timesheet.ApprovedBy = userId;
                timesheet.ApprovedAt = DateTime.UtcNow;
                timesheet.UpdatedAt = DateTime.UtcNow;
                timesheet.RejectionReason = null;

                await _dbContext.SaveChangesAsync();

                // TODO: Send notification to employee

                return Ok(new
                {
                    message = "Timesheet approved successfully",
                    timesheetId = timesheet.TimesheetId,
                    status = timesheet.Status,
                    approvedBy = $"{user.Employee.FirstName} {user.Employee.LastName}",
                    approvedAt = timesheet.ApprovedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error approving timesheet", error = ex.Message });
            }
        }

        // ============================================
        // REJECT TIMESHEET
        // ============================================
        [HttpPost("{id}/Reject")]
        public async Task<IActionResult> RejectTimesheet(int id, [FromBody] TimesheetRejectionRequest request)
        {
            try
            {
                // Validate rejection reason
                if (string.IsNullOrWhiteSpace(request.RejectionReason))
                {
                    return BadRequest(new { message = "Rejection reason is required" });
                }

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var user = await _dbContext.Users
                    .Include(u => u.Employee)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user?.Employee == null)
                {
                    return BadRequest(new { message = "Employee record not found" });
                }

                var roles = GetUserRoles();
                var isAdminOrExec = roles.Contains("Admin") || roles.Contains("Executive");
                var isDirector = roles.Contains("Director");

                // Only Directors, Executives, and Admins can reject
                if (!isDirector && !isAdminOrExec)
                {
                    return Forbid();
                }

                var timesheet = await _dbContext.Timesheets
                    .Include(t => t.Employee)
                    .FirstOrDefaultAsync(t => t.TimesheetId == id);

                if (timesheet == null)
                {
                    return NotFound(new { message = "Timesheet not found" });
                }

                // Check status
                if (timesheet.Status != "Submitted")
                {
                    return BadRequest(new { message = "Only Submitted timesheets can be rejected" });
                }

                // Authorization: Directors can only reject their department
                if (isDirector && !isAdminOrExec)
                {
                    if (timesheet.Employee.DepartmentId != user.Employee.DepartmentId)
                    {
                        return Forbid();
                    }
                }

                // Update status
                timesheet.Status = "Rejected";
                timesheet.RejectionReason = request.RejectionReason;
                timesheet.UpdatedAt = DateTime.UtcNow;
                timesheet.ApprovedBy = null;
                timesheet.ApprovedAt = null;

                await _dbContext.SaveChangesAsync();

                // TODO: Send notification to employee with rejection reason

                return Ok(new
                {
                    message = "Timesheet rejected successfully",
                    timesheetId = timesheet.TimesheetId,
                    status = timesheet.Status,
                    rejectionReason = timesheet.RejectionReason,
                    rejectedBy = $"{user.Employee.FirstName} {user.Employee.LastName}"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error rejecting timesheet", error = ex.Message });
            }
        }

        // ============================================
        // CREATE TIMESHEET (From Time Entries)
        // ============================================
        [HttpPost("Generate")]
        public async Task<IActionResult> GenerateTimesheet([FromBody] TimesheetGenerationRequest request)
        {
            try
            {
                var employee = await GetCurrentEmployee();
                if (employee == null)
                {
                    return BadRequest(new { message = "Employee record not found" });
                }

                // Validate dates
                if (request.StartDate >= request.EndDate)
                {
                    return BadRequest(new { message = "End date must be after start date" });
                }

                // Check if timesheet already exists for this period
                var existingTimesheet = await _dbContext.Timesheets
                    .FirstOrDefaultAsync(t =>
                        t.EmployeeId == employee.EmployeeId &&
                        t.StartDate == request.StartDate &&
                        t.EndDate == request.EndDate);

                if (existingTimesheet != null)
                {
                    return BadRequest(new { message = "Timesheet already exists for this period" });
                }

                // Get time entries for the period
                var timeEntries = await _dbContext.TimeEntries
                    .Where(t =>
                        t.EmployeeId == employee.EmployeeId &&
                        t.WorkDate >= request.StartDate &&
                        t.WorkDate <= request.EndDate &&
                        t.Status == "Closed")
                    .OrderBy(t => t.WorkDate)
                    .ToListAsync();

                if (!timeEntries.Any())
                {
                    return BadRequest(new { message = "No completed time entries found for this period" });
                }

                // Calculate totals
                var totalHours = timeEntries.Sum(t => t.TotalHours ?? 0);
                var regularHours = timeEntries.Sum(t => t.RegularHours ?? 0);
                var overtimeHours = timeEntries.Sum(t => t.OvertimeHours ?? 0);

                // Create timesheet
                var timesheet = new Timesheet
                {
                    EmployeeId = employee.EmployeeId,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    TotalHours = totalHours,
                    RegularHours = regularHours,
                    OvertimeHours = overtimeHours,
                    Status = "Draft",
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.Timesheets.Add(timesheet);
                await _dbContext.SaveChangesAsync();

                // Create timesheet entries
                foreach (var timeEntry in timeEntries)
                {
                    var entry = new TimesheetEntry
                    {
                        TimesheetId = timesheet.TimesheetId,
                        WorkDate = timeEntry.WorkDate,
                        TimeEntryId = timeEntry.TimeEntryId,
                        StartTime = timeEntry.ClockInTime,
                        EndTime = timeEntry.ClockOutTime,
                        Hours = timeEntry.TotalHours,
                        IsBillable = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    _dbContext.TimesheetEntries.Add(entry);
                }

                await _dbContext.SaveChangesAsync();

                return Ok(new
                {
                    message = "Timesheet generated successfully",
                    timesheetId = timesheet.TimesheetId,
                    totalHours = timesheet.TotalHours,
                    regularHours = timesheet.RegularHours,
                    overtimeHours = timesheet.OvertimeHours,
                    entryCount = timeEntries.Count,
                    status = timesheet.Status
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error generating timesheet", error = ex.Message });
            }
        }

        // ============================================
        // GET STATISTICS (Dashboard)
        // ============================================
        [HttpGet("Statistics")]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                var employee = await GetCurrentEmployee();
                if (employee == null)
                {
                    return BadRequest(new { message = "Employee record not found" });
                }

                var stats = new
                {
                    // Current period (last 30 days)
                    totalTimesheets = await _dbContext.Timesheets
                        .Where(t => t.EmployeeId == employee.EmployeeId)
                        .CountAsync(),

                    draftTimesheets = await _dbContext.Timesheets
                        .Where(t => t.EmployeeId == employee.EmployeeId && t.Status == "Draft")
                        .CountAsync(),

                    submittedTimesheets = await _dbContext.Timesheets
                        .Where(t => t.EmployeeId == employee.EmployeeId && t.Status == "Submitted")
                        .CountAsync(),

                    approvedTimesheets = await _dbContext.Timesheets
                        .Where(t => t.EmployeeId == employee.EmployeeId && t.Status == "Approved")
                        .CountAsync(),

                    rejectedTimesheets = await _dbContext.Timesheets
                        .Where(t => t.EmployeeId == employee.EmployeeId && t.Status == "Rejected")
                        .CountAsync(),

                    totalHoursThisMonth = await _dbContext.Timesheets
                        .Where(t => t.EmployeeId == employee.EmployeeId &&
                                   t.StartDate.Month == DateTime.UtcNow.Month &&
                                   t.StartDate.Year == DateTime.UtcNow.Year)
                        .SumAsync(t => t.TotalHours ?? 0),

                    overtimeHoursThisMonth = await _dbContext.Timesheets
                        .Where(t => t.EmployeeId == employee.EmployeeId &&
                                   t.StartDate.Month == DateTime.UtcNow.Month &&
                                   t.StartDate.Year == DateTime.UtcNow.Year)
                        .SumAsync(t => t.OvertimeHours ?? 0)
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving statistics", error = ex.Message });
            }
        }

        // ============================================
        // REQUEST MODELS
        // ============================================
        public class TimesheetApprovalRequest
        {
            public string? Comments { get; set; }
        }

        public class TimesheetRejectionRequest
        {
            public string RejectionReason { get; set; } = string.Empty;
        }

        public class TimesheetGenerationRequest
        {
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
        }
    }
}