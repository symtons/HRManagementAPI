using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRManagementAPI.Data;
using HRManagementAPI.Models;
using System.Security.Claims;

namespace HRManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TimeEntryController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TimeEntryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================
        // CLOCK IN
        // ============================================
        [HttpPost("ClockIn")]
        public async Task<IActionResult> ClockIn([FromBody] ClockInRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var user = await _context.Users
                    .Include(u => u.Employee)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user?.Employee == null)
                {
                    return BadRequest(new { message = "Employee record not found" });
                }

                var employeeId = user.Employee.EmployeeId;
                var today = DateTime.Today;

                // Check if already clocked in today
                var existingEntry = await _context.TimeEntries
                    .FirstOrDefaultAsync(t => t.EmployeeId == employeeId &&
                                             t.WorkDate.Date == today &&
                                             t.ClockOutTime == null);

                if (existingEntry != null)
                {
                    return BadRequest(new { message = "Already clocked in. Please clock out first." });
                }

                // Get employee's current shift
                var employeeShift = await _context.EmployeeShifts
                    .Include(es => es.Shift)
                    .Where(es => es.EmployeeId == employeeId &&
                                es.IsActive &&
                                es.EffectiveDate <= today &&
                                (es.EndDate == null || es.EndDate >= today))
                    .OrderByDescending(es => es.EffectiveDate)
                    .FirstOrDefaultAsync();

                var timeEntry = new TimeEntry
                {
                    EmployeeId = employeeId,
                    ClockInTime = DateTime.Now,
                    WorkDate = today,
                    ShiftId = employeeShift?.ShiftId,
                    ClockInLocation = request.Location,
                    Notes = request.Notes,
                    Status = "Open"
                };

                _context.TimeEntries.Add(timeEntry);

                // Also create/update attendance record
                var attendance = await _context.Attendance
                    .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.AttendanceDate.Date == today);

                if (attendance == null)
                {
                    attendance = new Attendance
                    {
                        EmployeeId = employeeId,
                        AttendanceDate = today,
                        Status = "Present",
                        ClockInTime = timeEntry.ClockInTime
                    };

                    // Check if late
                    if (employeeShift?.Shift != null)
                    {
                        var shiftStartTime = today.Add(employeeShift.Shift.StartTime);
                        if (timeEntry.ClockInTime > shiftStartTime.AddMinutes(15)) // 15 min grace period
                        {
                            attendance.IsLate = true;
                            attendance.LateMinutes = (int)(timeEntry.ClockInTime - shiftStartTime).TotalMinutes;
                            attendance.Status = "Late";
                        }
                    }

                    _context.Attendance.Add(attendance);
                }
                else
                {
                    attendance.ClockInTime = timeEntry.ClockInTime;
                    attendance.Status = attendance.IsLate ? "Late" : "Present";
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Clocked in successfully",
                    timeEntry = new
                    {
                        timeEntry.TimeEntryId,
                        timeEntry.ClockInTime,
                        timeEntry.WorkDate,
                        shift = employeeShift?.Shift?.ShiftName,
                        isLate = attendance.IsLate,
                        lateMinutes = attendance.LateMinutes
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
        public async Task<IActionResult> ClockOut([FromBody] ClockOutRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var user = await _context.Users
                    .Include(u => u.Employee)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user?.Employee == null)
                {
                    return BadRequest(new { message = "Employee record not found" });
                }

                var employeeId = user.Employee.EmployeeId;
                var today = DateTime.Today;

                // Find open time entry
                var timeEntry = await _context.TimeEntries
                    .Include(t => t.Shift)
                    .FirstOrDefaultAsync(t => t.EmployeeId == employeeId &&
                                             t.WorkDate.Date == today &&
                                             t.ClockOutTime == null);

                if (timeEntry == null)
                {
                    return BadRequest(new { message = "No active clock-in found. Please clock in first." });
                }

                timeEntry.ClockOutTime = DateTime.Now;
                timeEntry.ClockOutLocation = request.Location;
                timeEntry.BreakMinutes = request.BreakMinutes ?? timeEntry.Shift?.BreakDuration ?? 0;
                timeEntry.Status = "Closed";

                // Calculate hours
                var totalMinutes = (timeEntry.ClockOutTime.Value - timeEntry.ClockInTime).TotalMinutes;
                var workMinutes = totalMinutes - timeEntry.BreakMinutes;
                timeEntry.TotalHours = Math.Round((decimal)(workMinutes / 60), 2);

                // Calculate regular and overtime hours
                var expectedHours = timeEntry.Shift?.WorkingHours ?? 8m;
                if (timeEntry.TotalHours <= expectedHours)
                {
                    timeEntry.RegularHours = timeEntry.TotalHours;
                    timeEntry.OvertimeHours = 0;
                }
                else
                {
                    timeEntry.RegularHours = expectedHours;
                    timeEntry.OvertimeHours = timeEntry.TotalHours - expectedHours;
                }

                timeEntry.UpdatedAt = DateTime.UtcNow;

                // Update attendance
                var attendance = await _context.Attendance
                    .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.AttendanceDate.Date == today);

                if (attendance != null)
                {
                    attendance.ClockOutTime = timeEntry.ClockOutTime;
                    attendance.WorkingHours = timeEntry.TotalHours;

                    // Check early leave
                    if (timeEntry.Shift != null)
                    {
                        var shiftEndTime = today.Add(timeEntry.Shift.EndTime);
                        if (timeEntry.ClockOutTime < shiftEndTime.AddMinutes(-15)) // 15 min grace period
                        {
                            attendance.IsEarlyLeave = true;
                            attendance.EarlyLeaveMinutes = (int)(shiftEndTime - timeEntry.ClockOutTime.Value).TotalMinutes;
                        }
                    }

                    attendance.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Clocked out successfully",
                    timeEntry = new
                    {
                        timeEntry.TimeEntryId,
                        timeEntry.ClockInTime,
                        timeEntry.ClockOutTime,
                        timeEntry.TotalHours,
                        timeEntry.RegularHours,
                        timeEntry.OvertimeHours,
                        isEarlyLeave = attendance?.IsEarlyLeave ?? false
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
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var user = await _context.Users
                    .Include(u => u.Employee)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user?.Employee == null)
                {
                    return BadRequest(new { message = "Employee record not found" });
                }

                var employeeId = user.Employee.EmployeeId;
                var today = DateTime.Today;

                var timeEntry = await _context.TimeEntries
                    .Include(t => t.Shift)
                    .FirstOrDefaultAsync(t => t.EmployeeId == employeeId &&
                                             t.WorkDate.Date == today &&
                                             t.ClockOutTime == null);

                if (timeEntry == null)
                {
                    return Ok(new { isClockedIn = false });
                }

                var currentDuration = DateTime.Now - timeEntry.ClockInTime;

                return Ok(new
                {
                    isClockedIn = true,
                    timeEntry = new
                    {
                        timeEntry.TimeEntryId,
                        timeEntry.ClockInTime,
                        shift = timeEntry.Shift?.ShiftName,
                        currentDuration = $"{(int)currentDuration.TotalHours}h {currentDuration.Minutes}m"
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error getting status", error = ex.Message });
            }
        }

        // ============================================
        // GET MY TIME ENTRIES
        // ============================================
        [HttpGet("MyEntries")]
        public async Task<IActionResult> GetMyTimeEntries(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var user = await _context.Users
                    .Include(u => u.Employee)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user?.Employee == null)
                {
                    return BadRequest(new { message = "Employee record not found" });
                }

                var employeeId = user.Employee.EmployeeId;
                var start = startDate ?? DateTime.Today.AddDays(-30);
                var end = endDate ?? DateTime.Today;

                var entries = await _context.TimeEntries
                    .Include(t => t.Shift)
                    .Where(t => t.EmployeeId == employeeId &&
                               t.WorkDate >= start &&
                               t.WorkDate <= end)
                    .OrderByDescending(t => t.WorkDate)
                    .Select(t => new
                    {
                        t.TimeEntryId,
                        t.WorkDate,
                        t.ClockInTime,
                        t.ClockOutTime,
                        t.TotalHours,
                        t.RegularHours,
                        t.OvertimeHours,
                        t.Status,
                        shift = t.Shift != null ? t.Shift.ShiftName : null
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
        // GET ALL TIME ENTRIES (Manager/Admin)
        // ============================================
        [HttpGet("All")]
        [Authorize(Roles = "Admin,Executive,Director")]
        public async Task<IActionResult> GetAllTimeEntries(
            [FromQuery] int? employeeId,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? status)
        {
            try
            {
                var start = startDate ?? DateTime.Today.AddDays(-30);
                var end = endDate ?? DateTime.Today;

                var query = _context.TimeEntries
                    .Include(t => t.Employee)
                    .Include(t => t.Shift)
                    .Where(t => t.WorkDate >= start && t.WorkDate <= end);

                if (employeeId.HasValue)
                {
                    query = query.Where(t => t.EmployeeId == employeeId.Value);
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(t => t.Status == status);
                }

                var entries = await query
                    .OrderByDescending(t => t.WorkDate)
                    .Select(t => new
                    {
                        t.TimeEntryId,
                        employeeId = t.EmployeeId,
                        employeeName = $"{t.Employee!.FirstName} {t.Employee.LastName}",
                        employeeCode = t.Employee.EmployeeCode,
                        t.WorkDate,
                        t.ClockInTime,
                        t.ClockOutTime,
                        t.TotalHours,
                        t.RegularHours,
                        t.OvertimeHours,
                        t.Status,
                        shift = t.Shift != null ? t.Shift.ShiftName : null
                    })
                    .ToListAsync();

                return Ok(entries);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving time entries", error = ex.Message });
            }
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
        public int? BreakMinutes { get; set; }
    }
}
