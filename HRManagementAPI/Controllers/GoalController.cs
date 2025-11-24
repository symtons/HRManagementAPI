using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRManagementAPI.Data;
using HRManagementAPI.Models;
using System.Security.Claims;

namespace HRManagementAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class GoalController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public GoalController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =============================================
        // GET: My Goals
        // =============================================
        [HttpGet("MyGoals")]
        public async Task<IActionResult> GetMyGoals([FromQuery] string? status, [FromQuery] string? category)
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                var query = _context.Goals
                    .Include(g => g.GoalUpdates.OrderByDescending(u => u.UpdateDate))
                    .Include(g => g.Creator)
                    .Where(g => g.EmployeeId == employeeId)
                    .AsQueryable();

                // Filter by status
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(g => g.Status == status);
                }

                // Filter by category
                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(g => g.Category == category);
                }

                var goals = await query
                    .OrderBy(g => g.DueDate)
                    .Select(g => new
                    {
                        g.GoalId,
                        g.EmployeeId,
                        g.Title,
                        g.Description,
                        g.Category,
                        g.Priority,
                        g.StartDate,
                        g.DueDate,
                        g.Progress,
                        g.Status,
                        CreatedBy = g.Creator.FirstName + " " + g.Creator.LastName,
                        UpdateCount = g.GoalUpdates.Count,
                        LatestUpdate = g.GoalUpdates.OrderByDescending(u => u.UpdateDate).FirstOrDefault(),
                        g.CreatedAt,
                        g.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(new { goals });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving goals", error = ex.Message });
            }
        }

        // =============================================
        // GET: Goal by ID
        // =============================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetGoal(int id)
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");
                var role = User.FindFirst(ClaimTypes.Role)?.Value;

                var goal = await _context.Goals
                    .Include(g => g.Employee)
                    .Include(g => g.Creator)
                    .Include(g => g.GoalUpdates.OrderByDescending(u => u.UpdateDate))
                        .ThenInclude(u => u.Updater)
                    .FirstOrDefaultAsync(g => g.GoalId == id);

                if (goal == null)
                    return NotFound(new { message = "Goal not found" });

                // Check permissions
                bool canView = goal.EmployeeId == employeeId ||
                              role == "Admin" || role == "Executive" || role == "Director" ||
                              (goal.Employee.ManagerId == employeeId);

                if (!canView)
                    return Forbid();

                var result = new
                {
                    goal.GoalId,
                    goal.EmployeeId,
                    EmployeeName = goal.Employee.FirstName + " " + goal.Employee.LastName,
                    goal.Title,
                    goal.Description,
                    goal.Category,
                    goal.Priority,
                    goal.StartDate,
                    goal.DueDate,
                    goal.Progress,
                    goal.Status,
                    CreatedBy = goal.Creator.FirstName + " " + goal.Creator.LastName,
                    Updates = goal.GoalUpdates.Select(u => new
                    {
                        u.UpdateId,
                        u.UpdateText,
                        u.Progress,
                        UpdatedBy = u.Updater.FirstName + " " + u.Updater.LastName,
                        u.UpdateDate
                    }).ToList(),
                    goal.CreatedAt,
                    goal.UpdatedAt
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving goal", error = ex.Message });
            }
        }

        // =============================================
        // POST: Create Goal
        // =============================================
        [HttpPost]
        public async Task<IActionResult> CreateGoal([FromBody] CreateGoalRequest request)
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                var goal = new Goal
                {
                    EmployeeId = request.EmployeeId ?? employeeId,
                    Title = request.Title,
                    Description = request.Description,
                    Category = request.Category,
                    Priority = request.Priority ?? "Medium",
                    StartDate = request.StartDate,
                    DueDate = request.DueDate,
                    Progress = 0,
                    Status = "NotStarted",
                    CreatedBy = employeeId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Goals.Add(goal);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Goal created successfully", goalId = goal.GoalId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error creating goal", error = ex.Message });
            }
        }

        // =============================================
        // PUT: Update Goal
        // =============================================
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateGoal(int id, [FromBody] UpdateGoalRequest request)
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                var goal = await _context.Goals
                    .Include(g => g.Employee)
                    .FirstOrDefaultAsync(g => g.GoalId == id);

                if (goal == null)
                    return NotFound(new { message = "Goal not found" });

                // Check permissions
                bool canEdit = goal.EmployeeId == employeeId ||
                              goal.Employee.ManagerId == employeeId;

                if (!canEdit)
                    return Forbid();

                if (request.Title != null) goal.Title = request.Title;
                if (request.Description != null) goal.Description = request.Description;
                if (request.Category != null) goal.Category = request.Category;
                if (request.Priority != null) goal.Priority = request.Priority;
                if (request.StartDate.HasValue) goal.StartDate = request.StartDate.Value;
                if (request.DueDate.HasValue) goal.DueDate = request.DueDate.Value;
                if (request.Progress.HasValue) goal.Progress = request.Progress.Value;
                if (request.Status != null) goal.Status = request.Status;

                goal.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Goal updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating goal", error = ex.Message });
            }
        }

        // =============================================
        // POST: Add Goal Update
        // =============================================
        [HttpPost("{id}/Update")]
        public async Task<IActionResult> AddGoalUpdate(int id, [FromBody] GoalUpdateRequest request)
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                var goal = await _context.Goals
                    .Include(g => g.Employee)
                    .FirstOrDefaultAsync(g => g.GoalId == id);

                if (goal == null)
                    return NotFound(new { message = "Goal not found" });

                // Check permissions
                bool canUpdate = goal.EmployeeId == employeeId ||
                                goal.Employee.ManagerId == employeeId;

                if (!canUpdate)
                    return Forbid();

                var update = new GoalUpdate
                {
                    GoalId = id,
                    UpdateText = request.UpdateText,
                    Progress = request.Progress,
                    UpdatedBy = employeeId,
                    UpdateDate = DateTime.UtcNow
                };

                _context.GoalUpdates.Add(update);

                // Update goal progress
                goal.Progress = request.Progress;

                // Update status based on progress
                if (request.Progress >= 100)
                {
                    goal.Status = "Completed";
                }
                else if (request.Progress > 0)
                {
                    goal.Status = "InProgress";
                }

                goal.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Goal update added successfully", updateId = update.UpdateId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error adding goal update", error = ex.Message });
            }
        }

        // =============================================
        // DELETE: Delete Goal
        // =============================================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGoal(int id)
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                var goal = await _context.Goals
                    .FirstOrDefaultAsync(g => g.GoalId == id);

                if (goal == null)
                    return NotFound(new { message = "Goal not found" });

                // Only the goal owner or creator can delete
                bool canDelete = goal.EmployeeId == employeeId || goal.CreatedBy == employeeId;

                if (!canDelete)
                    return Forbid();

                _context.Goals.Remove(goal);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Goal deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error deleting goal", error = ex.Message });
            }
        }

        // =============================================
        // GET: Team Goals (for managers)
        // =============================================
        [HttpGet("TeamGoals")]
        [Authorize(Roles = "Admin,Executive,Director,ProgramCoordinator,FieldOperatorManager")]
        public async Task<IActionResult> GetTeamGoals([FromQuery] string? status)
        {
            try
            {
                var managerId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");
                var role = User.FindFirst(ClaimTypes.Role)?.Value;

                var query = _context.Goals
                    .Include(g => g.Employee)
                    .AsQueryable();

                // Filter by supervisor (unless Admin)
                if (role != "Admin")
                {
                    query = query.Where(g => g.Employee.ManagerId == managerId);
                }

                // Filter by status
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(g => g.Status == status);
                }

                var goals = await query
                    .OrderBy(g => g.DueDate)
                    .Select(g => new
                    {
                        g.GoalId,
                        g.EmployeeId,
                        EmployeeName = g.Employee.FirstName + " " + g.Employee.LastName,
                        Department = g.Employee.Department != null ? g.Employee.Department.DepartmentName : null,
                        g.Title,
                        g.Category,
                        g.Priority,
                        g.DueDate,
                        g.Progress,
                        g.Status
                    })
                    .ToListAsync();

                return Ok(new { goals });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving team goals", error = ex.Message });
            }
        }

        // =============================================
        // GET: Goal Statistics
        // =============================================
        [HttpGet("Statistics")]
        public async Task<IActionResult> GetGoalStatistics()
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                var goals = await _context.Goals
                    .Where(g => g.EmployeeId == employeeId)
                    .ToListAsync();

                var stats = new
                {
                    totalGoals = goals.Count,
                    completedGoals = goals.Count(g => g.Status == "Completed"),
                    inProgressGoals = goals.Count(g => g.Status == "InProgress"),
                    notStartedGoals = goals.Count(g => g.Status == "NotStarted"),
                    overdueGoals = goals.Count(g => g.DueDate < DateTime.Today && g.Status != "Completed" && g.Status != "Cancelled"),
                    averageProgress = goals.Any() ? Math.Round(goals.Average(g => g.Progress), 0) : 0,
                    completionRate = goals.Any() ? Math.Round((decimal)goals.Count(g => g.Status == "Completed") / goals.Count * 100, 1) : 0
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving goal statistics", error = ex.Message });
            }
        }
    }

    // =============================================
    // Request Models
    // =============================================
    public class CreateGoalRequest
    {
        public int? EmployeeId { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string? Priority { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime DueDate { get; set; }
    }

    public class UpdateGoalRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string? Priority { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public int? Progress { get; set; }
        public string? Status { get; set; }
    }

    public class GoalUpdateRequest
    {
        public string UpdateText { get; set; }
        public int Progress { get; set; }
    }
}
