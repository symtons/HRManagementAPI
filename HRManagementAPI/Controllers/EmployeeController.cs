using HRManagementAPI.Data;
using HRManagementAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

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
                // Get current user's role
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

                // Find the employee to update
                var employee = await _context.Employees
                    .Include(e => e.User)
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.EmployeeId == id);

                if (employee == null)
                {
                    return NotFound(new { message = "Employee not found" });
                }

                // Check permission: Directors can only edit their department employees
                if (role.RoleName == "Director" && !string.IsNullOrEmpty(userEmployeeIdClaim))
                {
                    var currentEmployee = await _context.Employees.FindAsync(int.Parse(userEmployeeIdClaim));
                    if (currentEmployee == null || employee.DepartmentId != currentEmployee.DepartmentId)
                    {
                        return Forbid("You can only edit employees in your department");
                    }
                }

                // Validate department if changed
                if (request.DepartmentId.HasValue && request.DepartmentId != employee.DepartmentId)
                {
                    var department = await _context.Departments.FindAsync(request.DepartmentId.Value);
                    if (department == null)
                    {
                        return BadRequest(new { message = "Invalid department" });
                    }
                }

                // Validate employee code uniqueness if changed
                if (!string.IsNullOrEmpty(request.EmployeeCode) && request.EmployeeCode != employee.EmployeeCode)
                {
                    if (await _context.Employees.AnyAsync(e => e.EmployeeCode == request.EmployeeCode && e.EmployeeId != id))
                    {
                        return BadRequest(new { message = "Employee code already exists" });
                    }
                }

                // Update employee fields
                employee.FirstName = request.FirstName ?? employee.FirstName;
                employee.LastName = request.LastName ?? employee.LastName;
                employee.EmployeeCode = request.EmployeeCode ?? employee.EmployeeCode;
                employee.DepartmentId = request.DepartmentId ?? employee.DepartmentId;
                employee.JobTitle = request.JobTitle ?? employee.JobTitle;
                employee.EmployeeType = request.EmployeeType ?? employee.EmployeeType;

                // Personal Info
                if (request.DateOfBirth.HasValue) employee.DateOfBirth = request.DateOfBirth;
                if (!string.IsNullOrEmpty(request.Gender)) employee.Gender = request.Gender;
                if (!string.IsNullOrEmpty(request.PhoneNumber)) employee.PhoneNumber = request.PhoneNumber;
                if (!string.IsNullOrEmpty(request.PersonalEmail)) employee.PersonalEmail = request.PersonalEmail;

                // Address
                if (!string.IsNullOrEmpty(request.Address)) employee.Address = request.Address;
                employee.Address = request.Address; // Can be null
                if (!string.IsNullOrEmpty(request.City)) employee.City = request.City;
                if (!string.IsNullOrEmpty(request.State)) employee.State = request.State;
                if (!string.IsNullOrEmpty(request.ZipCode)) employee.ZipCode = request.ZipCode;

                // Employment Info
                if (request.HireDate.HasValue) employee.HireDate = request.HireDate;
                if (request.ManagerId.HasValue) employee.ManagerId = request.ManagerId;
                if (!string.IsNullOrEmpty(request.EmploymentStatus)) employee.EmploymentStatus = request.EmploymentStatus;

                // Emergency Contact
                if (!string.IsNullOrEmpty(request.EmergencyContactName)) employee.EmergencyContactName = request.EmergencyContactName;
                if (!string.IsNullOrEmpty(request.EmergencyContactRelationship)) employee.EmergencyContactRelationship = request.EmergencyContactRelationship;
                if (!string.IsNullOrEmpty(request.EmergencyContactPhone)) employee.EmergencyContactPhone = request.EmergencyContactPhone;

                // Update PTO eligibility if EmployeeType changed
                if (request.EmployeeType != null && request.EmployeeType != employee.EmployeeType)
                {
                    if (request.EmployeeType == "AdminStaff")
                    {
                        employee.IsEligibleForPTO = true;
                        employee.IsEligibleForInsurance = true;
                    }
                    else
                    {
                        employee.IsEligibleForPTO = false;
                        employee.IsEligibleForInsurance = false;
                    }
                }

                employee.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Employee updated successfully",
                    employeeId = employee.EmployeeId,
                    employeeCode = employee.EmployeeCode
                });
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
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? PhoneNumber { get; set; }
        public string? PersonalEmail { get; set; }

        // Address
        public string? Address { get; set; }

        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }

        // Employment Info
        public string? EmployeeCode { get; set; }
        public string? EmployeeType { get; set; }
        public int? DepartmentId { get; set; }
        public string? JobTitle { get; set; }
        public DateTime? HireDate { get; set; }
        public int? ManagerId { get; set; }
        public string? EmploymentStatus { get; set; }

        // Emergency Contact
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactRelationship { get; set; }
        public string? EmergencyContactPhone { get; set; }
    }

}