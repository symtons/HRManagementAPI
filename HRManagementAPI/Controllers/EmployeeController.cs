using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using HRManagementAPI.Data;
using HRManagementAPI.Models;

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

        // GET: api/Employee/Directory
        // Get employee directory with role-based filtering
        [HttpGet("Directory")]
        public async Task<IActionResult> GetEmployeeDirectory(
            [FromQuery] string? search = null,
            [FromQuery] int? departmentId = null,
            [FromQuery] string? employeeType = null,
            [FromQuery] string? employmentStatus = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
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

                // Apply pagination
                var employees = await query
                    .OrderBy(e => e.LastName)
                    .ThenBy(e => e.FirstName)
                    .Skip((pageNumber - 1) * pageSize)
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

                    // Benefits
                    employee.IsEligibleForPTO,
                    employee.PTOBalance,
                    employee.IsEligibleForInsurance,

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
}