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
    public class HRActionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public HRActionController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================
        // 1. GET ACTION TYPES
        // ============================================
        [HttpGet("ActionTypes")]
        public async Task<IActionResult> GetActionTypes()
        {
            try
            {
                var actionTypes = await _context.HRActionTypes
                    .Where(at => at.IsActive)
                    .OrderBy(at => at.DisplayOrder)
                    .Select(at => new
                    {
                        at.ActionTypeId,
                        at.ActionTypeName,
                        at.Description,
                        at.RequiresFinanceApproval,
                        at.RequiresAdminApproval
                    })
                    .ToListAsync();

                return Ok(actionTypes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving action types", error = ex.Message });
            }
        }

        // ============================================
        // 2. SUBMIT HR ACTION REQUEST
        // ============================================
        [HttpPost("Submit")]
        public async Task<IActionResult> SubmitRequest([FromBody] HRActionRequestDto request)
        {
            try
            {
                var userEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;
                var userId = User.FindFirst("UserId")?.Value;

                if (string.IsNullOrEmpty(userEmployeeIdClaim))
                {
                    return BadRequest(new { message = "Employee ID not found" });
                }

                int employeeId = int.Parse(userEmployeeIdClaim);

                // Get employee with details
                var employee = await _context.Employees
                    .Include(e => e.User).ThenInclude(u => u.Role)
                    .Include(e => e.Department)
                    .Include(e => e.Manager)
                    .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

                if (employee == null)
                {
                    return NotFound(new { message = "Employee not found" });
                }

                // Get action type
                var actionType = await _context.HRActionTypes.FindAsync(request.ActionTypeId);
                if (actionType == null || !actionType.IsActive)
                {
                    return BadRequest(new { message = "Invalid action type" });
                }

                // Generate request number: HRA-2025-0001
                var year = DateTime.Now.Year;
                var lastRequest = await _context.HRActionRequests
                    .Where(r => r.RequestNumber.StartsWith($"HRA-{year}-"))
                    .OrderByDescending(r => r.RequestId)
                    .FirstOrDefaultAsync();

                int nextNumber = 1;
                if (lastRequest != null)
                {
                    var lastNumberStr = lastRequest.RequestNumber.Split('-').Last();
                    if (int.TryParse(lastNumberStr, out int lastNumber))
                    {
                        nextNumber = lastNumber + 1;
                    }
                }

                string requestNumber = $"HRA-{year}-{nextNumber:D4}";

                // Create request
                var hrActionRequest = new HRActionRequest
                {
                    RequestNumber = requestNumber,
                    EmployeeId = employeeId,
                    ActionTypeId = request.ActionTypeId,
                    RequestDate = DateTime.UtcNow,
                    EffectiveDate = request.EffectiveDate,
                    Status = "Pending",
                    Reason = request.Reason,
                    Notes = request.Notes,
                    SubmittedBy = int.Parse(userId),
                    SubmittedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,

                    // Action-specific fields from request DTO
                    OldRate = request.OldRate,
                    NewRate = request.NewRate,
                    OldRateType = request.OldRateType,
                    NewRateType = request.NewRateType,
                    PremiumIncentive = request.PremiumIncentive,

                    OldDepartmentId = request.OldDepartmentId,
                    NewDepartmentId = request.NewDepartmentId,
                    OldLocation = request.OldLocation,
                    NewLocation = request.NewLocation,
                    OldSupervisorId = request.OldSupervisorId,
                    NewSupervisorId = request.NewSupervisorId,
                    OldClassification = request.OldClassification,
                    NewClassification = request.NewClassification,

                    OldJobTitle = request.OldJobTitle,
                    NewJobTitle = request.NewJobTitle,

                    OldEmploymentType = request.OldEmploymentType,
                    NewEmploymentType = request.NewEmploymentType,
                    OldMaritalStatus = request.OldMaritalStatus,
                    NewMaritalStatus = request.NewMaritalStatus,

                    OldFirstName = request.OldFirstName,
                    NewFirstName = request.NewFirstName,
                    OldLastName = request.OldLastName,
                    NewLastName = request.NewLastName,
                    OldAddress = request.OldAddress,
                    NewAddress = request.NewAddress,
                    OldPhone = request.OldPhone,
                    NewPhone = request.NewPhone,
                    OldEmail = request.OldEmail,
                    NewEmail = request.NewEmail,

                    HealthInsuranceChange = request.HealthInsuranceChange,
                    DentalInsuranceChange = request.DentalInsuranceChange,
                    Retirement403bEnroll = request.Retirement403bEnroll,

                    PayrollDeductionDescription = request.PayrollDeductionDescription,
                    PayrollDeductionAmount = request.PayrollDeductionAmount,

                    LeaveType = request.LeaveType,
                    LeaveStartDate = request.LeaveStartDate,
                    LeaveEndDate = request.LeaveEndDate,
                    LeaveDays = request.LeaveDays
                };

                _context.HRActionRequests.Add(hrActionRequest);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "HR Action request submitted successfully",
                    requestId = hrActionRequest.RequestId,
                    requestNumber = hrActionRequest.RequestNumber,
                    status = hrActionRequest.Status
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error submitting request", error = ex.Message });
            }
        }

        // ============================================
        // 3. GET MY REQUESTS
        // ============================================
        [HttpGet("MyRequests")]
        public async Task<IActionResult> GetMyRequests()
        {
            try
            {
                var userEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;
                if (string.IsNullOrEmpty(userEmployeeIdClaim))
                {
                    return BadRequest(new { message = "Employee ID not found" });
                }

                int employeeId = int.Parse(userEmployeeIdClaim);

                var requests = await _context.HRActionRequests
                    .Include(r => r.ActionType)
                    .Include(r => r.Employee)
                    .Where(r => r.EmployeeId == employeeId)
                    .OrderByDescending(r => r.RequestDate)
                    .Select(r => new
                    {
                        r.RequestId,
                        r.RequestNumber,
                        ActionType = r.ActionType.ActionTypeName,
                        r.RequestDate,
                        r.EffectiveDate,
                        r.Status,
                        r.Reason,
                        // Return relevant fields based on action type
                        r.OldRate,
                        r.NewRate,
                        r.OldJobTitle,
                        r.NewJobTitle,
                        r.NewLocation,
                        r.RejectionReason,
                        CanCancel = r.Status == "Pending"
                    })
                    .ToListAsync();

                return Ok(requests);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving requests", error = ex.Message });
            }
        }

        // ============================================
        // 4. GET PENDING REVIEW (HR/Executive Dashboard)
        // ============================================
        [HttpGet("PendingReview")]
        [Authorize(Roles = "Admin,Executive")]
        public async Task<IActionResult> GetPendingReview()
        {
            try
            {
                // HR and Executive see ALL pending requests (no filtering)
                var requests = await _context.HRActionRequests
                    .Include(r => r.ActionType)
                    .Include(r => r.Employee).ThenInclude(e => e.Department)
                    .Include(r => r.Employee).ThenInclude(e => e.User).ThenInclude(u => u.Role)
                    .Where(r => r.Status == "Pending")
                    .OrderBy(r => r.RequestDate)
                    .Select(r => new
                    {
                        r.RequestId,
                        r.RequestNumber,
                        Employee = new
                        {
                            r.Employee.EmployeeId,
                            FullName = r.Employee.FirstName + " " + r.Employee.LastName,
                            r.Employee.JobTitle,
                            Department = r.Employee.Department.DepartmentName
                        },
                        ActionType = r.ActionType.ActionTypeName,
                        r.RequestDate,
                        r.EffectiveDate,
                        r.Status,
                        r.Reason,
                        // Return relevant fields
                        r.OldRate,
                        r.NewRate,
                        r.OldJobTitle,
                        r.NewJobTitle,
                        r.NewLocation,
                        r.OldFirstName,
                        r.NewFirstName,
                        r.NewLastName,
                        r.OldLastName
                    })
                    .ToListAsync();

                return Ok(new { count = requests.Count, requests });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving pending requests", error = ex.Message });
            }
        }

        // ============================================
        // 5. GET REQUEST DETAILS
        // ============================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetRequestDetails(int id)
        {
            try
            {
                var request = await _context.HRActionRequests
                    .Include(r => r.ActionType)
                    .Include(r => r.Employee).ThenInclude(e => e.Department)
                    .Include(r => r.Employee).ThenInclude(e => e.User)
                    .Include(r => r.OldDepartment)
                    .Include(r => r.NewDepartment)
                    .FirstOrDefaultAsync(r => r.RequestId == id);

                if (request == null)
                {
                    return NotFound(new { message = "Request not found" });
                }

                return Ok(request);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving request details", error = ex.Message });
            }
        }

        // ============================================
        // 6. APPROVE REQUEST (HR/Executive ONLY)
        // ============================================
        [HttpPut("Approve/{id}")]
        [Authorize(Roles = "Admin,Executive")]
        public async Task<IActionResult> ApproveRequest(int id, [FromBody] HRActionApprovalDto approval)
        {
            try
            {
                var userId = User.FindFirst("UserId")?.Value;

                var request = await _context.HRActionRequests
                    .Include(r => r.Employee)
                    .Include(r => r.ActionType)
                    .FirstOrDefaultAsync(r => r.RequestId == id);

                if (request == null)
                {
                    return NotFound(new { message = "Request not found" });
                }

                if (request.Status != "Pending")
                {
                    return BadRequest(new { message = $"Cannot approve request with status: {request.Status}" });
                }

                // Simple approval - immediately approved
                request.Status = "Approved";
                request.FinalApprovedBy = int.Parse(userId);
                request.FinalApprovedAt = DateTime.UtcNow;
                request.HRComments = approval.Comments;
                request.UpdatedAt = DateTime.UtcNow;

                // Auto-update employee record
                await UpdateEmployeeRecord(request);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Request approved successfully and employee record updated",
                    requestId = request.RequestId,
                    requestNumber = request.RequestNumber,
                    employeeName = $"{request.Employee.FirstName} {request.Employee.LastName}",
                    status = request.Status
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error approving request", error = ex.Message });
            }
        }

        // ============================================
        // 7. REJECT REQUEST (HR/Executive ONLY)
        // ============================================
        [HttpPut("Reject/{id}")]
        [Authorize(Roles = "Admin,Executive")]
        public async Task<IActionResult> RejectRequest(int id, [FromBody] HRActionRejectionDto rejection)
        {
            try
            {
                var userId = User.FindFirst("UserId")?.Value;

                var request = await _context.HRActionRequests
                    .Include(r => r.Employee)
                    .FirstOrDefaultAsync(r => r.RequestId == id);

                if (request == null)
                {
                    return NotFound(new { message = "Request not found" });
                }

                if (request.Status != "Pending")
                {
                    return BadRequest(new { message = $"Cannot reject request with status: {request.Status}" });
                }

                if (string.IsNullOrWhiteSpace(rejection?.RejectionReason))
                {
                    return BadRequest(new { message = "Rejection reason is required" });
                }

                request.Status = "Rejected";
                request.RejectedBy = int.Parse(userId);
                request.RejectedAt = DateTime.UtcNow;
                request.RejectionReason = rejection.RejectionReason;
                request.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Request rejected",
                    requestId = request.RequestId,
                    requestNumber = request.RequestNumber,
                    employeeName = $"{request.Employee.FirstName} {request.Employee.LastName}"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error rejecting request", error = ex.Message });
            }
        }

        // ============================================
        // HELPER: Update Employee Record
        // ============================================
        private async Task UpdateEmployeeRecord(HRActionRequest request)
        {
            var employee = await _context.Employees.FindAsync(request.EmployeeId);
            if (employee == null) return;

            // Update based on action type
            switch (request.ActionTypeId)
            {
                case 1: // Rate Change
                    if (request.NewRate.HasValue)
                        employee.Salary = request.NewRate.Value;
                    if (!string.IsNullOrEmpty(request.NewRateType))
                        employee.PayFrequency = request.NewRateType;
                    break;

                case 2: // Transfer
                    if (request.NewDepartmentId.HasValue)
                        employee.DepartmentId = request.NewDepartmentId;
                    if (request.NewSupervisorId.HasValue)
                        employee.ManagerId = request.NewSupervisorId;
                    if (!string.IsNullOrEmpty(request.NewClassification))
                        employee.EmploymentType = request.NewClassification;
                    break;

                case 3: // Promotion
                    if (!string.IsNullOrEmpty(request.NewJobTitle))
                        employee.JobTitle = request.NewJobTitle;
                    if (request.NewRate.HasValue)
                        employee.Salary = request.NewRate.Value;
                    break;

                case 4: // Status Change
                    if (!string.IsNullOrEmpty(request.NewEmploymentType))
                        employee.EmploymentType = request.NewEmploymentType;
                    if (!string.IsNullOrEmpty(request.NewMaritalStatus))
                        employee.MaritalStatus = request.NewMaritalStatus;
                    break;

                case 5: // Personal Info Change
                    if (!string.IsNullOrEmpty(request.NewFirstName))
                        employee.FirstName = request.NewFirstName;
                    if (!string.IsNullOrEmpty(request.NewLastName))
                        employee.LastName = request.NewLastName;
                    if (!string.IsNullOrEmpty(request.NewAddress))
                        employee.Address = request.NewAddress;
                    if (!string.IsNullOrEmpty(request.NewPhone))
                        employee.PhoneNumber = request.NewPhone;
                    if (!string.IsNullOrEmpty(request.NewEmail))
                        employee.PersonalEmail = request.NewEmail;
                    break;
            }

            employee.UpdatedAt = DateTime.UtcNow;
            request.IsProcessed = true;
            request.ProcessedAt = DateTime.UtcNow;
            request.ProcessedBy = request.FinalApprovedBy;
        }
    }

    // ============================================
    // DTOs
    // ============================================
    public class HRActionRequestDto
    {
        public int ActionTypeId { get; set; }
        public DateTime? EffectiveDate { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }

        // Rate Change
        public decimal? OldRate { get; set; }
        public decimal? NewRate { get; set; }
        public string? OldRateType { get; set; }
        public string? NewRateType { get; set; }
        public string? PremiumIncentive { get; set; }

        // Transfer
        public int? OldDepartmentId { get; set; }
        public int? NewDepartmentId { get; set; }
        public string? OldLocation { get; set; }
        public string? NewLocation { get; set; }
        public int? OldSupervisorId { get; set; }
        public int? NewSupervisorId { get; set; }
        public string? OldClassification { get; set; }
        public string? NewClassification { get; set; }

        // Promotion
        public string? OldJobTitle { get; set; }
        public string? NewJobTitle { get; set; }

        // Status Change
        public string? OldEmploymentType { get; set; }
        public string? NewEmploymentType { get; set; }
        public string? OldMaritalStatus { get; set; }
        public string? NewMaritalStatus { get; set; }

        // Personal Info
        public string? OldFirstName { get; set; }
        public string? NewFirstName { get; set; }
        public string? OldLastName { get; set; }
        public string? NewLastName { get; set; }
        public string? OldAddress { get; set; }
        public string? NewAddress { get; set; }
        public string? OldPhone { get; set; }
        public string? NewPhone { get; set; }
        public string? OldEmail { get; set; }
        public string? NewEmail { get; set; }

        // Insurance
        public string? HealthInsuranceChange { get; set; }
        public string? DentalInsuranceChange { get; set; }
        public bool? Retirement403bEnroll { get; set; }

        // Payroll Deduction
        public string? PayrollDeductionDescription { get; set; }
        public decimal? PayrollDeductionAmount { get; set; }

        // Leave of Absence
        public string? LeaveType { get; set; }
        public DateTime? LeaveStartDate { get; set; }
        public DateTime? LeaveEndDate { get; set; }
        public int? LeaveDays { get; set; }
    }

    public class HRActionApprovalDto
    {
        public string? Comments { get; set; }
    }

    public class HRActionRejectionDto
    {
        public string RejectionReason { get; set; }
    }
}