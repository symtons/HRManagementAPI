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
    public class PerformanceReviewController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PerformanceReviewController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =============================================
        // GET: My Reviews
        // =============================================
        [HttpGet("MyReviews")]
        public async Task<IActionResult> GetMyReviews()
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                var reviews = await _context.PerformanceReviews
                    .Include(r => r.ReviewCycle)
                    .Include(r => r.Reviewer)
                    .Include(r => r.ReviewRatings)
                    .Where(r => r.EmployeeId == employeeId)
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => new
                    {
                        r.ReviewId,
                        r.EmployeeId,
                        ReviewerId = r.ReviewerId,
                        ReviewerName = r.Reviewer != null ? r.Reviewer.FirstName + " " + r.Reviewer.LastName : null,
                        r.ReviewCycleId,
                        CycleName = r.ReviewCycle.CycleName,
                        ReviewType = r.ReviewCycle.ReviewType,
                        r.SelfAssessment,
                        r.ManagerAssessment,
                        r.OverallRating,
                        r.Status,
                        r.ReviewDate,
                        r.DueDate,
                        r.Strengths,
                        r.AreasForImprovement,
                        r.Goals,
                        r.Comments,
                        Ratings = r.ReviewRatings.Select(rating => new
                        {
                            rating.RatingId,
                            rating.CompetencyName,
                            rating.SelfRating,
                            rating.ManagerRating,
                            rating.Comments
                        }).ToList(),
                        r.CreatedAt,
                        r.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(new { reviews });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving reviews", error = ex.Message });
            }
        }

        // =============================================
        // GET: Review by ID
        // =============================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetReview(int id)
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");
                var role = User.FindFirst(ClaimTypes.Role)?.Value;

                var review = await _context.PerformanceReviews
                    .Include(r => r.Employee)
                    .Include(r => r.ReviewCycle)
                    .Include(r => r.Reviewer)
                    .Include(r => r.ReviewRatings)
                    .Where(r => r.ReviewId == id)
                    .FirstOrDefaultAsync();

                if (review == null)
                    return NotFound(new { message = "Review not found" });

                // Check permissions
                bool canView = review.EmployeeId == employeeId ||
                              review.ReviewerId == employeeId ||
                              role == "Admin" || role == "Executive" || role == "Director";

                if (!canView)
                    return Forbid();

                var result = new
                {
                    review.ReviewId,
                    review.EmployeeId,
                    EmployeeName = review.Employee.FirstName + " " + review.Employee.LastName,
                    ReviewerId = review.ReviewerId,
                    ReviewerName = review.Reviewer != null ? review.Reviewer.FirstName + " " + review.Reviewer.LastName : null,
                    review.ReviewCycleId,
                    CycleName = review.ReviewCycle.CycleName,
                    ReviewType = review.ReviewCycle.ReviewType,
                    review.SelfAssessment,
                    review.ManagerAssessment,
                    review.OverallRating,
                    review.Status,
                    review.ReviewDate,
                    review.DueDate,
                    review.Strengths,
                    review.AreasForImprovement,
                    review.Goals,
                    review.Comments,
                    Ratings = review.ReviewRatings.Select(r => new
                    {
                        r.RatingId,
                        r.CompetencyName,
                        r.SelfRating,
                        r.ManagerRating,
                        r.Comments
                    }).ToList(),
                    review.CreatedAt,
                    review.UpdatedAt
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving review", error = ex.Message });
            }
        }

        // =============================================
        // POST: Submit Self-Assessment
        // =============================================
        [HttpPost("SubmitSelfAssessment")]
        public async Task<IActionResult> SubmitSelfAssessment([FromBody] SelfAssessmentRequest request)
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                var review = await _context.PerformanceReviews
                    .Include(r => r.ReviewRatings)
                    .FirstOrDefaultAsync(r => r.ReviewId == request.ReviewId && r.EmployeeId == employeeId);

                if (review == null)
                    return NotFound(new { message = "Review not found" });

                if (review.Status != "Draft" && review.Status != "Submitted")
                    return BadRequest(new { message = "Review cannot be edited in current status" });

                review.SelfAssessment = request.SelfAssessment;
                review.Strengths = request.Strengths;
                review.AreasForImprovement = request.AreasForImprovement;
                review.Goals = request.Goals;
                review.Status = "Submitted";
                review.UpdatedAt = DateTime.UtcNow;

                // Update self ratings
                if (request.Ratings != null)
                {
                    foreach (var ratingData in request.Ratings)
                    {
                        var rating = review.ReviewRatings.FirstOrDefault(r => r.RatingId == ratingData.RatingId);
                        if (rating != null)
                        {
                            rating.SelfRating = ratingData.SelfRating;
                            rating.Comments = ratingData.Comments;
                        }
                        else
                        {
                            review.ReviewRatings.Add(new ReviewRating
                            {
                                ReviewId = review.ReviewId,
                                CompetencyName = ratingData.CompetencyName,
                                SelfRating = ratingData.SelfRating,
                                Comments = ratingData.Comments,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new { message = "Self-assessment submitted successfully", review });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error submitting self-assessment", error = ex.Message });
            }
        }

        // =============================================
        // POST: Submit Manager Assessment
        // =============================================
        [HttpPost("SubmitManagerAssessment")]
        [Authorize(Roles = "Admin,Executive,Director,ProgramCoordinator,FieldOperatorManager")]
        public async Task<IActionResult> SubmitManagerAssessment([FromBody] ManagerAssessmentRequest request)
        {
            try
            {
                var managerId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                var review = await _context.PerformanceReviews
                    .Include(r => r.ReviewRatings)
                    .Include(r => r.Employee)
                    .FirstOrDefaultAsync(r => r.ReviewId == request.ReviewId);

                if (review == null)
                    return NotFound(new { message = "Review not found" });

                // Verify manager can review this employee
                if (review.Employee.ManagerId != managerId && User.FindFirst(ClaimTypes.Role)?.Value != "Admin")
                    return Forbid();

                review.ReviewerId = managerId;
                review.ManagerAssessment = request.ManagerAssessment;
                review.OverallRating = request.OverallRating;
                review.Comments = request.Comments;
                review.Status = "Completed";
                review.ReviewDate = DateTime.UtcNow;
                review.UpdatedAt = DateTime.UtcNow;

                // Update manager ratings
                if (request.Ratings != null)
                {
                    foreach (var ratingData in request.Ratings)
                    {
                        var rating = review.ReviewRatings.FirstOrDefault(r => r.CompetencyName == ratingData.CompetencyName);
                        if (rating != null)
                        {
                            rating.ManagerRating = ratingData.ManagerRating;
                        }
                        else
                        {
                            review.ReviewRatings.Add(new ReviewRating
                            {
                                ReviewId = review.ReviewId,
                                CompetencyName = ratingData.CompetencyName,
                                ManagerRating = ratingData.ManagerRating,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new { message = "Manager assessment submitted successfully", review });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error submitting manager assessment", error = ex.Message });
            }
        }

        // =============================================
        // GET: Team Reviews (for managers)
        // =============================================
        [HttpGet("TeamReviews")]
        [Authorize(Roles = "Admin,Executive,Director,ProgramCoordinator,FieldOperatorManager")]
        public async Task<IActionResult> GetTeamReviews([FromQuery] int? cycleId, [FromQuery] string? status)
        {
            try
            {
                var managerId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");
                var role = User.FindFirst(ClaimTypes.Role)?.Value;

                var query = _context.PerformanceReviews
                    .Include(r => r.Employee)
                    .Include(r => r.ReviewCycle)
                    .Include(r => r.Reviewer)
                    .AsQueryable();

                // Filter by supervisor (unless Admin)
                if (role != "Admin")
                {
                    query = query.Where(r => r.Employee.ManagerId == managerId);
                }

                // Filter by cycle
                if (cycleId.HasValue)
                {
                    query = query.Where(r => r.ReviewCycleId == cycleId.Value);
                }

                // Filter by status
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(r => r.Status == status);
                }

                var reviews = await query
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => new
                    {
                        r.ReviewId,
                        r.EmployeeId,
                        EmployeeName = r.Employee.FirstName + " " + r.Employee.LastName,
                        Department = r.Employee.Department != null ? r.Employee.Department.DepartmentName : null,
                        ReviewerId = r.ReviewerId,
                        ReviewerName = r.Reviewer != null ? r.Reviewer.FirstName + " " + r.Reviewer.LastName : null,
                        CycleName = r.ReviewCycle.CycleName,
                        r.OverallRating,
                        r.Status,
                        r.DueDate,
                        r.ReviewDate,
                        r.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new { reviews });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving team reviews", error = ex.Message });
            }
        }

        // =============================================
        // GET: Active Review Cycles
        // =============================================
        [HttpGet("ActiveCycles")]
        public async Task<IActionResult> GetActiveReviewCycles()
        {
            try
            {
                var cycles = await _context.ReviewCycles
                    .Where(c => c.IsActive)
                    .OrderByDescending(c => c.StartDate)
                    .ToListAsync();

                return Ok(new { cycles });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving review cycles", error = ex.Message });
            }
        }

        // =============================================
        // GET: Performance Summary
        // =============================================
        [HttpGet("Summary")]
        public async Task<IActionResult> GetPerformanceSummary()
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                var reviews = await _context.PerformanceReviews
                    .Where(r => r.EmployeeId == employeeId)
                    .ToListAsync();

                var completedReviews = reviews.Where(r => r.Status == "Completed").ToList();
                var pendingReviews = reviews.Where(r => r.Status != "Completed").ToList();

                var averageRating = completedReviews.Any() && completedReviews.Any(r => r.OverallRating.HasValue)
                    ? completedReviews.Where(r => r.OverallRating.HasValue).Average(r => r.OverallRating!.Value)
                    : 0;

                var summary = new
                {
                    totalReviews = reviews.Count,
                    completedReviews = completedReviews.Count,
                    pendingReviews = pendingReviews.Count,
                    averageRating = Math.Round(averageRating, 2),
                    latestReview = reviews.OrderByDescending(r => r.CreatedAt).FirstOrDefault()
                };

                return Ok(summary);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving performance summary", error = ex.Message });
            }
        }
    }

    // =============================================
    // Request Models
    // =============================================
    public class SelfAssessmentRequest
    {
        public int ReviewId { get; set; }
        public string? SelfAssessment { get; set; }
        public string? Strengths { get; set; }
        public string? AreasForImprovement { get; set; }
        public string? Goals { get; set; }
        public List<RatingData>? Ratings { get; set; }
    }

    public class ManagerAssessmentRequest
    {
        public int ReviewId { get; set; }
        public string? ManagerAssessment { get; set; }
        public decimal? OverallRating { get; set; }
        public string? Comments { get; set; }
        public List<RatingData>? Ratings { get; set; }
    }

    public class RatingData
    {
        public int? RatingId { get; set; }
        public string CompetencyName { get; set; }
        public int? SelfRating { get; set; }
        public int? ManagerRating { get; set; }
        public string? Comments { get; set; }
    }
}
