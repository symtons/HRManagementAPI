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
    public class FeedbackController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public FeedbackController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =============================================
        // GET: Received Feedback
        // =============================================
        [HttpGet("Received")]
        public async Task<IActionResult> GetReceivedFeedback([FromQuery] string? type, [FromQuery] bool? unreadOnly)
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                var query = _context.Feedback
                    .Include(f => f.FromEmployee)
                    .Where(f => f.ToEmployeeId == employeeId)
                    .AsQueryable();

                // Filter by type
                if (!string.IsNullOrEmpty(type))
                {
                    query = query.Where(f => f.FeedbackType == type);
                }

                // Filter unread only
                if (unreadOnly.HasValue && unreadOnly.Value)
                {
                    query = query.Where(f => !f.IsRead);
                }

                var feedback = await query
                    .OrderByDescending(f => f.CreatedAt)
                    .Select(f => new
                    {
                        f.FeedbackId,
                        f.FromEmployeeId,
                        FromEmployeeName = f.IsAnonymous ? "Anonymous" : f.FromEmployee.FirstName + " " + f.FromEmployee.LastName,
                        f.FeedbackType,
                        f.Content,
                        f.IsAnonymous,
                        f.IsRead,
                        f.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new { feedback });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving received feedback", error = ex.Message });
            }
        }

        // =============================================
        // GET: Given Feedback
        // =============================================
        [HttpGet("Given")]
        public async Task<IActionResult> GetGivenFeedback([FromQuery] string? type)
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                var query = _context.Feedback
                    .Include(f => f.ToEmployee)
                    .Where(f => f.FromEmployeeId == employeeId)
                    .AsQueryable();

                // Filter by type
                if (!string.IsNullOrEmpty(type))
                {
                    query = query.Where(f => f.FeedbackType == type);
                }

                var feedback = await query
                    .OrderByDescending(f => f.CreatedAt)
                    .Select(f => new
                    {
                        f.FeedbackId,
                        f.ToEmployeeId,
                        ToEmployeeName = f.ToEmployee.FirstName + " " + f.ToEmployee.LastName,
                        f.FeedbackType,
                        f.Content,
                        f.IsAnonymous,
                        f.IsRead,
                        f.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new { feedback });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving given feedback", error = ex.Message });
            }
        }

        // =============================================
        // GET: Feedback by ID
        // =============================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetFeedback(int id)
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");
                var role = User.FindFirst(ClaimTypes.Role)?.Value;

                var feedback = await _context.Feedback
                    .Include(f => f.FromEmployee)
                    .Include(f => f.ToEmployee)
                    .FirstOrDefaultAsync(f => f.FeedbackId == id);

                if (feedback == null)
                    return NotFound(new { message = "Feedback not found" });

                // Check permissions - only sender, recipient, or admin can view
                bool canView = feedback.FromEmployeeId == employeeId ||
                              feedback.ToEmployeeId == employeeId ||
                              role == "Admin" || role == "Executive" || role == "Director";

                if (!canView)
                    return Forbid();

                var result = new
                {
                    feedback.FeedbackId,
                    feedback.FromEmployeeId,
                    FromEmployeeName = feedback.IsAnonymous && feedback.ToEmployeeId == employeeId
                        ? "Anonymous"
                        : feedback.FromEmployee.FirstName + " " + feedback.FromEmployee.LastName,
                    feedback.ToEmployeeId,
                    ToEmployeeName = feedback.ToEmployee.FirstName + " " + feedback.ToEmployee.LastName,
                    feedback.FeedbackType,
                    feedback.Content,
                    feedback.IsAnonymous,
                    feedback.IsRead,
                    feedback.CreatedAt
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving feedback", error = ex.Message });
            }
        }

        // =============================================
        // POST: Create Feedback
        // =============================================
        [HttpPost]
        public async Task<IActionResult> CreateFeedback([FromBody] CreateFeedbackRequest request)
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                // Validate recipient exists
                var recipientExists = await _context.Employees.AnyAsync(e => e.EmployeeId == request.ToEmployeeId);
                if (!recipientExists)
                    return BadRequest(new { message = "Recipient employee not found" });

                // Prevent self-feedback
                if (request.ToEmployeeId == employeeId)
                    return BadRequest(new { message = "Cannot send feedback to yourself" });

                var feedback = new Feedback
                {
                    FromEmployeeId = employeeId,
                    ToEmployeeId = request.ToEmployeeId,
                    FeedbackType = request.FeedbackType,
                    Content = request.Content,
                    IsAnonymous = request.IsAnonymous ?? false,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Feedback.Add(feedback);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Feedback submitted successfully", feedbackId = feedback.FeedbackId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error creating feedback", error = ex.Message });
            }
        }

        // =============================================
        // PUT: Mark Feedback as Read
        // =============================================
        [HttpPut("{id}/MarkAsRead")]
        public async Task<IActionResult> MarkFeedbackAsRead(int id)
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                var feedback = await _context.Feedback
                    .FirstOrDefaultAsync(f => f.FeedbackId == id);

                if (feedback == null)
                    return NotFound(new { message = "Feedback not found" });

                // Only the recipient can mark as read
                if (feedback.ToEmployeeId != employeeId)
                    return Forbid();

                feedback.IsRead = true;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Feedback marked as read" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error marking feedback as read", error = ex.Message });
            }
        }

        // =============================================
        // DELETE: Delete Feedback
        // =============================================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFeedback(int id)
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                var feedback = await _context.Feedback
                    .FirstOrDefaultAsync(f => f.FeedbackId == id);

                if (feedback == null)
                    return NotFound(new { message = "Feedback not found" });

                // Only the sender can delete their feedback
                bool canDelete = feedback.FromEmployeeId == employeeId;

                if (!canDelete)
                    return Forbid();

                _context.Feedback.Remove(feedback);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Feedback deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error deleting feedback", error = ex.Message });
            }
        }

        // =============================================
        // GET: Feedback Statistics
        // =============================================
        [HttpGet("Statistics")]
        public async Task<IActionResult> GetFeedbackStatistics()
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                var receivedFeedback = await _context.Feedback
                    .Where(f => f.ToEmployeeId == employeeId)
                    .ToListAsync();

                var givenFeedback = await _context.Feedback
                    .Where(f => f.FromEmployeeId == employeeId)
                    .ToListAsync();

                var stats = new
                {
                    received = new
                    {
                        total = receivedFeedback.Count,
                        unread = receivedFeedback.Count(f => !f.IsRead),
                        positive = receivedFeedback.Count(f => f.FeedbackType == "Positive"),
                        constructive = receivedFeedback.Count(f => f.FeedbackType == "Constructive"),
                        general = receivedFeedback.Count(f => f.FeedbackType == "General")
                    },
                    given = new
                    {
                        total = givenFeedback.Count,
                        positive = givenFeedback.Count(f => f.FeedbackType == "Positive"),
                        constructive = givenFeedback.Count(f => f.FeedbackType == "Constructive"),
                        general = givenFeedback.Count(f => f.FeedbackType == "General")
                    }
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving feedback statistics", error = ex.Message });
            }
        }

        // =============================================
        // GET: Team Feedback (for managers)
        // =============================================
        [HttpGet("TeamFeedback")]
        [Authorize(Roles = "Admin,Executive,Director,ProgramCoordinator,FieldOperatorManager")]
        public async Task<IActionResult> GetTeamFeedback([FromQuery] string? type)
        {
            try
            {
                var managerId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");
                var role = User.FindFirst(ClaimTypes.Role)?.Value;

                var query = _context.Feedback
                    .Include(f => f.FromEmployee)
                    .Include(f => f.ToEmployee)
                    .AsQueryable();

                // Filter by supervisor (unless Admin)
                if (role != "Admin")
                {
                    query = query.Where(f => f.ToEmployee.ManagerId == managerId || f.FromEmployee.ManagerId == managerId);
                }

                // Filter by type
                if (!string.IsNullOrEmpty(type))
                {
                    query = query.Where(f => f.FeedbackType == type);
                }

                var feedback = await query
                    .OrderByDescending(f => f.CreatedAt)
                    .Select(f => new
                    {
                        f.FeedbackId,
                        f.FromEmployeeId,
                        FromEmployeeName = f.IsAnonymous ? "Anonymous" : f.FromEmployee.FirstName + " " + f.FromEmployee.LastName,
                        f.ToEmployeeId,
                        ToEmployeeName = f.ToEmployee.FirstName + " " + f.ToEmployee.LastName,
                        FromDepartment = f.FromEmployee.Department != null ? f.FromEmployee.Department.DepartmentName : null,
                        ToDepartment = f.ToEmployee.Department != null ? f.ToEmployee.Department.DepartmentName : null,
                        f.FeedbackType,
                        f.Content,
                        f.IsAnonymous,
                        f.IsRead,
                        f.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new { feedback });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving team feedback", error = ex.Message });
            }
        }
    }

    // =============================================
    // Request Models
    // =============================================
    public class CreateFeedbackRequest
    {
        public int ToEmployeeId { get; set; }
        public string FeedbackType { get; set; }
        public string Content { get; set; }
        public bool? IsAnonymous { get; set; }
    }
}
