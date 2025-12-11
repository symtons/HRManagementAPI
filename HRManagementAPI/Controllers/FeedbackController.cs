using HRManagementAPI.Data;
using HRManagementAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System;
using System.Security.Claims;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static QuestPDF.Helpers.Colors;

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

        // GET: Received Feedback
        [HttpGet("Received")]
        public async Task<IActionResult> GetReceivedFeedback()
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                var feedback = await _context.Feedback
                    .Include(f => f.FromEmployee)
                    .Where(f => f.ToEmployeeId == employeeId)
                    .OrderByDescending(f => f.CreatedAt)
                    .Select(f => new
                    {
                        f.FeedbackId,
                        FromName = f.IsAnonymous ? "Anonymous" : f.FromEmployee.FirstName + " " + f.FromEmployee.LastName,
                        f.FeedbackType,
                        f.Content,
                        f.IsRead,
                        f.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new { feedback });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving feedback", error = ex.Message });
            }
        }

        // GET: Given Feedback
        [HttpGet("Given")]
        public async Task<IActionResult> GetGivenFeedback()
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                var feedback = await _context.Feedback
                    .Include(f => f.ToEmployee)
                    .Where(f => f.FromEmployeeId == employeeId)
                    .OrderByDescending(f => f.CreatedAt)
                    .Select(f => new
                    {
                        f.FeedbackId,
                        ToName = f.ToEmployee.FirstName + " " + f.ToEmployee.LastName,
                        f.FeedbackType,
                        f.Content,
                        f.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new { feedback });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving feedback", error = ex.Message });
            }
        }

        // POST: Give Feedback
        [HttpPost]
        public async Task<IActionResult> GiveFeedback([FromBody] GiveFeedbackRequest request)
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                if (request.ToEmployeeId == employeeId)
                    return BadRequest(new { message = "Cannot give feedback to yourself" });

                var feedback = new Feedback
                {
                    FromEmployeeId = employeeId,
                    ToEmployeeId = request.ToEmployeeId,
                    FeedbackType = request.FeedbackType,
                    Content = request.Content,
                    IsAnonymous = request.IsAnonymous
                };

                _context.Feedback.Add(feedback);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Feedback submitted successfully", feedbackId = feedback.FeedbackId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error submitting feedback", error = ex.Message });
            }
        }

        // PUT: Mark as Read
        [HttpPut("{id}/MarkAsRead")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                var feedback = await _context.Feedback.FindAsync(id);
                if (feedback == null)
                    return NotFound(new { message = "Feedback not found" });

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
    }

    public class GiveFeedbackRequest
    {
        public int ToEmployeeId { get; set; }
        public string FeedbackType { get; set; }
        public string Content { get; set; }
        public bool IsAnonymous { get; set; }
    }
}
