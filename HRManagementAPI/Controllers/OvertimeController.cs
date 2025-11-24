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
    public class OvertimeController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public OvertimeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================
        // GET MY OVERTIME REQUESTS
        // ============================================
        [HttpGet("MyRequests")]
        public async Task<IActionResult> GetMyOvertimeRequests()
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

                var requests = await _context.OvertimeRequests
                    .Where(or => or.EmployeeId == user.Employee.EmployeeId)
                    .OrderByDescending(or => or.OvertimeDate)
                    .Select(or => new
                    {
                        or.OvertimeRequestId,
                        or.RequestDate,
                        or.OvertimeDate,
                        or.StartTime,
                        or.EndTime,
                        or.RequestedHours,
                        or.Reason,
                        or.Status,
                        or.ReviewedAt,
                        or.ReviewComments
                    })
                    .ToListAsync();

                return Ok(requests);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving overtime requests", error = ex.Message });
            }
        }

        // ============================================
        // CREATE OVERTIME REQUEST
        // ============================================
        [HttpPost]
        public async Task<IActionResult> CreateOvertimeRequest([FromBody] OvertimeRequestModel request)
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

                // Calculate hours
                var hours = (request.EndTime - request.StartTime).TotalHours;

                if (hours <= 0)
                {
                    return BadRequest(new { message = "End time must be after start time" });
                }

                var overtimeRequest = new OvertimeRequest
                {
                    EmployeeId = user.Employee.EmployeeId,
                    RequestDate = DateTime.Today,
                    OvertimeDate = request.OvertimeDate,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    RequestedHours = Math.Round((decimal)hours, 2),
                    Reason = request.Reason,
                    Status = "Pending"
                };

                _context.OvertimeRequests.Add(overtimeRequest);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Overtime request submitted successfully",
                    overtimeRequestId = overtimeRequest.OvertimeRequestId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error creating overtime request", error = ex.Message });
            }
        }

        // ============================================
        // UPDATE OVERTIME REQUEST
        // ============================================
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOvertimeRequest(int id, [FromBody] OvertimeRequestModel request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var user = await _context.Users
                    .Include(u => u.Employee)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                var overtimeRequest = await _context.OvertimeRequests.FindAsync(id);

                if (overtimeRequest == null)
                {
                    return NotFound(new { message = "Overtime request not found" });
                }

                if (overtimeRequest.EmployeeId != user?.Employee?.EmployeeId)
                {
                    return Forbid();
                }

                if (overtimeRequest.Status != "Pending")
                {
                    return BadRequest(new { message = "Cannot update a reviewed overtime request" });
                }

                // Calculate hours
                var hours = (request.EndTime - request.StartTime).TotalHours;

                if (hours <= 0)
                {
                    return BadRequest(new { message = "End time must be after start time" });
                }

                overtimeRequest.OvertimeDate = request.OvertimeDate;
                overtimeRequest.StartTime = request.StartTime;
                overtimeRequest.EndTime = request.EndTime;
                overtimeRequest.RequestedHours = Math.Round((decimal)hours, 2);
                overtimeRequest.Reason = request.Reason;
                overtimeRequest.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Overtime request updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating overtime request", error = ex.Message });
            }
        }

        // ============================================
        // DELETE OVERTIME REQUEST
        // ============================================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOvertimeRequest(int id)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var user = await _context.Users
                    .Include(u => u.Employee)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                var overtimeRequest = await _context.OvertimeRequests.FindAsync(id);

                if (overtimeRequest == null)
                {
                    return NotFound(new { message = "Overtime request not found" });
                }

                if (overtimeRequest.EmployeeId != user?.Employee?.EmployeeId)
                {
                    return Forbid();
                }

                if (overtimeRequest.Status != "Pending")
                {
                    return BadRequest(new { message = "Cannot delete a reviewed overtime request" });
                }

                _context.OvertimeRequests.Remove(overtimeRequest);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Overtime request deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error deleting overtime request", error = ex.Message });
            }
        }

        // ============================================
        // GET PENDING REQUESTS (Manager/Admin)
        // ============================================
        [HttpGet("Pending")]
        [Authorize(Roles = "Admin,Executive,Director,FieldOperatorManager")]
        public async Task<IActionResult> GetPendingRequests([FromQuery] int? departmentId)
        {
            try
            {
                var query = _context.OvertimeRequests
                    .Include(or => or.Employee)
                        .ThenInclude(e => e!.Department)
                    .Where(or => or.Status == "Pending");

                if (departmentId.HasValue)
                {
                    query = query.Where(or => or.Employee!.DepartmentId == departmentId.Value);
                }

                var requests = await query
                    .OrderBy(or => or.OvertimeDate)
                    .Select(or => new
                    {
                        or.OvertimeRequestId,
                        employeeId = or.EmployeeId,
                        employeeName = $"{or.Employee!.FirstName} {or.Employee.LastName}",
                        employeeCode = or.Employee.EmployeeCode,
                        department = or.Employee.Department != null ? or.Employee.Department.DepartmentName : null,
                        or.RequestDate,
                        or.OvertimeDate,
                        or.StartTime,
                        or.EndTime,
                        or.RequestedHours,
                        or.Reason
                    })
                    .ToListAsync();

                return Ok(requests);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving pending requests", error = ex.Message });
            }
        }

        // ============================================
        // GET ALL OVERTIME REQUESTS (Manager/Admin)
        // ============================================
        [HttpGet("All")]
        [Authorize(Roles = "Admin,Executive,Director,FieldOperatorManager")]
        public async Task<IActionResult> GetAllOvertimeRequests(
            [FromQuery] int? employeeId,
            [FromQuery] int? departmentId,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? status)
        {
            try
            {
                var query = _context.OvertimeRequests
                    .Include(or => or.Employee)
                        .ThenInclude(e => e!.Department)
                    .AsQueryable();

                if (employeeId.HasValue)
                {
                    query = query.Where(or => or.EmployeeId == employeeId.Value);
                }

                if (departmentId.HasValue)
                {
                    query = query.Where(or => or.Employee!.DepartmentId == departmentId.Value);
                }

                if (startDate.HasValue)
                {
                    query = query.Where(or => or.OvertimeDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(or => or.OvertimeDate <= endDate.Value);
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(or => or.Status == status);
                }

                var requests = await query
                    .OrderByDescending(or => or.OvertimeDate)
                    .Select(or => new
                    {
                        or.OvertimeRequestId,
                        employeeId = or.EmployeeId,
                        employeeName = $"{or.Employee!.FirstName} {or.Employee.LastName}",
                        employeeCode = or.Employee.EmployeeCode,
                        department = or.Employee.Department != null ? or.Employee.Department.DepartmentName : null,
                        or.RequestDate,
                        or.OvertimeDate,
                        or.StartTime,
                        or.EndTime,
                        or.RequestedHours,
                        or.Reason,
                        or.Status,
                        or.ReviewedAt,
                        or.ReviewComments
                    })
                    .ToListAsync();

                return Ok(requests);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving overtime requests", error = ex.Message });
            }
        }

        // ============================================
        // APPROVE OVERTIME REQUEST
        // ============================================
        [HttpPost("{id}/Approve")]
        [Authorize(Roles = "Admin,Executive,Director,FieldOperatorManager")]
        public async Task<IActionResult> ApproveOvertimeRequest(int id, [FromBody] ReviewOvertimeRequest? request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                var overtimeRequest = await _context.OvertimeRequests.FindAsync(id);

                if (overtimeRequest == null)
                {
                    return NotFound(new { message = "Overtime request not found" });
                }

                if (overtimeRequest.Status != "Pending")
                {
                    return BadRequest(new { message = "Only pending requests can be approved" });
                }

                overtimeRequest.Status = "Approved";
                overtimeRequest.ReviewedBy = userId;
                overtimeRequest.ReviewedAt = DateTime.UtcNow;
                overtimeRequest.ReviewComments = request?.Comments;
                overtimeRequest.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Overtime request approved successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error approving overtime request", error = ex.Message });
            }
        }

        // ============================================
        // REJECT OVERTIME REQUEST
        // ============================================
        [HttpPost("{id}/Reject")]
        [Authorize(Roles = "Admin,Executive,Director,FieldOperatorManager")]
        public async Task<IActionResult> RejectOvertimeRequest(int id, [FromBody] ReviewOvertimeRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                var overtimeRequest = await _context.OvertimeRequests.FindAsync(id);

                if (overtimeRequest == null)
                {
                    return NotFound(new { message = "Overtime request not found" });
                }

                if (overtimeRequest.Status != "Pending")
                {
                    return BadRequest(new { message = "Only pending requests can be rejected" });
                }

                overtimeRequest.Status = "Rejected";
                overtimeRequest.ReviewedBy = userId;
                overtimeRequest.ReviewedAt = DateTime.UtcNow;
                overtimeRequest.ReviewComments = request.Comments;
                overtimeRequest.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Overtime request rejected" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error rejecting overtime request", error = ex.Message });
            }
        }

        // ============================================
        // GET OVERTIME SUMMARY (Reports)
        // ============================================
        [HttpGet("Summary")]
        [Authorize(Roles = "Admin,Executive,Director")]
        public async Task<IActionResult> GetOvertimeSummary(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] int? departmentId)
        {
            try
            {
                var start = startDate ?? DateTime.Today.AddMonths(-1);
                var end = endDate ?? DateTime.Today;

                var query = _context.OvertimeRequests
                    .Include(or => or.Employee)
                        .ThenInclude(e => e!.Department)
                    .Where(or => or.OvertimeDate >= start && or.OvertimeDate <= end);

                if (departmentId.HasValue)
                {
                    query = query.Where(or => or.Employee!.DepartmentId == departmentId.Value);
                }

                var allRequests = await query.ToListAsync();

                // Group by employee
                var summary = allRequests
                    .GroupBy(or => or.EmployeeId)
                    .Select(g => new
                    {
                        employeeId = g.Key,
                        employeeName = $"{g.First().Employee!.FirstName} {g.First().Employee.LastName}",
                        employeeCode = g.First().Employee!.EmployeeCode,
                        department = g.First().Employee!.Department?.DepartmentName,
                        totalRequests = g.Count(),
                        approvedRequests = g.Count(or => or.Status == "Approved"),
                        rejectedRequests = g.Count(or => or.Status == "Rejected"),
                        pendingRequests = g.Count(or => or.Status == "Pending"),
                        totalOvertimeHours = g.Where(or => or.Status == "Approved").Sum(or => or.RequestedHours)
                    })
                    .OrderByDescending(s => s.totalOvertimeHours)
                    .ToList();

                var overallStats = new
                {
                    totalRequests = allRequests.Count,
                    approvedRequests = allRequests.Count(or => or.Status == "Approved"),
                    rejectedRequests = allRequests.Count(or => or.Status == "Rejected"),
                    pendingRequests = allRequests.Count(or => or.Status == "Pending"),
                    totalOvertimeHours = allRequests.Where(or => or.Status == "Approved").Sum(or => or.RequestedHours)
                };

                return Ok(new { stats = overallStats, summary });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error generating overtime summary", error = ex.Message });
            }
        }
    }

    // ============================================
    // REQUEST MODELS
    // ============================================
    public class OvertimeRequestModel
    {
        public DateTime OvertimeDate { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string? Reason { get; set; }
    }

    public class ReviewOvertimeRequest
    {
        public string? Comments { get; set; }
    }
}
