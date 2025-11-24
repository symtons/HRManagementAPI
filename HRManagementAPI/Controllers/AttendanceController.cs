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
    public class AttendanceController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AttendanceController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================
        // GET MY ATTENDANCE
        // ============================================
        [HttpGet("MyAttendance")]
        public async Task<IActionResult> GetMyAttendance(
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

                var attendance = await _context.Attendance
                    .Where(a => a.EmployeeId == employeeId &&
                               a.AttendanceDate >= start &&
                               a.AttendanceDate <= end)
                    .OrderByDescending(a => a.AttendanceDate)
                    .Select(a => new
                    {
                        a.AttendanceId,
                        a.AttendanceDate,
                        a.Status,
                        a.ClockInTime,
                        a.ClockOutTime,
                        a.WorkingHours,
                        a.IsLate,
                        a.LateMinutes,
                        a.IsEarlyLeave,
                        a.EarlyLeaveMinutes,
                        a.Remarks
                    })
                    .ToListAsync();

                // Calculate statistics
                var stats = new
                {
                    totalDays = attendance.Count,
                    presentDays = attendance.Count(a => a.Status == "Present" || a.Status == "Late"),
                    absentDays = attendance.Count(a => a.Status == "Absent"),
                    lateDays = attendance.Count(a => a.IsLate),
                    totalHours = attendance.Sum(a => a.WorkingHours ?? 0)
                };

                return Ok(new { attendance, stats });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving attendance", error = ex.Message });
            }
        }

        // ============================================
        // GET ALL ATTENDANCE (Manager/Admin)
        // ============================================
        [HttpGet("All")]
        [Authorize(Roles = "Admin,Executive,Director,FieldOperatorManager")]
        public async Task<IActionResult> GetAllAttendance(
            [FromQuery] int? employeeId,
            [FromQuery] int? departmentId,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? status)
        {
            try
            {
                var start = startDate ?? DateTime.Today.AddDays(-30);
                var end = endDate ?? DateTime.Today;

                var query = _context.Attendance
                    .Include(a => a.Employee)
                        .ThenInclude(e => e!.Department)
                    .Where(a => a.AttendanceDate >= start && a.AttendanceDate <= end);

                if (employeeId.HasValue)
                {
                    query = query.Where(a => a.EmployeeId == employeeId.Value);
                }

                if (departmentId.HasValue)
                {
                    query = query.Where(a => a.Employee!.DepartmentId == departmentId.Value);
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(a => a.Status == status);
                }

                var attendance = await query
                    .OrderByDescending(a => a.AttendanceDate)
                    .ThenBy(a => a.Employee!.FirstName)
                    .Select(a => new
                    {
                        a.AttendanceId,
                        employeeId = a.EmployeeId,
                        employeeName = $"{a.Employee!.FirstName} {a.Employee.LastName}",
                        employeeCode = a.Employee.EmployeeCode,
                        department = a.Employee.Department != null ? a.Employee.Department.DepartmentName : null,
                        a.AttendanceDate,
                        a.Status,
                        a.ClockInTime,
                        a.ClockOutTime,
                        a.WorkingHours,
                        a.IsLate,
                        a.LateMinutes,
                        a.IsEarlyLeave,
                        a.EarlyLeaveMinutes,
                        a.Remarks
                    })
                    .ToListAsync();

                return Ok(attendance);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving attendance", error = ex.Message });
            }
        }

        // ============================================
        // GET ATTENDANCE SUMMARY (Reports)
        // ============================================
        [HttpGet("Summary")]
        [Authorize(Roles = "Admin,Executive,Director,FieldOperatorManager")]
        public async Task<IActionResult> GetAttendanceSummary(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] int? departmentId)
        {
            try
            {
                var start = startDate ?? DateTime.Today.AddDays(-30);
                var end = endDate ?? DateTime.Today;

                var query = _context.Attendance
                    .Include(a => a.Employee)
                        .ThenInclude(e => e!.Department)
                    .Where(a => a.AttendanceDate >= start && a.AttendanceDate <= end);

                if (departmentId.HasValue)
                {
                    query = query.Where(a => a.Employee!.DepartmentId == departmentId.Value);
                }

                var allAttendance = await query.ToListAsync();

                // Group by employee
                var summary = allAttendance
                    .GroupBy(a => a.EmployeeId)
                    .Select(g => new
                    {
                        employeeId = g.Key,
                        employeeName = $"{g.First().Employee!.FirstName} {g.First().Employee.LastName}",
                        employeeCode = g.First().Employee!.EmployeeCode,
                        department = g.First().Employee!.Department?.DepartmentName,
                        totalDays = g.Count(),
                        presentDays = g.Count(a => a.Status == "Present" || a.Status == "Late"),
                        absentDays = g.Count(a => a.Status == "Absent"),
                        lateDays = g.Count(a => a.IsLate),
                        leaveDays = g.Count(a => a.Status == "Leave"),
                        totalHours = g.Sum(a => a.WorkingHours ?? 0),
                        averageHoursPerDay = g.Average(a => a.WorkingHours ?? 0),
                        attendanceRate = g.Count() > 0 ? (decimal)g.Count(a => a.Status == "Present" || a.Status == "Late") / g.Count() * 100 : 0
                    })
                    .OrderByDescending(s => s.attendanceRate)
                    .ToList();

                return Ok(summary);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error generating summary", error = ex.Message });
            }
        }

        // ============================================
        // MARK ATTENDANCE MANUALLY (Manager/Admin)
        // ============================================
        [HttpPost("Mark")]
        [Authorize(Roles = "Admin,Executive,Director,FieldOperatorManager")]
        public async Task<IActionResult> MarkAttendance([FromBody] MarkAttendanceRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                // Check if attendance already exists
                var existing = await _context.Attendance
                    .FirstOrDefaultAsync(a => a.EmployeeId == request.EmployeeId &&
                                             a.AttendanceDate.Date == request.AttendanceDate.Date);

                if (existing != null)
                {
                    // Update existing
                    existing.Status = request.Status;
                    existing.Remarks = request.Remarks;
                    existing.ApprovedBy = userId;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Create new
                    var attendance = new Attendance
                    {
                        EmployeeId = request.EmployeeId,
                        AttendanceDate = request.AttendanceDate.Date,
                        Status = request.Status,
                        Remarks = request.Remarks,
                        ApprovedBy = userId
                    };

                    _context.Attendance.Add(attendance);
                }

                await _context.SaveChangesAsync();

                return Ok(new { message = "Attendance marked successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error marking attendance", error = ex.Message });
            }
        }

        // ============================================
        // UPDATE ATTENDANCE
        // ============================================
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Executive,Director,FieldOperatorManager")]
        public async Task<IActionResult> UpdateAttendance(int id, [FromBody] UpdateAttendanceRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                var attendance = await _context.Attendance.FindAsync(id);
                if (attendance == null)
                {
                    return NotFound(new { message = "Attendance record not found" });
                }

                attendance.Status = request.Status ?? attendance.Status;
                attendance.Remarks = request.Remarks ?? attendance.Remarks;
                attendance.ApprovedBy = userId;
                attendance.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Attendance updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating attendance", error = ex.Message });
            }
        }

        // ============================================
        // GET DAILY ATTENDANCE REPORT
        // ============================================
        [HttpGet("DailyReport")]
        [Authorize(Roles = "Admin,Executive,Director,FieldOperatorManager")]
        public async Task<IActionResult> GetDailyReport([FromQuery] DateTime? date)
        {
            try
            {
                var targetDate = date?.Date ?? DateTime.Today;

                var attendance = await _context.Attendance
                    .Include(a => a.Employee)
                        .ThenInclude(e => e!.Department)
                    .Where(a => a.AttendanceDate.Date == targetDate)
                    .Select(a => new
                    {
                        a.AttendanceId,
                        employeeId = a.EmployeeId,
                        employeeName = $"{a.Employee!.FirstName} {a.Employee.LastName}",
                        employeeCode = a.Employee.EmployeeCode,
                        department = a.Employee.Department != null ? a.Employee.Department.DepartmentName : null,
                        a.Status,
                        a.ClockInTime,
                        a.ClockOutTime,
                        a.IsLate,
                        a.LateMinutes
                    })
                    .ToListAsync();

                // Get all active employees to find who hasn't clocked in
                var activeEmployees = await _context.Employees
                    .Where(e => e.EmploymentStatus == "Active")
                    .Select(e => new
                    {
                        e.EmployeeId,
                        employeeName = $"{e.FirstName} {e.LastName}",
                        e.EmployeeCode,
                        department = e.Department != null ? e.Department.DepartmentName : null
                    })
                    .ToListAsync();

                var presentEmployeeIds = attendance.Select(a => a.employeeId).ToList();
                var absentEmployees = activeEmployees
                    .Where(e => !presentEmployeeIds.Contains(e.EmployeeId))
                    .ToList();

                var stats = new
                {
                    totalEmployees = activeEmployees.Count,
                    present = attendance.Count(a => a.Status == "Present" || a.Status == "Late"),
                    absent = absentEmployees.Count,
                    late = attendance.Count(a => a.IsLate),
                    onLeave = attendance.Count(a => a.Status == "Leave")
                };

                return Ok(new
                {
                    date = targetDate,
                    stats,
                    attendance,
                    absentEmployees
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error generating daily report", error = ex.Message });
            }
        }
    }

    // ============================================
    // REQUEST MODELS
    // ============================================
    public class MarkAttendanceRequest
    {
        public int EmployeeId { get; set; }
        public DateTime AttendanceDate { get; set; }
        public string Status { get; set; } = "Present";
        public string? Remarks { get; set; }
    }

    public class UpdateAttendanceRequest
    {
        public string? Status { get; set; }
        public string? Remarks { get; set; }
    }
}
