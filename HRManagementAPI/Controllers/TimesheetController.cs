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
    public class TimesheetController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TimesheetController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================
        // GET MY TIMESHEETS
        // ============================================
        [HttpGet("MyTimesheets")]
        public async Task<IActionResult> GetMyTimesheets()
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

                var timesheets = await _context.Timesheets
                    .Where(t => t.EmployeeId == user.Employee.EmployeeId)
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
                        t.RejectionReason
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
        // GET TIMESHEET BY ID
        // ============================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTimesheet(int id)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var user = await _context.Users
                    .Include(u => u.Employee)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                var timesheet = await _context.Timesheets
                    .Include(t => t.TimesheetEntries)
                        .ThenInclude(te => te.TimeEntry)
                    .FirstOrDefaultAsync(t => t.TimesheetId == id);

                if (timesheet == null)
                {
                    return NotFound(new { message = "Timesheet not found" });
                }

                // Check authorization
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                if (timesheet.EmployeeId != user?.Employee?.EmployeeId &&
                    !new[] { "Admin", "Executive", "Director" }.Contains(role))
                {
                    return Forbid();
                }

                return Ok(new
                {
                    timesheet.TimesheetId,
                    timesheet.EmployeeId,
                    timesheet.StartDate,
                    timesheet.EndDate,
                    timesheet.TotalHours,
                    timesheet.RegularHours,
                    timesheet.OvertimeHours,
                    timesheet.Status,
                    timesheet.SubmittedAt,
                    timesheet.ApprovedAt,
                    timesheet.RejectionReason,
                    entries = timesheet.TimesheetEntries.Select(te => new
                    {
                        te.TimesheetEntryId,
                        te.WorkDate,
                        te.StartTime,
                        te.EndTime,
                        te.Hours,
                        te.TaskDescription,
                        te.IsBillable
                    })
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving timesheet", error = ex.Message });
            }
        }

        // ============================================
        // CREATE TIMESHEET FROM TIME ENTRIES
        // ============================================
        [HttpPost("Generate")]
        public async Task<IActionResult> GenerateTimesheet([FromBody] GenerateTimesheetRequest request)
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

                // Check if timesheet already exists for this period
                var existing = await _context.Timesheets
                    .FirstOrDefaultAsync(t => t.EmployeeId == user.Employee.EmployeeId &&
                                             t.StartDate == request.StartDate &&
                                             t.EndDate == request.EndDate);

                if (existing != null)
                {
                    return BadRequest(new { message = "Timesheet already exists for this period" });
                }

                // Get time entries for the period
                var timeEntries = await _context.TimeEntries
                    .Where(t => t.EmployeeId == user.Employee.EmployeeId &&
                               t.WorkDate >= request.StartDate &&
                               t.WorkDate <= request.EndDate &&
                               t.Status == "Closed")
                    .ToListAsync();

                var timesheet = new Timesheet
                {
                    EmployeeId = user.Employee.EmployeeId,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    Status = "Draft"
                };

                _context.Timesheets.Add(timesheet);
                await _context.SaveChangesAsync();

                // Create timesheet entries from time entries
                foreach (var te in timeEntries)
                {
                    var entry = new TimesheetEntry
                    {
                        TimesheetId = timesheet.TimesheetId,
                        WorkDate = te.WorkDate,
                        TimeEntryId = te.TimeEntryId,
                        StartTime = te.ClockInTime,
                        EndTime = te.ClockOutTime,
                        Hours = te.TotalHours,
                        IsBillable = true
                    };

                    _context.TimesheetEntries.Add(entry);
                }

                // Calculate totals
                timesheet.TotalHours = timeEntries.Sum(t => t.TotalHours ?? 0);
                timesheet.RegularHours = timeEntries.Sum(t => t.RegularHours ?? 0);
                timesheet.OvertimeHours = timeEntries.Sum(t => t.OvertimeHours ?? 0);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Timesheet generated successfully",
                    timesheetId = timesheet.TimesheetId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error generating timesheet", error = ex.Message });
            }
        }

        // ============================================
        // ADD MANUAL TIMESHEET ENTRY
        // ============================================
        [HttpPost("{id}/Entry")]
        public async Task<IActionResult> AddTimesheetEntry(int id, [FromBody] TimesheetEntryRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var user = await _context.Users
                    .Include(u => u.Employee)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                var timesheet = await _context.Timesheets.FindAsync(id);

                if (timesheet == null)
                {
                    return NotFound(new { message = "Timesheet not found" });
                }

                if (timesheet.EmployeeId != user?.Employee?.EmployeeId)
                {
                    return Forbid();
                }

                if (timesheet.Status != "Draft")
                {
                    return BadRequest(new { message = "Cannot add entries to submitted timesheet" });
                }

                var entry = new TimesheetEntry
                {
                    TimesheetId = id,
                    WorkDate = request.WorkDate,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    Hours = request.Hours,
                    TaskDescription = request.TaskDescription,
                    IsBillable = request.IsBillable ?? true
                };

                _context.TimesheetEntries.Add(entry);

                // Recalculate timesheet totals
                var entries = await _context.TimesheetEntries
                    .Where(te => te.TimesheetId == id)
                    .ToListAsync();

                timesheet.TotalHours = entries.Sum(e => e.Hours ?? 0) + entry.Hours;
                timesheet.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Entry added successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error adding entry", error = ex.Message });
            }
        }

        // ============================================
        // SUBMIT TIMESHEET
        // ============================================
        [HttpPost("{id}/Submit")]
        public async Task<IActionResult> SubmitTimesheet(int id)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var user = await _context.Users
                    .Include(u => u.Employee)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                var timesheet = await _context.Timesheets.FindAsync(id);

                if (timesheet == null)
                {
                    return NotFound(new { message = "Timesheet not found" });
                }

                if (timesheet.EmployeeId != user?.Employee?.EmployeeId)
                {
                    return Forbid();
                }

                if (timesheet.Status != "Draft")
                {
                    return BadRequest(new { message = "Timesheet already submitted" });
                }

                timesheet.Status = "Submitted";
                timesheet.SubmittedAt = DateTime.UtcNow;
                timesheet.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Timesheet submitted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error submitting timesheet", error = ex.Message });
            }
        }

        // ============================================
        // APPROVE TIMESHEET (Manager/Admin)
        // ============================================
        [HttpPost("{id}/Approve")]
        [Authorize(Roles = "Admin,Executive,Director,FieldOperatorManager")]
        public async Task<IActionResult> ApproveTimesheet(int id)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                var timesheet = await _context.Timesheets.FindAsync(id);

                if (timesheet == null)
                {
                    return NotFound(new { message = "Timesheet not found" });
                }

                if (timesheet.Status != "Submitted")
                {
                    return BadRequest(new { message = "Only submitted timesheets can be approved" });
                }

                timesheet.Status = "Approved";
                timesheet.ApprovedBy = userId;
                timesheet.ApprovedAt = DateTime.UtcNow;
                timesheet.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Timesheet approved successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error approving timesheet", error = ex.Message });
            }
        }

        // ============================================
        // REJECT TIMESHEET (Manager/Admin)
        // ============================================
        [HttpPost("{id}/Reject")]
        [Authorize(Roles = "Admin,Executive,Director,FieldOperatorManager")]
        public async Task<IActionResult> RejectTimesheet(int id, [FromBody] RejectTimesheetRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                var timesheet = await _context.Timesheets.FindAsync(id);

                if (timesheet == null)
                {
                    return NotFound(new { message = "Timesheet not found" });
                }

                if (timesheet.Status != "Submitted")
                {
                    return BadRequest(new { message = "Only submitted timesheets can be rejected" });
                }

                timesheet.Status = "Rejected";
                timesheet.ApprovedBy = userId;
                timesheet.RejectionReason = request.Reason;
                timesheet.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Timesheet rejected" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error rejecting timesheet", error = ex.Message });
            }
        }

        // ============================================
        // GET PENDING TIMESHEETS (Manager/Admin)
        // ============================================
        [HttpGet("Pending")]
        [Authorize(Roles = "Admin,Executive,Director,FieldOperatorManager")]
        public async Task<IActionResult> GetPendingTimesheets()
        {
            try
            {
                var timesheets = await _context.Timesheets
                    .Include(t => t.Employee)
                        .ThenInclude(e => e!.Department)
                    .Where(t => t.Status == "Submitted")
                    .OrderBy(t => t.SubmittedAt)
                    .Select(t => new
                    {
                        t.TimesheetId,
                        employeeId = t.EmployeeId,
                        employeeName = $"{t.Employee!.FirstName} {t.Employee.LastName}",
                        employeeCode = t.Employee.EmployeeCode,
                        department = t.Employee.Department != null ? t.Employee.Department.DepartmentName : null,
                        t.StartDate,
                        t.EndDate,
                        t.TotalHours,
                        t.RegularHours,
                        t.OvertimeHours,
                        t.SubmittedAt
                    })
                    .ToListAsync();

                return Ok(timesheets);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving pending timesheets", error = ex.Message });
            }
        }
    }

    // ============================================
    // REQUEST MODELS
    // ============================================
    public class GenerateTimesheetRequest
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class TimesheetEntryRequest
    {
        public DateTime WorkDate { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public decimal? Hours { get; set; }
        public string? TaskDescription { get; set; }
        public bool? IsBillable { get; set; }
    }

    public class RejectTimesheetRequest
    {
        public string Reason { get; set; } = string.Empty;
    }
}
