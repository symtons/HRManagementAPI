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
    public class TimeentryController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public TimeentryController(ApplicationDbContext context)
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
        // CLOCK IN
        // ============================================
        [HttpPost("ClockIn")]
        public async Task<IActionResult> ClockIn([FromBody] ClockInRequest? request = null)
        {
            try
            {
                var employee = await GetCurrentEmployee();
                if (employee == null)
                {
                    return BadRequest(new { message = "Employee record not found" });
                }

                // Check if already clocked in
                var existingEntry = await _dbContext.TimeEntries
                    .FirstOrDefaultAsync(t =>
                        t.EmployeeId == employee.EmployeeId &&
                        t.WorkDate == DateTime.UtcNow.Date &&
                        t.Status == "Open");

                if (existingEntry != null)
                {
                    return BadRequest(new { message = "Already clocked in" });
                }

                // Create new time entry
                var timeEntry = new TimeEntry
                {
                    EmployeeId = employee.EmployeeId,
                    WorkDate = DateTime.UtcNow.Date,
                    ClockInTime = DateTime.UtcNow,
                    ClockInLocation = request?.Location,
                    Notes = request?.Notes,
                    Status = "Open",
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.TimeEntries.Add(timeEntry);
                await _dbContext.SaveChangesAsync();

                return Ok(new
                {
                    message = "Clocked in successfully",
                    timeEntry = new
                    {
                        timeEntryId = timeEntry.TimeEntryId,
                        clockInTime = timeEntry.ClockInTime,
                        workDate = timeEntry.WorkDate
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error clocking in", error = ex.Message });
            }
        }

        // ============================================
        // CLOCK OUT
        // ============================================
        [HttpPost("ClockOut")]
        public async Task<IActionResult> ClockOut([FromBody] ClockOutRequest? request = null)
        {
            try
            {
                var employee = await GetCurrentEmployee();
                if (employee == null)
                {
                    return BadRequest(new { message = "Employee record not found" });
                }

                // Find open time entry
                var timeEntry = await _dbContext.TimeEntries
                    .FirstOrDefaultAsync(t =>
                        t.EmployeeId == employee.EmployeeId &&
                        t.WorkDate == DateTime.UtcNow.Date &&
                        t.Status == "Open");

                if (timeEntry == null)
                {
                    return BadRequest(new { message = "No open time entry found. Please clock in first." });
                }

                // Update time entry
                var clockOutTime = DateTime.UtcNow;
                timeEntry.ClockOutTime = clockOutTime;
                timeEntry.ClockOutLocation = request?.Location;
                timeEntry.BreakMinutes = request?.BreakMinutes ?? 0;

                // Calculate hours
                var workDuration = clockOutTime - timeEntry.ClockInTime;
                var totalMinutes = workDuration.TotalMinutes - timeEntry.BreakMinutes;
                var totalHours = Math.Round(totalMinutes / 60.0, 2);

                // Calculate regular and overtime
                var regularHours = Math.Min(totalHours, 8);
                var overtimeHours = Math.Max(0, totalHours - 8);

                timeEntry.TotalHours = (decimal)totalHours;
                timeEntry.RegularHours = (decimal)regularHours;
                timeEntry.OvertimeHours = (decimal)overtimeHours;
                timeEntry.Status = "Closed";
                timeEntry.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                return Ok(new
                {
                    message = "Clocked out successfully",
                    timeEntry = new
                    {
                        timeEntryId = timeEntry.TimeEntryId,
                        clockInTime = timeEntry.ClockInTime,
                        clockOutTime = timeEntry.ClockOutTime,
                        totalHours = timeEntry.TotalHours,
                        regularHours = timeEntry.RegularHours,
                        overtimeHours = timeEntry.OvertimeHours,
                        breakMinutes = timeEntry.BreakMinutes
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error clocking out", error = ex.Message });
            }
        }

        // ============================================
        // GET CURRENT STATUS
        // ============================================
        [HttpGet("CurrentStatus")]
        public async Task<IActionResult> GetCurrentStatus()
        {
            try
            {
                var employee = await GetCurrentEmployee();
                if (employee == null)
                {
                    return BadRequest(new { message = "Employee record not found" });
                }

                // Check for open time entry
                var openEntry = await _dbContext.TimeEntries
                    .FirstOrDefaultAsync(t =>
                        t.EmployeeId == employee.EmployeeId &&
                        t.WorkDate == DateTime.UtcNow.Date &&
                        t.Status == "Open");

                var isClockedIn = openEntry != null;

                return Ok(new
                {
                    isClockedIn,
                    timeEntry = isClockedIn ? new
                    {
                        timeEntryId = openEntry!.TimeEntryId,
                        clockInTime = openEntry.ClockInTime,
                        workDate = openEntry.WorkDate,
                        clockInLocation = openEntry.ClockInLocation,
                        notes = openEntry.Notes
                    } : null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error getting status", error = ex.Message });
            }
        }

        // ============================================
        // GET MY ENTRIES
        // ============================================
        [HttpGet("MyEntries")]
        public async Task<IActionResult> GetMyTimeEntries(
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

                var query = _dbContext.TimeEntries
                    .Where(t => t.EmployeeId == employee.EmployeeId);

                if (startDate.HasValue)
                {
                    query = query.Where(t => t.WorkDate >= startDate.Value.Date);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(t => t.WorkDate <= endDate.Value.Date);
                }

                var entries = await query
                    .OrderByDescending(t => t.WorkDate)
                    .ThenByDescending(t => t.ClockInTime)
                    .Select(t => new
                    {
                        t.TimeEntryId,
                        t.WorkDate,
                        t.ClockInTime,
                        t.ClockOutTime,
                        t.TotalHours,
                        t.RegularHours,
                        t.OvertimeHours,
                        t.BreakMinutes,
                        t.Status,
                        t.ClockInLocation,
                        t.ClockOutLocation,
                        t.Notes
                    })
                    .ToListAsync();

                return Ok(entries);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving time entries", error = ex.Message });
            }
        }

        // ============================================
        // GET ALL ENTRIES (Admin/Director)
        // ============================================
        [HttpGet("All")]
        public async Task<IActionResult> GetAllTimeEntries(
            [FromQuery] int? departmentId = null,
            [FromQuery] int? employeeId = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? status = null)
        {
            try
            {
                var employee = await GetCurrentEmployee();
                if (employee == null)
                {
                    return BadRequest(new { message = "Employee record not found" });
                }

                var roles = GetUserRoles();
                var isAdminOrExec = roles.Contains("Admin") || roles.Contains("Executive");
                var isDirector = roles.Contains("Director");

                var query = _dbContext.TimeEntries
                    .Include(t => t.Employee)
                        .ThenInclude(e => e.Department)
                    .AsQueryable();

                // Role-based filtering
                if (isDirector && !isAdminOrExec)
                {
                    query = query.Where(t => t.Employee!.DepartmentId == employee.DepartmentId);
                }
                else if (!isAdminOrExec)
                {
                    return Forbid();
                }

                // Apply filters
                if (departmentId.HasValue)
                {
                    query = query.Where(t => t.Employee!.DepartmentId == departmentId.Value);
                }

                if (employeeId.HasValue)
                {
                    query = query.Where(t => t.EmployeeId == employeeId.Value);
                }

                if (startDate.HasValue)
                {
                    query = query.Where(t => t.WorkDate >= startDate.Value.Date);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(t => t.WorkDate <= endDate.Value.Date);
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(t => t.Status == status);
                }

                var entries = await query
                    .OrderByDescending(t => t.WorkDate)
                    .ThenByDescending(t => t.ClockInTime)
                    .Select(t => new
                    {
                        t.TimeEntryId,
                        t.EmployeeId,
                        employeeName = $"{t.Employee!.FirstName} {t.Employee.LastName}",
                        employeeCode = t.Employee.EmployeeCode,
                        departmentName = t.Employee.Department != null ? t.Employee.Department.DepartmentName : null,
                        t.WorkDate,
                        t.ClockInTime,
                        t.ClockOutTime,
                        t.TotalHours,
                        t.RegularHours,
                        t.OvertimeHours,
                        t.BreakMinutes,
                        t.Status,
                        t.ClockInLocation,
                        t.ClockOutLocation,
                        t.Notes
                    })
                    .ToListAsync();

                return Ok(entries);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving time entries", error = ex.Message });
            }
        }

        // ============================================
        // MANUAL TIME ENTRY (for corrections/missed punches)
        // ============================================
        [HttpPost("Manual")]
        public async Task<IActionResult> AddManualTimeEntry([FromBody] ManualEntryRequest request)
        {
            try
            {
                var employee = await GetCurrentEmployee();
                if (employee == null)
                {
                    return BadRequest(new { message = "Employee record not found" });
                }

                // Validate times
                if (request.ClockInTime >= request.ClockOutTime)
                {
                    return BadRequest(new { message = "Clock out time must be after clock in time" });
                }

                // Check for existing entry on same date
                var existingEntry = await _dbContext.TimeEntries
                    .FirstOrDefaultAsync(t =>
                        t.EmployeeId == employee.EmployeeId &&
                        t.WorkDate == request.WorkDate.Date);

                if (existingEntry != null)
                {
                    return BadRequest(new { message = "Time entry already exists for this date" });
                }

                // Calculate hours
                var workDuration = request.ClockOutTime - request.ClockInTime;
                var totalMinutes = workDuration.TotalMinutes - request.BreakMinutes;
                var totalHours = Math.Round(totalMinutes / 60.0, 2);
                var regularHours = Math.Min(totalHours, 8);
                var overtimeHours = Math.Max(0, totalHours - 8);

                // Create time entry
                var timeEntry = new TimeEntry
                {
                    EmployeeId = employee.EmployeeId,
                    WorkDate = request.WorkDate.Date,
                    ClockInTime = request.ClockInTime,
                    ClockOutTime = request.ClockOutTime,
                    TotalHours = (decimal)totalHours,
                    RegularHours = (decimal)regularHours,
                    OvertimeHours = (decimal)overtimeHours,
                    BreakMinutes = request.BreakMinutes,
                    Notes = $"Manual entry: {request.Notes}",
                    Status = "Closed",
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.TimeEntries.Add(timeEntry);

                // Also create attendance record
                var attendance = new Attendance
                {
                    EmployeeId = employee.EmployeeId,
                    AttendanceDate = request.WorkDate.Date,
                    Status = "Present",
                    ClockInTime = request.ClockInTime,
                    ClockOutTime = request.ClockOutTime,
                    WorkingHours = (decimal)totalHours,
                    IsLate = false,
                    IsEarlyLeave = false,
                    Remarks = $"Manual entry: {request.Notes}",
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.Attendance.Add(attendance);
                await _dbContext.SaveChangesAsync();

                return Ok(new { message = "Manual time entry added successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error adding manual entry", error = ex.Message });
            }
        }

        // ============================================
        // REQUEST MODELS
        // ============================================
        public class ClockInRequest
        {
            public string? Location { get; set; }
            public string? Notes { get; set; }
        }

        public class ClockOutRequest
        {
            public string? Location { get; set; }
            public int BreakMinutes { get; set; }
        }

        public class ManualEntryRequest
        {
            public DateTime WorkDate { get; set; }
            public DateTime ClockInTime { get; set; }
            public DateTime ClockOutTime { get; set; }
            public int BreakMinutes { get; set; }
            public string? Notes { get; set; }
        }
    }
}