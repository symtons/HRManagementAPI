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

        // GET: My Goals
        [HttpGet("MyGoals")]
        public async Task<IActionResult> GetMyGoals([FromQuery] string status = null)
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                var query = _context.Goals
                    .Include(g => g.Creator)
                    .Where(g => g.EmployeeId == employeeId);

                if (!string.IsNullOrEmpty(status))
                    query = query.Where(g => g.Status == status);

                var goals = await query
                    .OrderBy(g => g.Status == "Completed" ? 1 : 0)
                    .ThenBy(g => g.DueDate)
                    .Select(g => new
                    {
                        g.GoalId,
                        g.Title,
                        g.Description,
                        g.DueDate,
                        g.Progress,
                        g.Status,
                        CreatedBy = g.Creator.FirstName + " " + g.Creator.LastName,
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

        // POST: Create Goal
        [HttpPost]
        public async Task<IActionResult> CreateGoal([FromBody] CreateGoalRequest request)
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                var goal = new Goal
                {
                    EmployeeId = request.EmployeeId ?? employeeId,
                    CreatedBy = employeeId,
                    Title = request.Title,
                    Description = request.Description,
                    DueDate = request.DueDate,
                    Progress = 0,
                    Status = "Active"
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

        // PUT: Update Progress
        [HttpPut("{id}/Progress")]
        public async Task<IActionResult> UpdateProgress(int id, [FromBody] UpdateProgressRequest request)
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                var goal = await _context.Goals.FindAsync(id);
                if (goal == null)
                    return NotFound(new { message = "Goal not found" });

                if (goal.EmployeeId != employeeId)
                    return Forbid();

                goal.Progress = request.Progress;
                goal.Status = request.Progress >= 100 ? "Completed" : "Active";
                goal.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Progress updated successfully", goal.Progress, goal.Status });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating progress", error = ex.Message });
            }
        }

        // DELETE: Delete Goal
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGoal(int id)
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                var goal = await _context.Goals.FindAsync(id);
                if (goal == null)
                    return NotFound(new { message = "Goal not found" });

                if (goal.EmployeeId != employeeId)
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
    }

    public class CreateGoalRequest
    {
        public int? EmployeeId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime DueDate { get; set; }
    }

    public class UpdateProgressRequest
    {
        public int Progress { get; set; }
    }
}