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
    public class LeaveController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public LeaveController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================
        // 1. SUBMIT LEAVE REQUEST
        // ============================================

        /// <summary>
        /// Submit a new leave request
        /// Auto-approves for Admin/Executive, routes to appropriate approver for others
        /// </summary>
        [HttpPost("Request")]
        public async Task<IActionResult> SubmitLeaveRequest([FromBody] LeaveRequestDto request)
        {
            try
            {
                // Get current user info
                var userEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var userEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;

                if (string.IsNullOrEmpty(userEmployeeIdClaim))
                {
                    return BadRequest(new { message = "Employee ID not found" });
                }

                int employeeId = int.Parse(userEmployeeIdClaim);

                // Get employee details with role
                var employee = await _context.Employees
                    .Include(e => e.User)
                        .ThenInclude(u => u.Role)
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

                if (employee == null)
                {
                    return NotFound(new { message = "Employee not found" });
                }

                // Get leave type
                var leaveType = await _context.LeaveTypes.FindAsync(request.LeaveTypeId);
                if (leaveType == null || !leaveType.IsActive)
                {
                    return BadRequest(new { message = "Invalid leave type" });
                }

                // ✅ Check 1: Full-Time eligibility for paid leave
                if (leaveType.RequiresFullTimeStatus && employee.EmploymentType != "FullTime")
                {
                    return BadRequest(new
                    {
                        message = $"Only full-time employees can request {leaveType.TypeName}. Hourly employees can only request Unpaid Leave."
                    });
                }

                // ✅ Check 2: Validate dates
                if (request.EndDate < request.StartDate)
                {
                    return BadRequest(new { message = "End date cannot be before start date" });
                }

                // Calculate total days (including weekends for now - can enhance later)
                decimal totalDays = (decimal)(request.EndDate - request.StartDate).TotalDays + 1;

                // If half day specified
                if (request.IsHalfDay)
                {
                    totalDays = 0.5m;
                }

                // ✅ Check 3: PTO Balance validation
                if (leaveType.TypeName == "PTO")
                {
                    var currentYear = DateTime.UtcNow.Year;
                    var balance = await _context.LeaveBalance
                        .FirstOrDefaultAsync(lb => lb.EmployeeId == employeeId && lb.Year == currentYear);

                    if (balance == null)
                    {
                        return BadRequest(new { message = "PTO balance not found. Please contact HR." });
                    }

                    if (balance.RemainingPTODays < totalDays)
                    {
                        return BadRequest(new
                        {
                            message = $"Insufficient PTO balance. Available: {balance.RemainingPTODays} days, Requested: {totalDays} days"
                        });
                    }

                    // Check if would exceed annual 20-day limit
                    if (totalDays > 20)
                    {
                        return BadRequest(new { message = "Cannot request more than 20 days in a single request" });
                    }
                }

                // ✅ Check 4: Check for overlapping requests
                var hasOverlap = await _context.LeaveRequests
                    .AnyAsync(lr =>
                        lr.EmployeeId == employeeId &&
                        lr.Status == "Approved" &&
                        ((request.StartDate >= lr.StartDate && request.StartDate <= lr.EndDate) ||
                         (request.EndDate >= lr.StartDate && request.EndDate <= lr.EndDate) ||
                         (request.StartDate <= lr.StartDate && request.EndDate >= lr.EndDate))
                    );

                if (hasOverlap)
                {
                    return BadRequest(new { message = "You already have approved leave during this period" });
                }

                // ✅ Determine approval workflow based on role level
                int? approverRoleLevel = null;
                bool requiresApproval = leaveType.RequiresApproval;
                string status = "Pending";
                int? approvedBy = null;
                DateTime? approvedAt = null;

                var roleLevel = employee.User.Role.RoleLevel;

                if (roleLevel >= 4)
                {
                    // Regular employees (Program Coordinator, Field Operator Manager, Field Operator)
                    // → Need Director approval
                    approverRoleLevel = 3;
                    requiresApproval = true;
                }
                else if (roleLevel == 3)
                {
                    // Directors → Need Executive approval
                    approverRoleLevel = 2;
                    requiresApproval = true;
                }
                else if (roleLevel <= 2)
                {
                    // Admin/Executive → Auto-approved
                    approverRoleLevel = null;
                    requiresApproval = false;
                    status = "Approved";
                    approvedBy = employee.UserId;
                    approvedAt = DateTime.UtcNow;
                }

                // Some leave types don't require approval (e.g., Jury Duty)
                if (!leaveType.RequiresApproval)
                {
                    status = "Approved";
                    approvedBy = employee.UserId;
                    approvedAt = DateTime.UtcNow;
                    requiresApproval = false;
                }

                // Create leave request
                var leaveRequest = new LeaveRequest
                {
                    EmployeeId = employeeId,
                    LeaveTypeId = request.LeaveTypeId,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    TotalDays = totalDays,
                    Reason = request.Reason,
                    Status = status,
                    RequestedAt = DateTime.UtcNow,
                    ApproverRoleLevel = approverRoleLevel,
                    RequiresApproval = requiresApproval,
                    ApprovedBy = approvedBy,
                    ApprovedAt = approvedAt,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.LeaveRequests.Add(leaveRequest);
                await _context.SaveChangesAsync();

                // If auto-approved, update balance and calendar
                if (status == "Approved" && leaveType.TypeName == "PTO")
                {
                    await UpdatePTOBalanceAndCalendar(employeeId, leaveRequest.LeaveRequestId, totalDays, request.StartDate, request.EndDate, request.IsHalfDay);
                }

                // Get approver name for response
                string approverName = "Auto-Approved";
                if (approverRoleLevel == 3)
                {
                    var director = await GetDepartmentDirector(employee.DepartmentId);
                    approverName = director != null ? $"{director.FirstName} {director.LastName}" : "Department Director";
                }
                else if (approverRoleLevel == 2)
                {
                    approverName = "Executive";
                }

                return Ok(new
                {
                    message = status == "Approved" ? "Leave request approved automatically" : "Leave request submitted successfully",
                    leaveRequestId = leaveRequest.LeaveRequestId,
                    status = status,
                    approverName = approverName,
                    totalDays = totalDays
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error submitting leave request", error = ex.Message });
            }
        }

        // ============================================
        // 2. GET MY LEAVE REQUESTS
        // ============================================

        /// <summary>
        /// Get all leave requests for the logged-in employee
        /// </summary>
        [HttpGet("MyRequests")]
        public async Task<IActionResult> GetMyLeaveRequests()
        {
            try
            {
                var userEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;
                if (string.IsNullOrEmpty(userEmployeeIdClaim))
                {
                    return BadRequest(new { message = "Employee ID not found" });
                }

                int employeeId = int.Parse(userEmployeeIdClaim);

                var requests = await _context.LeaveRequests
                    .Include(lr => lr.LeaveType)
                    .Include(lr => lr.ApprovedByUser)
                    .Where(lr => lr.EmployeeId == employeeId)
                    .OrderByDescending(lr => lr.RequestedAt)
                    .Select(lr => new
                    {
                        lr.LeaveRequestId,
                        LeaveType = lr.LeaveType.TypeName,
                        LeaveTypeColor = lr.LeaveType.Color,
                        lr.StartDate,
                        lr.EndDate,
                        lr.TotalDays,
                        lr.Reason,
                        lr.Status,
                        lr.RequestedAt,
                        ApprovedBy = lr.ApprovedByUser != null ? lr.ApprovedByUser.Email : null,
                        lr.ApprovedAt,
                        lr.RejectionReason,
                        lr.ApprovalNotes,
                        CanCancel = lr.Status == "Pending"
                    })
                    .ToListAsync();

                return Ok(requests);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving leave requests", error = ex.Message });
            }
        }

        // ============================================
        // 3. GET MY PTO BALANCE
        // ============================================

        /// <summary>
        /// Get PTO balance for the logged-in employee
        /// </summary>
        [HttpGet("MyBalance")]
        public async Task<IActionResult> GetMyPTOBalance()
        {
            try
            {
                var userEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;
                if (string.IsNullOrEmpty(userEmployeeIdClaim))
                {
                    return BadRequest(new { message = "Employee ID not found" });
                }

                int employeeId = int.Parse(userEmployeeIdClaim);
                var currentYear = DateTime.UtcNow.Year;

                var employee = await _context.Employees.FindAsync(employeeId);
                if (employee == null)
                {
                    return NotFound(new { message = "Employee not found" });
                }

                // Check if eligible for PTO
                if (employee.EmploymentType != "FullTime")
                {
                    return Ok(new
                    {
                        employeeId = employeeId,
                        employmentType = employee.EmploymentType,
                        isEligible = false,
                        message = "Hourly employees are not eligible for PTO",
                        totalPTODays = 0,
                        usedPTODays = 0,
                        remainingPTODays = 0
                    });
                }

                var balance = await _context.LeaveBalance
                    .FirstOrDefaultAsync(lb => lb.EmployeeId == employeeId && lb.Year == currentYear);

                if (balance == null)
                {
                    return Ok(new
                    {
                        employeeId = employeeId,
                        year = currentYear,
                        message = "No balance found for current year. Please contact HR.",
                        totalPTODays = 0,
                        usedPTODays = 0,
                        remainingPTODays = 0
                    });
                }

                return Ok(new
                {
                    employeeId = employeeId,
                    year = currentYear,
                    isEligible = true,
                    totalPTODays = balance.TotalPTODays,
                    usedPTODays = balance.UsedPTODays,
                    remainingPTODays = balance.RemainingPTODays,
                    accrualRate = balance.AccrualRate,
                    lastAccrualDate = balance.LastAccrualDate
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving PTO balance", error = ex.Message });
            }
        }

        // ============================================
        // 4. GET PENDING APPROVALS (Directors & Executives)
        // ============================================

        /// <summary>
        /// Get leave requests pending approval for the logged-in user
        /// Directors see their department's requests
        /// Executives see all director requests
        /// </summary>
        [HttpGet("PendingApprovals")]
        [Authorize(Roles = "Director,Executive,Admin")]
        public async Task<IActionResult> GetPendingApprovals()
        {
            try
            {
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var userEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;

                if (string.IsNullOrEmpty(userRole))
                {
                    return Unauthorized(new { message = "Role not found" });
                }

                var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == userRole);
                if (role == null)
                {
                    return NotFound(new { message = "Role not found" });
                }

                IQueryable<LeaveRequest> query = _context.LeaveRequests
                    .Include(lr => lr.Employee)
                        .ThenInclude(e => e.Department)
                    .Include(lr => lr.Employee.User)
                        .ThenInclude(u => u.Role)
                    .Include(lr => lr.LeaveType)
                    .Where(lr => lr.Status == "Pending" && lr.RequiresApproval == true);

                // Filter based on approver role level
                if (role.RoleName == "Director")
                {
                    // Directors see requests from their department employees (RoleLevel >= 4)
                    if (string.IsNullOrEmpty(userEmployeeIdClaim))
                    {
                        return BadRequest(new { message = "Employee ID not found" });
                    }

                    var directorEmployee = await _context.Employees.FindAsync(int.Parse(userEmployeeIdClaim));
                    if (directorEmployee == null)
                    {
                        return NotFound(new { message = "Director employee record not found" });
                    }

                    query = query.Where(lr =>
                        lr.ApproverRoleLevel == 3 && // Needs director approval
                        lr.Employee.DepartmentId == directorEmployee.DepartmentId // Same department
                    );
                }
                else if (role.RoleName == "Executive")
                {
                    // Executives see all director requests (RoleLevel == 3)
                    query = query.Where(lr => lr.ApproverRoleLevel == 2); // Needs executive approval
                }
                else if (role.RoleName == "Admin")
                {
                    // Admins see all pending requests
                    query = query.Where(lr => lr.RequiresApproval == true);
                }

                var pendingRequests = await query
                    .OrderBy(lr => lr.RequestedAt)
                    .Select(lr => new
                    {
                        lr.LeaveRequestId,
                        Employee = new
                        {
                            lr.Employee.EmployeeId,
                            lr.Employee.EmployeeCode,
                            FullName = lr.Employee.FirstName + " " + lr.Employee.LastName,
                            lr.Employee.JobTitle,
                            Role = lr.Employee.User.Role.RoleName,
                            Department = lr.Employee.Department != null ? lr.Employee.Department.DepartmentName : null
                        },
                        LeaveType = lr.LeaveType.TypeName,
                        LeaveTypeColor = lr.LeaveType.Color,
                        lr.StartDate,
                        lr.EndDate,
                        lr.TotalDays,
                        lr.Reason,
                        lr.Status,
                        lr.RequestedAt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    count = pendingRequests.Count,
                    requests = pendingRequests
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving pending approvals", error = ex.Message });
            }
        }

        // ============================================
        // 5. APPROVE LEAVE REQUEST
        // ============================================

        /// <summary>
        /// Approve a leave request
        /// </summary>
        [HttpPut("Approve/{id}")]
        [Authorize(Roles = "Director,Executive,Admin")]
        public async Task<IActionResult> ApproveLeaveRequest(int id, [FromBody] ApprovalDto approval)
        {
            try
            {
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var userEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;
                var userId = User.FindFirst("UserId")?.Value;

                var leaveRequest = await _context.LeaveRequests
                    .Include(lr => lr.Employee)
                        .ThenInclude(e => e.Department)
                    .Include(lr => lr.Employee.User)
                        .ThenInclude(u => u.Role)
                    .Include(lr => lr.LeaveType)
                    .FirstOrDefaultAsync(lr => lr.LeaveRequestId == id);

                if (leaveRequest == null)
                {
                    return NotFound(new { message = "Leave request not found" });
                }

                if (leaveRequest.Status != "Pending")
                {
                    return BadRequest(new { message = $"Cannot approve request with status: {leaveRequest.Status}" });
                }

                // Verify approver is authorized
                var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == userRole);
                if (role == null)
                {
                    return Unauthorized(new { message = "Role not found" });
                }

                // Check authorization based on role
                if (role.RoleName == "Director")
                {
                    // Verify it's in their department and needs director approval
                    var directorEmployee = await _context.Employees.FindAsync(int.Parse(userEmployeeIdClaim));
                    if (directorEmployee == null ||
                        leaveRequest.Employee.DepartmentId != directorEmployee.DepartmentId ||
                        leaveRequest.ApproverRoleLevel != 3)
                    {
                        return Forbid();
                    }
                }
                else if (role.RoleName == "Executive")
                {
                    // Verify it needs executive approval
                    if (leaveRequest.ApproverRoleLevel != 2)
                    {
                        return Forbid();
                    }
                }

                // Update leave request status
                leaveRequest.Status = "Approved";
                leaveRequest.ApprovedBy = int.Parse(userId);
                leaveRequest.ApprovedAt = DateTime.UtcNow;
                leaveRequest.ApprovalNotes = approval?.ApprovalNotes;
                leaveRequest.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Update PTO balance and calendar if it's a PTO request
                if (leaveRequest.LeaveType.TypeName == "PTO")
                {
                    await UpdatePTOBalanceAndCalendar(
                        leaveRequest.EmployeeId,
                        leaveRequest.LeaveRequestId,
                        leaveRequest.TotalDays,
                        leaveRequest.StartDate,
                        leaveRequest.EndDate,
                        leaveRequest.TotalDays < 1 // IsHalfDay if less than 1 day
                    );
                }

                return Ok(new
                {
                    message = "Leave request approved successfully",
                    leaveRequestId = leaveRequest.LeaveRequestId,
                    employeeName = $"{leaveRequest.Employee.FirstName} {leaveRequest.Employee.LastName}",
                    status = leaveRequest.Status
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error approving leave request", error = ex.Message });
            }
        }

        // ============================================
        // 6. REJECT LEAVE REQUEST
        // ============================================

        /// <summary>
        /// Reject a leave request
        /// </summary>
        [HttpPut("Reject/{id}")]
        [Authorize(Roles = "Director,Executive,Admin")]
        public async Task<IActionResult> RejectLeaveRequest(int id, [FromBody] RejectionDto rejection)
        {
            try
            {
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var userEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;

                if (string.IsNullOrEmpty(rejection?.RejectionReason))
                {
                    return BadRequest(new { message = "Rejection reason is required" });
                }

                var leaveRequest = await _context.LeaveRequests
                    .Include(lr => lr.Employee)
                        .ThenInclude(e => e.Department)
                    .Include(lr => lr.Employee.User)
                        .ThenInclude(u => u.Role)
                    .FirstOrDefaultAsync(lr => lr.LeaveRequestId == id);

                if (leaveRequest == null)
                {
                    return NotFound(new { message = "Leave request not found" });
                }

                if (leaveRequest.Status != "Pending")
                {
                    return BadRequest(new { message = $"Cannot reject request with status: {leaveRequest.Status}" });
                }

                // Verify approver is authorized (same logic as approve)
                var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == userRole);
                if (role == null)
                {
                    return Unauthorized(new { message = "Role not found" });
                }

                if (role.RoleName == "Director")
                {
                    var directorEmployee = await _context.Employees.FindAsync(int.Parse(userEmployeeIdClaim));
                    if (directorEmployee == null ||
                        leaveRequest.Employee.DepartmentId != directorEmployee.DepartmentId ||
                        leaveRequest.ApproverRoleLevel != 3)
                    {
                        return Forbid();
                    }
                }
                else if (role.RoleName == "Executive")
                {
                    if (leaveRequest.ApproverRoleLevel != 2)
                    {
                        return Forbid();
                    }
                }

                // Update leave request status
                leaveRequest.Status = "Rejected";
                leaveRequest.RejectionReason = rejection.RejectionReason;
                leaveRequest.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Leave request rejected",
                    leaveRequestId = leaveRequest.LeaveRequestId,
                    employeeName = $"{leaveRequest.Employee.FirstName} {leaveRequest.Employee.LastName}",
                    status = leaveRequest.Status
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error rejecting leave request", error = ex.Message });
            }
        }

        // ============================================
        // 7. CANCEL MY REQUEST
        // ============================================

        /// <summary>
        /// Cancel own leave request (only if pending)
        /// </summary>
        [HttpDelete("Cancel/{id}")]
        public async Task<IActionResult> CancelLeaveRequest(int id)
        {
            try
            {
                var userEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;
                if (string.IsNullOrEmpty(userEmployeeIdClaim))
                {
                    return BadRequest(new { message = "Employee ID not found" });
                }

                int employeeId = int.Parse(userEmployeeIdClaim);

                var leaveRequest = await _context.LeaveRequests
                    .FirstOrDefaultAsync(lr => lr.LeaveRequestId == id && lr.EmployeeId == employeeId);

                if (leaveRequest == null)
                {
                    return NotFound(new { message = "Leave request not found or you don't have permission to cancel it" });
                }

                if (leaveRequest.Status != "Pending")
                {
                    return BadRequest(new { message = "Only pending requests can be cancelled" });
                }

                leaveRequest.Status = "Cancelled";
                leaveRequest.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Leave request cancelled successfully",
                    leaveRequestId = leaveRequest.LeaveRequestId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error cancelling leave request", error = ex.Message });
            }
        }

        // ============================================
        // 8. GET LEAVE CALENDAR
        // ============================================

        /// <summary>
        /// Get leave calendar for a date range
        /// Directors see their department, Executives/Admins see all
        /// </summary>
        [HttpGet("Calendar")]
        public async Task<IActionResult> GetLeaveCalendar([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] int? departmentId)
        {
            try
            {
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var userEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;

                // Default to current month if dates not provided
                var start = startDate ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
                var end = endDate ?? start.AddMonths(1).AddDays(-1);

                IQueryable<LeaveCalendar> query = _context.LeaveCalendar
                    .Include(lc => lc.Employee)
                        .ThenInclude(e => e.Department)
                    .Include(lc => lc.LeaveRequest)
                        .ThenInclude(lr => lr.LeaveType)
                    .Where(lc =>
                        lc.LeaveDate >= start &&
                        lc.LeaveDate <= end &&
                        lc.LeaveRequest.Status == "Approved"
                    );

                // Apply filters based on role
                if (userRole == "Director" && !string.IsNullOrEmpty(userEmployeeIdClaim))
                {
                    var directorEmployee = await _context.Employees.FindAsync(int.Parse(userEmployeeIdClaim));
                    if (directorEmployee != null)
                    {
                        query = query.Where(lc => lc.Employee.DepartmentId == directorEmployee.DepartmentId);
                    }
                }
                else if (departmentId.HasValue)
                {
                    // Filter by specific department if provided
                    query = query.Where(lc => lc.Employee.DepartmentId == departmentId.Value);
                }

                var calendar = await query
                    .OrderBy(lc => lc.LeaveDate)
                    .Select(lc => new
                    {
                        lc.LeaveCalendarId,
                        lc.LeaveDate,
                        lc.IsFullDay,
                        lc.IsFirstHalf,
                        lc.IsSecondHalf,
                        Employee = new
                        {
                            lc.Employee.EmployeeId,
                            lc.Employee.EmployeeCode,
                            FullName = lc.Employee.FirstName + " " + lc.Employee.LastName,
                            lc.Employee.JobTitle,
                            Department = lc.Employee.Department != null ? lc.Employee.Department.DepartmentName : null
                        },
                        LeaveType = lc.LeaveRequest.LeaveType.TypeName,
                        LeaveTypeColor = lc.LeaveRequest.LeaveType.Color,
                        LeaveRequestId = lc.LeaveRequestId
                    })
                    .ToListAsync();

                return Ok(calendar);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving leave calendar", error = ex.Message });
            }
        }

        // ============================================
        // 9. GET LEAVE TYPES
        // ============================================

        /// <summary>
        /// Get all active leave types
        /// </summary>
        [HttpGet("Types")]
        public async Task<IActionResult> GetLeaveTypes()
        {
            try
            {
                var userEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;
                if (string.IsNullOrEmpty(userEmployeeIdClaim))
                {
                    return BadRequest(new { message = "Employee ID not found" });
                }

                int employeeId = int.Parse(userEmployeeIdClaim);
                var employee = await _context.Employees.FindAsync(employeeId);

                if (employee == null)
                {
                    return NotFound(new { message = "Employee not found" });
                }

                var leaveTypes = await _context.LeaveTypes
                    .Where(lt => lt.IsActive)
                    .OrderBy(lt => lt.DisplayOrder)
                    .Select(lt => new
                    {
                        lt.LeaveTypeId,
                        lt.TypeName,
                        lt.Description,
                        lt.IsPaidLeave,
                        lt.RequiresApproval,
                        lt.MaxDaysPerYear,
                        lt.RequiresFullTimeStatus,
                        lt.Color,
                        // Check if employee is eligible
                        IsEligible = !lt.RequiresFullTimeStatus || employee.EmploymentType == "FullTime"
                    })
                    .ToListAsync();

                return Ok(leaveTypes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving leave types", error = ex.Message });
            }
        }

        // ============================================
        // 10. GET LEAVE STATS (For Dashboard)
        // ============================================

        /// <summary>
        /// Get leave statistics for dashboard
        /// </summary>
        [HttpGet("Stats")]
        public async Task<IActionResult> GetLeaveStats()
        {
            try
            {
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var userEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;

                if (string.IsNullOrEmpty(userEmployeeIdClaim))
                {
                    return BadRequest(new { message = "Employee ID not found" });
                }

                int employeeId = int.Parse(userEmployeeIdClaim);
                var currentYear = DateTime.UtcNow.Year;

                // Get employee's stats
                var myStats = new
                {
                    pendingRequests = await _context.LeaveRequests
                        .CountAsync(lr => lr.EmployeeId == employeeId && lr.Status == "Pending"),
                    approvedThisYear = await _context.LeaveRequests
                        .CountAsync(lr => lr.EmployeeId == employeeId &&
                                         lr.Status == "Approved" &&
                                         lr.StartDate.Year == currentYear),
                    upcomingLeaves = await _context.LeaveRequests
                        .CountAsync(lr => lr.EmployeeId == employeeId &&
                                         lr.Status == "Approved" &&
                                         lr.StartDate > DateTime.UtcNow)
                };

                // Get approver stats (for Directors/Executives)
                object approverStats = null;
                if (userRole == "Director" || userRole == "Executive" || userRole == "Admin")
                {
                    IQueryable<LeaveRequest> pendingQuery = _context.LeaveRequests
                        .Include(lr => lr.Employee)
                        .Where(lr => lr.Status == "Pending" && lr.RequiresApproval == true);

                    if (userRole == "Director")
                    {
                        var directorEmployee = await _context.Employees.FindAsync(employeeId);
                        if (directorEmployee != null)
                        {
                            pendingQuery = pendingQuery.Where(lr =>
                                lr.ApproverRoleLevel == 3 &&
                                lr.Employee.DepartmentId == directorEmployee.DepartmentId
                            );
                        }
                    }
                    else if (userRole == "Executive")
                    {
                        pendingQuery = pendingQuery.Where(lr => lr.ApproverRoleLevel == 2);
                    }

                    approverStats = new
                    {
                        pendingApprovals = await pendingQuery.CountAsync()
                    };
                }

                return Ok(new
                {
                    myStats,
                    approverStats
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving leave stats", error = ex.Message });
            }
        }

        // ============================================
        // HELPER METHODS
        // ============================================

        /// <summary>
        /// Get department director
        /// </summary>
        private async Task<Employee?> GetDepartmentDirector(int? departmentId)
        {
            if (!departmentId.HasValue) return null;

            return await _context.Employees
                .Include(e => e.User)
                    .ThenInclude(u => u.Role)
                .Where(e => e.DepartmentId == departmentId &&
                           e.User.Role.RoleName == "Director" &&
                           e.IsActive)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Update PTO balance and add to leave calendar
        /// </summary>
        private async Task UpdatePTOBalanceAndCalendar(int employeeId, int leaveRequestId, decimal totalDays, DateTime startDate, DateTime endDate, bool isHalfDay)
        {
            var currentYear = DateTime.UtcNow.Year;

            // Update PTO balance
            var balance = await _context.LeaveBalance
                .FirstOrDefaultAsync(lb => lb.EmployeeId == employeeId && lb.Year == currentYear);

            if (balance != null)
            {
                balance.UsedPTODays += totalDays;
                balance.UpdatedAt = DateTime.UtcNow;
            }

            // Add to leave calendar
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var calendarEntry = new LeaveCalendar
                {
                    LeaveRequestId = leaveRequestId,
                    EmployeeId = employeeId,
                    LeaveDate = date,
                    IsFullDay = !isHalfDay || (endDate - startDate).TotalDays > 0,
                    IsFirstHalf = isHalfDay && date == startDate,
                    IsSecondHalf = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.LeaveCalendar.Add(calendarEntry);
            }

            await _context.SaveChangesAsync();
        }
    }

    // ============================================
    // DTOs
    // ============================================

    public class LeaveRequestDto
    {
        public int LeaveTypeId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Reason { get; set; }
        public bool IsHalfDay { get; set; } = false;
    }

    public class ApprovalDto
    {
        public string? ApprovalNotes { get; set; }
    }

    public class RejectionDto
    {
        public string RejectionReason { get; set; } = string.Empty;
    }
}