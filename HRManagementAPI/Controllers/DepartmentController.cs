// HRManagementAPI/Controllers/DepartmentController.cs
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
    public class DepartmentController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DepartmentController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================
        // GET: api/Department/All
        // Get all departments with employee counts
        // ============================================
        [HttpGet("All")]
        public async Task<IActionResult> GetAllDepartments()
        {
            try
            {
                var departments = await _context.Departments
                    .Where(d => d.IsActive)
                    .Select(d => new
                    {
                        d.DepartmentId,
                        d.DepartmentName,
                        d.DepartmentCode,
                        d.Description,
                        d.IsActive,
                        EmployeeCount = d.Employees.Count(e => e.IsActive),
                        AdminStaffCount = d.Employees.Count(e => e.IsActive && e.EmployeeType == "AdminStaff"),
                        FieldStaffCount = d.Employees.Count(e => e.IsActive && e.EmployeeType == "FieldStaff"),
                        DepartmentHead = d.Employees
                            .Where(e => e.IsActive && e.User.Role.RoleName == "Director")
                            .Select(e => e.FirstName + " " + e.LastName)
                            .FirstOrDefault()
                    })
                    .OrderBy(d => d.DepartmentName)
                    .ToListAsync();

                return Ok(departments);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching departments", error = ex.Message });
            }
        }

        // ============================================
        // GET: api/Department/{id}
        // Get single department details
        // ============================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetDepartmentById(int id)
        {
            try
            {
                var department = await _context.Departments
                    .Where(d => d.DepartmentId == id)
                    .Select(d => new
                    {
                        d.DepartmentId,
                        d.DepartmentName,
                        d.DepartmentCode,
                        d.Description,
                        d.IsActive,
                        d.CreatedAt,

                        // Statistics
                        TotalEmployees = d.Employees.Count(e => e.IsActive),
                        AdminStaffCount = d.Employees.Count(e => e.IsActive && e.EmployeeType == "AdminStaff"),
                        FieldStaffCount = d.Employees.Count(e => e.IsActive && e.EmployeeType == "FieldStaff"),
                        ActiveEmployees = d.Employees.Count(e => e.IsActive && e.EmploymentStatus == "Active"),
                        OnLeaveEmployees = d.Employees.Count(e => e.IsActive && e.EmploymentStatus == "OnLeave"),

                        // Director info
                        Director = d.Employees
                            .Where(e => e.IsActive && e.User.Role.RoleName == "Director")
                            .Select(e => new
                            {
                                e.EmployeeId,
                                FullName = e.FirstName + " " + e.LastName,
                                Email = e.User.Email,
                                e.PhoneNumber
                            })
                            .FirstOrDefault()
                    })
                    .FirstOrDefaultAsync();

                if (department == null)
                {
                    return NotFound(new { message = "Department not found" });
                }

                return Ok(department);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching department", error = ex.Message });
            }
        }

        // ============================================
        // GET: api/Department/{id}/Employees
        // Get all employees in a department
        // ============================================
        [HttpGet("{id}/Employees")]
        public async Task<IActionResult> GetDepartmentEmployees(int id)
        {
            try
            {
                // Verify department exists
                var departmentExists = await _context.Departments.AnyAsync(d => d.DepartmentId == id);
                if (!departmentExists)
                {
                    return NotFound(new { message = "Department not found" });
                }

                var employees = await _context.Employees
                    .Where(e => e.DepartmentId == id && e.IsActive)
                    .Include(e => e.User)
                        .ThenInclude(u => u.Role)
                    .Select(e => new
                    {
                        e.EmployeeId,
                        e.EmployeeCode,
                        FullName = e.FirstName + " " + e.LastName,
                        e.FirstName,
                        e.LastName,
                        e.JobTitle,
                        e.EmployeeType,
                        e.EmploymentStatus,
                        e.HireDate,
                        e.PhoneNumber,
                        Email = e.User.Email,
                        Role = e.User.Role.RoleName,
                        RoleLevel = e.User.Role.RoleLevel
                    })
                    .OrderBy(e => e.RoleLevel)
                    .ThenBy(e => e.LastName)
                    .ToListAsync();

                return Ok(new
                {
                    departmentId = id,
                    totalEmployees = employees.Count,
                    employees = employees
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching department employees", error = ex.Message });
            }
        }

        // ============================================
        // GET: api/Department/{id}/Stats
        // Get department statistics
        // ============================================
        [HttpGet("{id}/Stats")]
        public async Task<IActionResult> GetDepartmentStats(int id)
        {
            try
            {
                var department = await _context.Departments
                    .Where(d => d.DepartmentId == id)
                    .Select(d => new
                    {
                        d.DepartmentId,
                        d.DepartmentName,
                        d.DepartmentCode,

                        // Employee Statistics
                        TotalEmployees = d.Employees.Count(e => e.IsActive),
                        AdminStaffCount = d.Employees.Count(e => e.IsActive && e.EmployeeType == "AdminStaff"),
                        FieldStaffCount = d.Employees.Count(e => e.IsActive && e.EmployeeType == "FieldStaff"),

                        // Employment Status
                        ActiveEmployees = d.Employees.Count(e => e.IsActive && e.EmploymentStatus == "Active"),
                        OnLeaveEmployees = d.Employees.Count(e => e.IsActive && e.EmploymentStatus == "OnLeave"),

                        // Role Distribution
                        DirectorCount = d.Employees.Count(e => e.IsActive && e.User.Role.RoleName == "Director"),
                        ManagerCount = d.Employees.Count(e => e.IsActive &&
                            (e.User.Role.RoleName == "ProgramCoordinator" || e.User.Role.RoleName == "FieldOperatorManager")),
                        StaffCount = d.Employees.Count(e => e.IsActive && e.User.Role.RoleName == "FieldOperator"),

                        // Benefits
                        PTOEligibleCount = d.Employees.Count(e => e.IsActive && e.IsEligibleForPTO),
                        InsuranceEligibleCount = d.Employees.Count(e => e.IsActive && e.IsEligibleForInsurance),

                        // Recent Hires (last 90 days)
                        RecentHires = d.Employees.Count(e => e.IsActive &&
                            e.HireDate.HasValue &&
                            e.HireDate.Value >= DateTime.UtcNow.AddDays(-90))
                    })
                    .FirstOrDefaultAsync();

                if (department == null)
                {
                    return NotFound(new { message = "Department not found" });
                }

                return Ok(department);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching department statistics", error = ex.Message });
            }
        }

        // ============================================
        // POST: api/Department
        // Create new department (Admin/Executive only)
        // ============================================
        [HttpPost]
        [Authorize(Roles = "Admin,Executive")]
        public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentRequest request)
        {
            try
            {
                // Validate request
                if (string.IsNullOrWhiteSpace(request.DepartmentName))
                {
                    return BadRequest(new { message = "Department name is required" });
                }

                if (string.IsNullOrWhiteSpace(request.DepartmentCode))
                {
                    return BadRequest(new { message = "Department code is required" });
                }

                // Check for duplicate department name
                if (await _context.Departments.AnyAsync(d => d.DepartmentName == request.DepartmentName))
                {
                    return BadRequest(new { message = "Department name already exists" });
                }

                // Check for duplicate department code
                if (await _context.Departments.AnyAsync(d => d.DepartmentCode == request.DepartmentCode))
                {
                    return BadRequest(new { message = "Department code already exists" });
                }

                // Create new department
                var department = new Department
                {
                    DepartmentName = request.DepartmentName.Trim(),
                    DepartmentCode = request.DepartmentCode.Trim().ToUpper(),
                    Description = request.Description?.Trim(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Departments.Add(department);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Department created successfully",
                    departmentId = department.DepartmentId,
                    departmentName = department.DepartmentName,
                    departmentCode = department.DepartmentCode
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error creating department", error = ex.Message });
            }
        }

        // ============================================
        // PUT: api/Department/{id}
        // Update department (Admin/Executive only)
        // ============================================
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Executive")]
        public async Task<IActionResult> UpdateDepartment(int id, [FromBody] UpdateDepartmentRequest request)
        {
            try
            {
                var department = await _context.Departments.FindAsync(id);
                if (department == null)
                {
                    return NotFound(new { message = "Department not found" });
                }

                // Check for duplicate name (excluding current department)
                if (!string.IsNullOrWhiteSpace(request.DepartmentName) &&
                    request.DepartmentName != department.DepartmentName)
                {
                    if (await _context.Departments.AnyAsync(d => d.DepartmentName == request.DepartmentName && d.DepartmentId != id))
                    {
                        return BadRequest(new { message = "Department name already exists" });
                    }
                    department.DepartmentName = request.DepartmentName.Trim();
                }

                // Check for duplicate code (excluding current department)
                if (!string.IsNullOrWhiteSpace(request.DepartmentCode) &&
                    request.DepartmentCode != department.DepartmentCode)
                {
                    if (await _context.Departments.AnyAsync(d => d.DepartmentCode == request.DepartmentCode && d.DepartmentId != id))
                    {
                        return BadRequest(new { message = "Department code already exists" });
                    }
                    department.DepartmentCode = request.DepartmentCode.Trim().ToUpper();
                }

                // Update other fields
                if (request.Description != null)
                {
                    department.Description = request.Description.Trim();
                }

                if (request.IsActive.HasValue)
                {
                    department.IsActive = request.IsActive.Value;
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Department updated successfully",
                    departmentId = department.DepartmentId,
                    departmentName = department.DepartmentName,
                    departmentCode = department.DepartmentCode,
                    isActive = department.IsActive
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating department", error = ex.Message });
            }
        }

        // ============================================
        // DELETE: api/Department/{id}
        // Soft delete department (Admin only)
        // ============================================
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteDepartment(int id)
        {
            try
            {
                var department = await _context.Departments.FindAsync(id);
                if (department == null)
                {
                    return NotFound(new { message = "Department not found" });
                }

                // Check if department has active employees
                var hasActiveEmployees = await _context.Employees
                    .AnyAsync(e => e.DepartmentId == id && e.IsActive);

                if (hasActiveEmployees)
                {
                    return BadRequest(new { message = "Cannot delete department with active employees. Please reassign employees first." });
                }

                // Soft delete (set IsActive to false)
                department.IsActive = false;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Department deactivated successfully",
                    departmentId = department.DepartmentId,
                    departmentName = department.DepartmentName
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error deleting department", error = ex.Message });
            }
        }
    }

    // ============================================
    // REQUEST MODELS
    // ============================================

    public class CreateDepartmentRequest
    {
        public string DepartmentName { get; set; }
        public string DepartmentCode { get; set; }
        public string? Description { get; set; }
    }

    public class UpdateDepartmentRequest
    {
        public string? DepartmentName { get; set; }
        public string? DepartmentCode { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
    }
}