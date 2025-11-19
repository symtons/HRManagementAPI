// Controllers/ProfileController.cs
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
    [Authorize] // All endpoints require authentication
    public class ProfileController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ProfileController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================
        // GET: api/Profile/Me
        // Get complete profile of current logged-in user
        // ============================================
        [HttpGet("Me")]
        public async Task<IActionResult> GetMyProfile()
        {
            try
            {
                var userEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;

                if (string.IsNullOrEmpty(userEmployeeIdClaim))
                {
                    return Unauthorized(new { message = "Employee information not found in token" });
                }

                var employeeId = int.Parse(userEmployeeIdClaim);

                var employee = await _context.Employees
                    .Include(e => e.Department)
                    .Include(e => e.Manager)
                    .Include(e => e.User)
                        .ThenInclude(u => u.Role)
                    .Include(e => e.Banking) // Include banking info
                    .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

                if (employee == null)
                {
                    return NotFound(new { message = "Employee profile not found" });
                }

                // Return complete profile
                var profile = new
                {
                    // Personal Information
                    employee.EmployeeId,
                    employee.EmployeeCode,
                    employee.FirstName,
                    employee.LastName,
                    employee.MiddleName,
                    FullName = $"{employee.FirstName} {employee.MiddleName} {employee.LastName}".Replace("  ", " ").Trim(),
                    employee.DateOfBirth,
                    employee.Gender,
                    employee.MaritalStatus,

                    // Contact Information
                    employee.PhoneNumber,
                    employee.PersonalEmail,
                    employee.Address,
                    employee.City,
                    employee.State,
                    employee.ZipCode,
                    employee.Country,

                    // Emergency Contact
                    employee.EmergencyContactName,
                    employee.EmergencyContactPhone,
                    employee.EmergencyContactRelationship,

                    // Employment Information
                    employee.JobTitle,
                    employee.EmployeeType,
                    employee.EmploymentStatus,
                    employee.HireDate,

                    // Department & Manager
                    Department = employee.Department != null ? new
                    {
                        employee.Department.DepartmentId,
                        employee.Department.DepartmentName,
                        employee.Department.DepartmentCode
                    } : null,

                    Manager = employee.Manager != null ? new
                    {
                        employee.Manager.EmployeeId,
                        ManagerName = $"{employee.Manager.FirstName} {employee.Manager.LastName}",
                        employee.Manager.JobTitle
                    } : null,

                    // Benefits (only for Admin Staff)
                    employee.IsEligibleForPTO,
                    employee.PTOBalance,
                    employee.IsEligibleForInsurance,

                    // Banking (masked)
                    Banking = employee.Banking != null ? new
                    {
                        employee.Banking.BankingId,
                        employee.Banking.BankName,
                        employee.Banking.AccountHolderName,
                        AccountNumber = MaskAccountNumber(employee.Banking.BankAccountNumber),
                        employee.Banking.BankRoutingNumber,
                        employee.Banking.AccountType,
                        employee.Banking.IsVerified,
                        employee.Banking.UpdatedAt
                    } : null,

                    // User Account
                    User = employee.User != null ? new
                    {
                        employee.User.UserId,
                        employee.User.Email,
                        Role = employee.User.Role.RoleName,
                        employee.User.LastLoginAt,
                        employee.User.IsActive
                    } : null
                };

                return Ok(profile);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving profile", error = ex.Message });
            }
        }

        // ============================================
        // PUT: api/Profile/UpdateContact
        // Update contact information (phone, email, address)
        // ============================================
        [HttpPut("UpdateContact")]
        public async Task<IActionResult> UpdateContactInfo([FromBody] UpdateContactRequest request)
        {
            try
            {
                var userEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;

                if (string.IsNullOrEmpty(userEmployeeIdClaim))
                {
                    return Unauthorized(new { message = "Employee information not found" });
                }

                var employeeId = int.Parse(userEmployeeIdClaim);
                var employee = await _context.Employees.FindAsync(employeeId);

                if (employee == null)
                {
                    return NotFound(new { message = "Employee not found" });
                }

                // Update contact info
                employee.PhoneNumber = request.PhoneNumber;
                employee.PersonalEmail = request.PersonalEmail;
                employee.Address = request.Address;
                employee.City = request.City;
                employee.State = request.State;
                employee.ZipCode = request.ZipCode;
                employee.Country = request.Country;
                employee.UpdatedAt = DateTime.UtcNow;
                employee.UpdatedBy = employeeId;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Contact information updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating contact information", error = ex.Message });
            }
        }

        // ============================================
        // PUT: api/Profile/UpdateEmergencyContact
        // Update emergency contact information
        // ============================================
        [HttpPut("UpdateEmergencyContact")]
        public async Task<IActionResult> UpdateEmergencyContact([FromBody] UpdateEmergencyContactRequest request)
        {
            try
            {
                var userEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;

                if (string.IsNullOrEmpty(userEmployeeIdClaim))
                {
                    return Unauthorized(new { message = "Employee information not found" });
                }

                var employeeId = int.Parse(userEmployeeIdClaim);
                var employee = await _context.Employees.FindAsync(employeeId);

                if (employee == null)
                {
                    return NotFound(new { message = "Employee not found" });
                }

                // Update emergency contact
                employee.EmergencyContactName = request.EmergencyContactName;
                employee.EmergencyContactPhone = request.EmergencyContactPhone;
                employee.EmergencyContactRelationship = request.EmergencyContactRelationship;
                employee.UpdatedAt = DateTime.UtcNow;
                employee.UpdatedBy = employeeId;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Emergency contact updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating emergency contact", error = ex.Message });
            }
        }

        // ============================================
        // GET: api/Profile/Banking
        // Get banking information (masked)
        // ============================================
        [HttpGet("Banking")]
        public async Task<IActionResult> GetBanking()
        {
            try
            {
                var userEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;

                if (string.IsNullOrEmpty(userEmployeeIdClaim))
                {
                    return Unauthorized(new { message = "Employee information not found" });
                }

                var employeeId = int.Parse(userEmployeeIdClaim);

                var banking = await _context.Banking
                    .FirstOrDefaultAsync(b => b.EmployeeId == employeeId);

                if (banking == null)
                {
                    return Ok(new { message = "No banking information found", banking = (object)null });
                }

                return Ok(new
                {
                    bankingId = banking.BankingId,
                    bankName = banking.BankName,
                    accountHolderName = banking.AccountHolderName,
                    accountNumber = MaskAccountNumber(banking.BankAccountNumber),
                    routingNumber = banking.BankRoutingNumber,
                    accountType = banking.AccountType,
                    isVerified = banking.IsVerified,
                    updatedAt = banking.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving banking information", error = ex.Message });
            }
        }

        // ============================================
        // POST: api/Profile/Banking
        // Add or update banking information
        // ============================================
        [HttpPost("Banking")]
        public async Task<IActionResult> AddOrUpdateBanking([FromBody] BankingRequest request)
        {
            try
            {
                var userEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;

                if (string.IsNullOrEmpty(userEmployeeIdClaim))
                {
                    return Unauthorized(new { message = "Employee information not found" });
                }

                var employeeId = int.Parse(userEmployeeIdClaim);

                // Check if banking record exists
                var existingBanking = await _context.Banking
                    .FirstOrDefaultAsync(b => b.EmployeeId == employeeId);

                if (existingBanking != null)
                {
                    // Update existing
                    existingBanking.BankName = request.BankName;
                    existingBanking.AccountHolderName = request.AccountHolderName;
                    existingBanking.BankAccountNumber = request.BankAccountNumber;
                    existingBanking.BankRoutingNumber = request.BankRoutingNumber;
                    existingBanking.AccountType = request.AccountType;
                    existingBanking.IsVerified = false; // Needs re-verification
                    existingBanking.UpdatedAt = DateTime.UtcNow;
                    existingBanking.UpdatedBy = employeeId;
                }
                else
                {
                    // Create new
                    var newBanking = new Banking
                    {
                        EmployeeId = employeeId,
                        BankName = request.BankName,
                        AccountHolderName = request.AccountHolderName,
                        BankAccountNumber = request.BankAccountNumber,
                        BankRoutingNumber = request.BankRoutingNumber,
                        AccountType = request.AccountType,
                        IsVerified = false,
                        CreatedBy = employeeId,
                        UpdatedBy = employeeId
                    };
                    _context.Banking.Add(newBanking);
                }

                await _context.SaveChangesAsync();

                // TODO: Send notification to Payroll/Admin for verification

                return Ok(new { message = "Banking information submitted for verification" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error saving banking information", error = ex.Message });
            }
        }

        // ============================================
        // POST: api/Profile/ChangePassword
        // Change user password
        // ============================================
        [HttpPost("ChangePassword")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return Unauthorized(new { message = "User information not found" });
                }

                var userId = int.Parse(userIdClaim);
                var user = await _context.Users.FindAsync(userId);

                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                // Verify current password
                bool isCurrentPasswordValid = BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash);

                if (!isCurrentPasswordValid)
                {
                    return BadRequest(new { message = "Current password is incorrect" });
                }

                // Validate new password
                if (request.NewPassword.Length < 6)
                {
                    return BadRequest(new { message = "New password must be at least 6 characters long" });
                }

                if (request.NewPassword != request.ConfirmPassword)
                {
                    return BadRequest(new { message = "New password and confirmation do not match" });
                }

                // Hash and save new password
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                //user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Password changed successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error changing password", error = ex.Message });
            }
        }

        // ============================================
        // Helper Methods
        // ============================================

        private string MaskAccountNumber(string accountNumber)
        {
            if (string.IsNullOrEmpty(accountNumber) || accountNumber.Length < 4)
            {
                return "****";
            }

            return "****" + accountNumber.Substring(accountNumber.Length - 4);
        }
    }

    // ============================================
    // Request Models
    // ============================================

    public class UpdateContactRequest
    {
        public string? PhoneNumber { get; set; }
        public string? PersonalEmail { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
        public string? Country { get; set; }
    }

    public class UpdateEmergencyContactRequest
    {
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactPhone { get; set; }
        public string? EmergencyContactRelationship { get; set; }
    }

    public class BankingRequest
    {
        public string BankName { get; set; }
        public string AccountHolderName { get; set; }
        public string BankAccountNumber { get; set; }
        public string BankRoutingNumber { get; set; }
        public string? AccountType { get; set; } // 'Checking' or 'Savings'
    }

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }
    }
}