using HRManagementAPI.Data;
using HRManagementAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.Data.SqlClient;

namespace HRManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EmployeeController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public EmployeeController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("Directory")]
        public async Task<IActionResult> GetEmployeeDirectory(
             [FromQuery] string? search = null,
             [FromQuery] int? departmentId = null,
             [FromQuery] string? employeeType = null,
             [FromQuery] string? employmentStatus = null,
             [FromQuery][Range(1, int.MaxValue)] int pageNumber = 1,  // ✅ FIXED: Must be >= 1

             [FromQuery][Range(1, 100)] int pageSize = 10)
        {
            try
            {
                // Get current user's role and employee info
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var userEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;

                if (string.IsNullOrEmpty(userRole))
                {
                    return Unauthorized(new { message = "User role not found" });
                }

                // Get role details
                var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == userRole);
                if (role == null)
                {
                    return NotFound(new { message = "Role not found" });
                }

                // Start with base query
                var query = _context.Employees
                    .Include(e => e.Department)
                    .Include(e => e.Manager)
                    .Where(e => e.IsActive);

                // Apply role-based filtering
                if (role.RoleName == "Director" && !string.IsNullOrEmpty(userEmployeeIdClaim))
                {
                    // Directors only see their department employees
                    var currentEmployee = await _context.Employees
                        .FirstOrDefaultAsync(e => e.EmployeeId == int.Parse(userEmployeeIdClaim));

                    if (currentEmployee != null && currentEmployee.DepartmentId.HasValue)
                    {
                        query = query.Where(e => e.DepartmentId == currentEmployee.DepartmentId);
                    }
                }
                // Admin, Executive, and Program Coordinator see all employees

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.ToLower();
                    query = query.Where(e =>
                        e.FirstName.ToLower().Contains(search) ||
                        e.LastName.ToLower().Contains(search) ||
                        e.EmployeeCode.ToLower().Contains(search) ||
                        (e.PersonalEmail != null && e.PersonalEmail.ToLower().Contains(search)) ||
                        (e.JobTitle != null && e.JobTitle.ToLower().Contains(search))
                    );
                }

                // Apply department filter
                if (departmentId.HasValue)
                {
                    query = query.Where(e => e.DepartmentId == departmentId.Value);
                }

                // Apply employee type filter
                if (!string.IsNullOrWhiteSpace(employeeType))
                {
                    query = query.Where(e => e.EmployeeType == employeeType);
                }

                // Apply employment status filter
                if (!string.IsNullOrWhiteSpace(employmentStatus))
                {
                    query = query.Where(e => e.EmploymentStatus == employmentStatus);
                }

                // Get total count before pagination
                var totalCount = await query.CountAsync();

                // Apply pagination - pageNumber is 1-based
                var employees = await query
                    .OrderBy(e => e.LastName)
                    .ThenBy(e => e.FirstName)
                    .Skip((pageNumber - 1) * pageSize)  // ✅ 1-based: (1-1)*10=0, (2-1)*10=10, etc.
                    .Take(pageSize)
                    .Select(e => new
                    {
                        e.EmployeeId,
                        e.EmployeeCode,
                        e.FirstName,
                        e.LastName,
                        FullName = $"{e.FirstName} {e.LastName}",
                        e.JobTitle,
                        e.EmployeeType,
                        e.EmploymentStatus,
                        e.PhoneNumber,
                        e.PersonalEmail,
                        Department = e.Department != null ? new
                        {
                            e.Department.DepartmentId,
                            e.Department.DepartmentName,
                            e.Department.DepartmentCode
                        } : null,
                        Manager = e.Manager != null ? new
                        {
                            e.Manager.EmployeeId,
                            ManagerName = $"{e.Manager.FirstName} {e.Manager.LastName}"
                        } : null,
                        e.HireDate,
                        e.IsEligibleForPTO,
                        e.PTOBalance,
                        e.IsEligibleForInsurance
                    })
                    .ToListAsync();

                return Ok(new
                {
                    employees,
                    totalCount,
                    pageNumber,
                    pageSize,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving employees", error = ex.Message });
            }
        }



        // GET: api/Employee/{id}
        // Get single employee details
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEmployee(int id)
        {
            try
            {
                // Get current user's role and employee info
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var userEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;

                if (string.IsNullOrEmpty(userRole))
                {
                    return Unauthorized(new { message = "User role not found" });
                }

                // Get role details
                var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == userRole);
                if (role == null)
                {
                    return NotFound(new { message = "Role not found" });
                }

                var employee = await _context.Employees
                    .Include(e => e.Department)
                    .Include(e => e.Manager)
                    .Include(e => e.User)
                        .ThenInclude(u => u.Role)
                    .FirstOrDefaultAsync(e => e.EmployeeId == id);

                if (employee == null)
                {
                    return NotFound(new { message = "Employee not found" });
                }

                // Check if Director is trying to access employee outside their department
                if (role.RoleName == "Director" && !string.IsNullOrEmpty(userEmployeeIdClaim))
                {
                    var currentEmployee = await _context.Employees
                        .FirstOrDefaultAsync(e => e.EmployeeId == int.Parse(userEmployeeIdClaim));

                    if (currentEmployee != null &&
                        currentEmployee.DepartmentId.HasValue &&
                        employee.DepartmentId != currentEmployee.DepartmentId)
                    {
                        return Forbid(); // Director trying to access another department's employee
                    }
                }

                // Return detailed employee information
                var result = new
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
                    employee.TerminationDate,
                    SSN = (role.RoleName == "Admin" || role.RoleName == "Executive") ? employee.SSN : null,
                    // Additional Information (NEW - from bulk import)
                    employee.SSNLast4,
                    employee.WorkHoursCategory,
                    employee.DriversLicenseExpiration,
                    employee.NursingLicenseExpiration,

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

                    // Compensation (only for authorized roles)
                    Salary = (role.RoleName == "Admin" || role.RoleName == "Executive") ? employee.Salary : null,
                    employee.PayFrequency,

                    // Banking (only for Admin and the employee themselves)
                    BankName = (role.RoleName == "Admin" ||
                                (userEmployeeIdClaim == employee.EmployeeId.ToString()))
                                ? employee.BankName : null,
                    BankAccountNumber = (role.RoleName == "Admin" ||
                                        (userEmployeeIdClaim == employee.EmployeeId.ToString()))
                                        ? employee.BankAccountNumber : null,
                    BankRoutingNumber = (role.RoleName == "Admin" ||
                                        (userEmployeeIdClaim == employee.EmployeeId.ToString()))
                                        ? employee.BankRoutingNumber : null,

                    // Benefits (NEW - includes all benefit eligibility flags)
                    employee.IsEligibleForPTO,
                    employee.PTOBalance,
                    employee.IsEligibleForInsurance,
                    employee.IsEligibleForDental,
                    employee.IsEligibleForVision,
                    employee.IsEligibleForLife,
                    employee.IsEligibleFor403B,

                    // User Account
                    User = employee.User != null ? new
                    {
                        employee.User.UserId,
                        employee.User.Email,
                        Role = employee.User.Role.RoleName,
                        employee.User.IsActive,
                        employee.User.LastLoginAt
                    } : null,

                    // Metadata
                    employee.CreatedAt,
                    employee.UpdatedAt
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving employee", error = ex.Message });
            }
        }
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEmployee(int id, [FromBody] UpdateEmployeeRequest request)
        {
            try
            {
                // Get current user info
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var userEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(userRole))
                {
                    return Unauthorized(new { message = "User information not found" });
                }

                var userId = int.Parse(userIdClaim);

                // Check permissions
                var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == userRole);
                if (role == null)
                {
                    return NotFound(new { message = "Role not found" });
                }

                // Directors can only edit their department employees
                if (role.RoleName == "Director" && !string.IsNullOrEmpty(userEmployeeIdClaim))
                {
                    var currentEmployee = await _context.Employees.FindAsync(int.Parse(userEmployeeIdClaim));
                    var targetEmployee = await _context.Employees.FindAsync(id);

                    if (currentEmployee == null || targetEmployee == null ||
                        targetEmployee.DepartmentId != currentEmployee.DepartmentId)
                    {
                        return Forbid("You can only edit employees in your department");
                    }
                }

                // Call stored procedure
                var parameters = new[]
                {
            new SqlParameter("@EmployeeId", id),
            new SqlParameter("@UpdatedByUserId", userId),
            
            // Personal Info
            new SqlParameter("@FirstName", request.FirstName),
            new SqlParameter("@LastName", request.LastName),
            new SqlParameter("@MiddleName", (object)request.MiddleName ?? DBNull.Value),
            new SqlParameter("@DateOfBirth", (object)request.DateOfBirth ?? DBNull.Value),
            new SqlParameter("@Gender", (object)request.Gender ?? DBNull.Value),
            new SqlParameter("@MaritalStatus", (object)request.MaritalStatus ?? DBNull.Value),
            
            // Contact
            new SqlParameter("@PhoneNumber", (object)request.PhoneNumber ?? DBNull.Value),
            new SqlParameter("@PersonalEmail", (object)request.PersonalEmail ?? DBNull.Value),
            new SqlParameter("@Address", (object)request.Address ?? DBNull.Value),
            new SqlParameter("@City", (object)request.City ?? DBNull.Value),
            new SqlParameter("@State", (object)request.State ?? DBNull.Value),
            new SqlParameter("@ZipCode", (object)request.ZipCode ?? DBNull.Value),
            new SqlParameter("@Country", (object)request.Country ?? DBNull.Value),
            
            // Emergency Contact
            new SqlParameter("@EmergencyContactName", (object)request.EmergencyContactName ?? DBNull.Value),
            new SqlParameter("@EmergencyContactPhone", (object)request.EmergencyContactPhone ?? DBNull.Value),
            new SqlParameter("@EmergencyContactRelationship", (object)request.EmergencyContactRelationship ?? DBNull.Value),
            
            // Employment
            new SqlParameter("@DepartmentId", (object)request.DepartmentId ?? DBNull.Value),
            new SqlParameter("@ManagerId", (object)request.ManagerId ?? DBNull.Value),
            new SqlParameter("@JobTitle", (object)request.JobTitle ?? DBNull.Value),
            new SqlParameter("@EmployeeType", request.EmployeeType),
            new SqlParameter("@EmploymentType", request.EmploymentType),
            new SqlParameter("@PayFrequency", (object)request.PayFrequency ?? DBNull.Value),
            new SqlParameter("@Salary", (object)request.Salary ?? DBNull.Value),
            
            // Licenses
            new SqlParameter("@SSN", (object)request.SSN ?? DBNull.Value),
            new SqlParameter("@DriversLicenseNumber", (object)request.DriversLicenseNumber ?? DBNull.Value),
            new SqlParameter("@DriversLicenseState", (object)request.DriversLicenseState ?? DBNull.Value),
            new SqlParameter("@DriversLicenseExpiration", (object)request.DriversLicenseExpiration ?? DBNull.Value),
            new SqlParameter("@NursingLicenseNumber", (object)request.NursingLicenseNumber ?? DBNull.Value),
            new SqlParameter("@NursingLicenseState", (object)request.NursingLicenseState ?? DBNull.Value),
            new SqlParameter("@NursingLicenseExpiration", (object)request.NursingLicenseExpiration ?? DBNull.Value),
            
            // Benefits
            new SqlParameter("@IsEligibleForPTO", request.IsEligibleForPTO),
            new SqlParameter("@PTOBalance", request.PTOBalance),
            new SqlParameter("@IsEligibleForInsurance", request.IsEligibleForInsurance),
            new SqlParameter("@IsEligibleForDental", request.IsEligibleForDental),
            new SqlParameter("@IsEligibleForVision", request.IsEligibleForVision),
            new SqlParameter("@IsEligibleForLife", request.IsEligibleForLife),
            new SqlParameter("@IsEligibleFor403B", request.IsEligibleFor403B)
        };

                // Execute stored procedure
                var result = await _context.Database
                    .SqlQueryRaw<SpResult>("EXEC sp_UpdateEmployee " +
                        "@EmployeeId, @UpdatedByUserId, " +
                        "@FirstName, @LastName, @MiddleName, @DateOfBirth, @Gender, @MaritalStatus, " +
                        "@PhoneNumber, @PersonalEmail, @Address, @City, @State, @ZipCode, @Country, " +
                        "@EmergencyContactName, @EmergencyContactPhone, @EmergencyContactRelationship, " +
                        "@DepartmentId, @ManagerId, @JobTitle, @EmployeeType, @EmploymentType, @PayFrequency, @Salary, " +
                        "@SSN, @DriversLicenseNumber, @DriversLicenseState, @DriversLicenseExpiration, " +
                        "@NursingLicenseNumber, @NursingLicenseState, @NursingLicenseExpiration, " +
                        "@IsEligibleForPTO, @PTOBalance, @IsEligibleForInsurance, @IsEligibleForDental, " +
                        "@IsEligibleForVision, @IsEligibleForLife, @IsEligibleFor403B",
                        parameters)
                    .ToListAsync();

                if (result.Any() && result.First().Result == "Error")
                {
                    return BadRequest(new { message = result.First().Message });
                }

                return Ok(new { message = "Employee updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating employee", error = ex.Message });
            }
        }



        // GET: api/Employee/Stats
        // Get employee statistics for dashboard
        [HttpGet("Stats")]
        public async Task<IActionResult> GetEmployeeStats()
        {
            try
            {
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var userEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;

                var query = _context.Employees.Where(e => e.IsActive);

                // Apply role-based filtering
                if (userRole == "Director" && !string.IsNullOrEmpty(userEmployeeIdClaim))
                {
                    var currentEmployee = await _context.Employees
                        .FirstOrDefaultAsync(e => e.EmployeeId == int.Parse(userEmployeeIdClaim));

                    if (currentEmployee != null && currentEmployee.DepartmentId.HasValue)
                    {
                        query = query.Where(e => e.DepartmentId == currentEmployee.DepartmentId);
                    }
                }

                var stats = new
                {
                    TotalEmployees = await query.CountAsync(),
                    AdminStaff = await query.CountAsync(e => e.EmployeeType == "AdminStaff"),
                    FieldStaff = await query.CountAsync(e => e.EmployeeType == "FieldStaff"),
                    ByDepartment = await query
                        .GroupBy(e => e.Department.DepartmentName)
                        .Select(g => new
                        {
                            Department = g.Key,
                            Count = g.Count()
                        })
                        .ToListAsync(),
                    ByStatus = await query
                        .GroupBy(e => e.EmploymentStatus)
                        .Select(g => new
                        {
                            Status = g.Key,
                            Count = g.Count()
                        })
                        .ToListAsync()
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving stats", error = ex.Message });
            }
        }


    }
    public class UpdateEmployeeRequest
    {
        // Personal Info
        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        public string? MiddleName { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? MaritalStatus { get; set; }

        // Contact
        public string? PhoneNumber { get; set; }

        [EmailAddress]
        public string? PersonalEmail { get; set; }

        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
        public string? Country { get; set; }

        // Emergency Contact
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactPhone { get; set; }
        public string? EmergencyContactRelationship { get; set; }

        // Employment
        public int? DepartmentId { get; set; }
        public int? ManagerId { get; set; }
        public string? JobTitle { get; set; }

        [Required]
        public string EmployeeType { get; set; }

        [Required]
        public string EmploymentType { get; set; }

        public string? PayFrequency { get; set; }
        public decimal? Salary { get; set; }

        // Licenses
        public string? SSN { get; set; }
        public string? DriversLicenseNumber { get; set; }
        public string? DriversLicenseState { get; set; }
        public DateTime? DriversLicenseExpiration { get; set; }
        public string? NursingLicenseNumber { get; set; }
        public string? NursingLicenseState { get; set; }
        public DateTime? NursingLicenseExpiration { get; set; }

        // Benefits
        public bool IsEligibleForPTO { get; set; }
        public decimal PTOBalance { get; set; }
        public bool IsEligibleForInsurance { get; set; }
        public bool IsEligibleForDental { get; set; }
        public bool IsEligibleForVision { get; set; }
        public bool IsEligibleForLife { get; set; }
        public bool IsEligibleFor403B { get; set; }
    }

    // Helper class for stored procedure result
    public class SpResult
    {
        public string Result { get; set; }
        public string Message { get; set; }
    }
   

}