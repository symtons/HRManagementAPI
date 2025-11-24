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
    public class ShiftController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ShiftController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================
        // GET ALL SHIFTS
        // ============================================
        [HttpGet]
        public async Task<IActionResult> GetAllShifts([FromQuery] bool? activeOnly)
        {
            try
            {
                var query = _context.Shifts.AsQueryable();

                if (activeOnly == true)
                {
                    query = query.Where(s => s.IsActive);
                }

                var shifts = await query
                    .OrderBy(s => s.StartTime)
                    .Select(s => new
                    {
                        s.ShiftId,
                        s.ShiftName,
                        s.ShiftCode,
                        s.StartTime,
                        s.EndTime,
                        s.WorkingHours,
                        s.BreakDuration,
                        s.IsActive
                    })
                    .ToListAsync();

                return Ok(shifts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving shifts", error = ex.Message });
            }
        }

        // ============================================
        // GET SHIFT BY ID
        // ============================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetShift(int id)
        {
            try
            {
                var shift = await _context.Shifts.FindAsync(id);

                if (shift == null)
                {
                    return NotFound(new { message = "Shift not found" });
                }

                return Ok(shift);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving shift", error = ex.Message });
            }
        }

        // ============================================
        // CREATE SHIFT
        // ============================================
        [HttpPost]
        [Authorize(Roles = "Admin,Executive")]
        public async Task<IActionResult> CreateShift([FromBody] ShiftRequest request)
        {
            try
            {
                var shift = new Shift
                {
                    ShiftName = request.ShiftName,
                    ShiftCode = request.ShiftCode,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    WorkingHours = request.WorkingHours,
                    BreakDuration = request.BreakDuration,
                    IsActive = request.IsActive ?? true
                };

                _context.Shifts.Add(shift);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Shift created successfully",
                    shift = new
                    {
                        shift.ShiftId,
                        shift.ShiftName,
                        shift.ShiftCode
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error creating shift", error = ex.Message });
            }
        }

        // ============================================
        // UPDATE SHIFT
        // ============================================
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Executive")]
        public async Task<IActionResult> UpdateShift(int id, [FromBody] ShiftRequest request)
        {
            try
            {
                var shift = await _context.Shifts.FindAsync(id);

                if (shift == null)
                {
                    return NotFound(new { message = "Shift not found" });
                }

                shift.ShiftName = request.ShiftName;
                shift.ShiftCode = request.ShiftCode;
                shift.StartTime = request.StartTime;
                shift.EndTime = request.EndTime;
                shift.WorkingHours = request.WorkingHours;
                shift.BreakDuration = request.BreakDuration;
                shift.IsActive = request.IsActive ?? shift.IsActive;
                shift.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Shift updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating shift", error = ex.Message });
            }
        }

        // ============================================
        // DELETE/DEACTIVATE SHIFT
        // ============================================
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Executive")]
        public async Task<IActionResult> DeleteShift(int id)
        {
            try
            {
                var shift = await _context.Shifts.FindAsync(id);

                if (shift == null)
                {
                    return NotFound(new { message = "Shift not found" });
                }

                // Soft delete - just deactivate
                shift.IsActive = false;
                shift.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Shift deactivated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error deleting shift", error = ex.Message });
            }
        }

        // ============================================
        // ASSIGN SHIFT TO EMPLOYEE
        // ============================================
        [HttpPost("Assign")]
        [Authorize(Roles = "Admin,Executive,Director,FieldOperatorManager")]
        public async Task<IActionResult> AssignShift([FromBody] AssignShiftRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                // Deactivate existing active shift for the employee
                var existingShifts = await _context.EmployeeShifts
                    .Where(es => es.EmployeeId == request.EmployeeId && es.IsActive)
                    .ToListAsync();

                foreach (var es in existingShifts)
                {
                    es.IsActive = false;
                    es.EndDate = request.EffectiveDate.AddDays(-1);
                }

                // Create new shift assignment
                var employeeShift = new EmployeeShift
                {
                    EmployeeId = request.EmployeeId,
                    ShiftId = request.ShiftId,
                    EffectiveDate = request.EffectiveDate,
                    IsActive = true,
                    AssignedBy = userId
                };

                _context.EmployeeShifts.Add(employeeShift);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Shift assigned successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error assigning shift", error = ex.Message });
            }
        }

        // ============================================
        // GET EMPLOYEE SHIFTS
        // ============================================
        [HttpGet("Employee/{employeeId}")]
        public async Task<IActionResult> GetEmployeeShifts(int employeeId)
        {
            try
            {
                var shifts = await _context.EmployeeShifts
                    .Include(es => es.Shift)
                    .Include(es => es.Employee)
                    .Where(es => es.EmployeeId == employeeId)
                    .OrderByDescending(es => es.EffectiveDate)
                    .Select(es => new
                    {
                        es.EmployeeShiftId,
                        es.ShiftId,
                        shift = new
                        {
                            es.Shift!.ShiftName,
                            es.Shift.ShiftCode,
                            es.Shift.StartTime,
                            es.Shift.EndTime,
                            es.Shift.WorkingHours
                        },
                        es.EffectiveDate,
                        es.EndDate,
                        es.IsActive
                    })
                    .ToListAsync();

                return Ok(shifts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving employee shifts", error = ex.Message });
            }
        }

        // ============================================
        // GET MY CURRENT SHIFT
        // ============================================
        [HttpGet("MyShift")]
        public async Task<IActionResult> GetMyShift()
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

                var today = DateTime.Today;
                var employeeShift = await _context.EmployeeShifts
                    .Include(es => es.Shift)
                    .Where(es => es.EmployeeId == user.Employee.EmployeeId &&
                                es.IsActive &&
                                es.EffectiveDate <= today &&
                                (es.EndDate == null || es.EndDate >= today))
                    .OrderByDescending(es => es.EffectiveDate)
                    .FirstOrDefaultAsync();

                if (employeeShift == null)
                {
                    return Ok(new { hasShift = false });
                }

                return Ok(new
                {
                    hasShift = true,
                    shift = new
                    {
                        employeeShift.Shift!.ShiftId,
                        employeeShift.Shift.ShiftName,
                        employeeShift.Shift.ShiftCode,
                        employeeShift.Shift.StartTime,
                        employeeShift.Shift.EndTime,
                        employeeShift.Shift.WorkingHours,
                        employeeShift.Shift.BreakDuration,
                        effectiveDate = employeeShift.EffectiveDate
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving shift", error = ex.Message });
            }
        }

        // ============================================
        // GET ALL EMPLOYEE SHIFT ASSIGNMENTS
        // ============================================
        [HttpGet("Assignments")]
        [Authorize(Roles = "Admin,Executive,Director,FieldOperatorManager")]
        public async Task<IActionResult> GetAllAssignments(
            [FromQuery] int? departmentId,
            [FromQuery] bool? activeOnly)
        {
            try
            {
                var query = _context.EmployeeShifts
                    .Include(es => es.Employee)
                        .ThenInclude(e => e!.Department)
                    .Include(es => es.Shift)
                    .AsQueryable();

                if (activeOnly == true)
                {
                    query = query.Where(es => es.IsActive);
                }

                if (departmentId.HasValue)
                {
                    query = query.Where(es => es.Employee!.DepartmentId == departmentId.Value);
                }

                var assignments = await query
                    .OrderBy(es => es.Employee!.FirstName)
                    .Select(es => new
                    {
                        es.EmployeeShiftId,
                        employeeId = es.EmployeeId,
                        employeeName = $"{es.Employee!.FirstName} {es.Employee.LastName}",
                        employeeCode = es.Employee.EmployeeCode,
                        department = es.Employee.Department != null ? es.Employee.Department.DepartmentName : null,
                        shift = new
                        {
                            es.Shift!.ShiftId,
                            es.Shift.ShiftName,
                            es.Shift.ShiftCode,
                            es.Shift.StartTime,
                            es.Shift.EndTime
                        },
                        es.EffectiveDate,
                        es.EndDate,
                        es.IsActive
                    })
                    .ToListAsync();

                return Ok(assignments);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving shift assignments", error = ex.Message });
            }
        }
    }

    // ============================================
    // REQUEST MODELS
    // ============================================
    public class ShiftRequest
    {
        public string ShiftName { get; set; } = string.Empty;
        public string ShiftCode { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public decimal WorkingHours { get; set; }
        public int BreakDuration { get; set; }
        public bool? IsActive { get; set; }
    }

    public class AssignShiftRequest
    {
        public int EmployeeId { get; set; }
        public int ShiftId { get; set; }
        public DateTime EffectiveDate { get; set; }
    }
}
