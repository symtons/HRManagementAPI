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
        // CREATE REVIEW PERIOD (Executive Only)
        // =============================================
        [HttpPost("CreatePeriod")]
        [Authorize(Roles = "Admin,Executive")]
        public async Task<IActionResult> CreateReviewPeriod([FromBody] CreatePeriodRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                // Create period
                var period = new ReviewPeriod
                {
                    PeriodName = request.PeriodName,
                    PeriodType = request.PeriodType,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    RatingDeadline = request.RatingDeadline,
                    Status = "Active",
                    CreatedBy = employeeId
                };

                _context.ReviewPeriods.Add(period);
                await _context.SaveChangesAsync();

                // Get all active employees
                var employees = await _context.Employees
                    .Where(e => e.EmploymentStatus == "Active")
                    .ToListAsync();

                int totalAssignments = 0;

                // Create employee reviews and rater assignments
                foreach (var employee in employees)
                {
                    // Get employee's reporting chain
                    var raters = await GetEmployeeRaters(employee.EmployeeId);

                    if (raters.Count == 0) continue; // Skip if no raters found

                    // Create employee review
                    var employeeReview = new EmployeeReview
                    {
                        PeriodId = period.PeriodId,
                        EmployeeId = employee.EmployeeId,
                        TotalRaters = raters.Count,
                        Status = "Open"
                    };

                    _context.EmployeeReviews.Add(employeeReview);
                    await _context.SaveChangesAsync();

                    // Create rater assignments
                    foreach (var rater in raters)
                    {
                        var assignment = new RaterAssignment
                        {
                            EmployeeReviewId = employeeReview.EmployeeReviewId,
                            RaterId = rater.EmployeeId,
                            RaterRole = rater.Role
                        };

                        _context.RaterAssignments.Add(assignment);
                        totalAssignments++;
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Review period created successfully",
                    periodId = period.PeriodId,
                    employeesIncluded = employees.Count,
                    totalAssignments = totalAssignments
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error creating review period", error = ex.Message });
            }
        }

        // =============================================
        // GET MY RATINGS TO COMPLETE
        // =============================================
        [HttpGet("MyRatings")]
        public async Task<IActionResult> GetMyRatings([FromQuery] int? periodId)
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                var query = _context.RaterAssignments
                    .Include(ra => ra.Rater)
                    .Where(ra => ra.RaterId == employeeId);

                if (periodId.HasValue)
                {
                    query = query.Where(ra => _context.EmployeeReviews
                        .Any(er => er.EmployeeReviewId == ra.EmployeeReviewId && er.PeriodId == periodId.Value));
                }

                var assignments = await query
                    .Select(ra => new
                    {
                        ra.AssignmentId,
                        ra.EmployeeReviewId,
                        ra.RaterRole,
                        ra.IsCompleted,
                        ra.CompletedAt,
                        Employee = _context.EmployeeReviews
                            .Where(er => er.EmployeeReviewId == ra.EmployeeReviewId)
                            .Select(er => new
                            {
                                er.Employee.EmployeeId,
                                EmployeeName = er.Employee.FirstName + " " + er.Employee.LastName,
                                er.Employee.JobTitle,
                                er.Employee.Department.DepartmentName,
                                PeriodName = er.Period.PeriodName,
                                er.Period.RatingDeadline
                            })
                            .FirstOrDefault()
                    })
                    .OrderBy(a => a.IsCompleted)
                    .ThenBy(a => a.Employee.EmployeeName)
                    .ToListAsync();

                var completed = assignments.Count(a => a.IsCompleted);
                var pending = assignments.Count(a => !a.IsCompleted);

                return Ok(new
                {
                    total = assignments.Count,
                    completed = completed,
                    pending = pending,
                    assignments = assignments
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving ratings", error = ex.Message });
            }
        }

        // =============================================
        // SUBMIT RATING
        // =============================================
        [HttpPost("SubmitRating")]
        public async Task<IActionResult> SubmitRating([FromBody] SubmitRatingRequest request)
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                // Verify rater is assigned
                var assignment = await _context.RaterAssignments
                    .FirstOrDefaultAsync(ra => ra.EmployeeReviewId == request.EmployeeReviewId && ra.RaterId == employeeId);

                if (assignment == null)
                    return BadRequest(new { message = "You are not assigned to rate this employee" });

                if (assignment.IsCompleted)
                    return BadRequest(new { message = "You have already rated this employee" });

                // Create rating
                var rating = new Rating
                {
                    EmployeeReviewId = request.EmployeeReviewId,
                    RaterId = employeeId,
                    RaterRole = assignment.RaterRole,
                    OverallRating = request.OverallRating,
                    QualityOfWork = request.QualityOfWork,
                    Punctuality = request.Punctuality,
                    Teamwork = request.Teamwork,
                    Initiative = request.Initiative,
                    Reliability = request.Reliability,
                    Communication = request.Communication,
                    ProblemSolving = request.ProblemSolving,
                    Leadership = request.Leadership,
                    TeamManagement = request.TeamManagement,
                    Comments = request.Comments
                };

                _context.Ratings.Add(rating);

                // Mark assignment complete
                assignment.IsCompleted = true;
                assignment.CompletedAt = DateTime.UtcNow;

                // Update employee review
                var employeeReview = await _context.EmployeeReviews
                    .FindAsync(request.EmployeeReviewId);

                employeeReview.CompletedRatings++;
                employeeReview.Status = "InProgress";

                await _context.SaveChangesAsync();

                // Check if all ratings complete
                if (employeeReview.CompletedRatings == employeeReview.TotalRaters)
                {
                    await CalculateFinalScore(employeeReview.EmployeeReviewId);
                }

                return Ok(new
                {
                    message = "Rating submitted successfully",
                    ratingId = rating.RatingId,
                    progress = $"{employeeReview.CompletedRatings}/{employeeReview.TotalRaters}"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error submitting rating", error = ex.Message });
            }
        }

        // =============================================
        // GET MY PERFORMANCE REVIEWS
        // =============================================
        [HttpGet("MyReviews")]
        public async Task<IActionResult> GetMyReviews()
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");

                var reviews = await _context.EmployeeReviews
                    .Include(er => er.Period)
                    .Where(er => er.EmployeeId == employeeId)
                    .OrderByDescending(er => er.CreatedAt)
                    .Select(er => new
                    {
                        er.EmployeeReviewId,
                        er.Period.PeriodName,
                        er.Period.PeriodType,
                        er.Period.StartDate,
                        er.Period.EndDate,
                        er.TotalRaters,
                        er.CompletedRatings,
                        er.FinalScore,
                        er.Status,
                        er.CompanyWideRank,
                        er.DepartmentRank,
                        er.RoleRank,
                        er.CompletedAt
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
        // GET REVIEW DETAILS
        // =============================================
        [HttpGet("ReviewDetails/{employeeReviewId}")]
        public async Task<IActionResult> GetReviewDetails(int employeeReviewId)
        {
            try
            {
                var employeeId = int.Parse(User.FindFirst("EmployeeId")?.Value ?? "0");
                var role = User.FindFirst(ClaimTypes.Role)?.Value;

                var review = await _context.EmployeeReviews
                    .Include(er => er.Employee)
                    .Include(er => er.Period)
                    .FirstOrDefaultAsync(er => er.EmployeeReviewId == employeeReviewId);

                if (review == null)
                    return NotFound(new { message = "Review not found" });

                // Check access
                var canView = review.EmployeeId == employeeId ||
                              role == "Admin" ||
                              role == "Executive" ||
                              await _context.RaterAssignments.AnyAsync(ra =>
                                  ra.EmployeeReviewId == employeeReviewId && ra.RaterId == employeeId);

                if (!canView)
                    return Forbid();

                // Get ratings
                var ratings = await _context.Ratings
                    .Include(r => r.Rater)
                    .Where(r => r.EmployeeReviewId == employeeReviewId)
                    .Select(r => new
                    {
                        r.RatingId,
                        RaterName = review.EmployeeId == employeeId ? r.Rater.FirstName + " " + r.Rater.LastName : null,
                        r.RaterRole,
                        r.OverallRating,
                        r.QualityOfWork,
                        r.Punctuality,
                        r.Teamwork,
                        r.Initiative,
                        r.Reliability,
                        r.Communication,
                        r.ProblemSolving,
                        r.Leadership,
                        r.TeamManagement,
                        r.Comments,
                        r.SubmittedAt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    review = new
                    {
                        review.EmployeeReviewId,
                        EmployeeName = review.Employee.FirstName + " " + review.Employee.LastName,
                        review.Employee.JobTitle,
                        review.Period.PeriodName,
                        review.Period.StartDate,
                        review.Period.EndDate,
                        review.TotalRaters,
                        review.CompletedRatings,
                        review.FinalScore,
                        review.Status,
                        review.CompanyWideRank,
                        review.DepartmentRank,
                        review.RoleRank,
                        review.CompletedAt
                    },
                    ratings = ratings
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving review details", error = ex.Message });
            }
        }

        // =============================================
        // GET RANKINGS
        // =============================================
        [HttpGet("Rankings/{periodId}")]
        [Authorize(Roles = "Admin,Executive,Director")]
        public async Task<IActionResult> GetRankings(int periodId, [FromQuery] string filter = "all")
        {
            try
            {
                var query = _context.EmployeeReviews
                    .Include(er => er.Employee)
                    .ThenInclude(e => e.Department)
                    .Where(er => er.PeriodId == periodId && er.Status == "Completed");

                var rankings = await query
                    .OrderBy(er => er.CompanyWideRank)
                    .Select(er => new
                    {
                        er.EmployeeReviewId,
                        er.Employee.EmployeeId,
                        EmployeeName = er.Employee.FirstName + " " + er.Employee.LastName,
                        er.Employee.JobTitle,
                        DepartmentName = er.Employee.Department.DepartmentName,
                        er.FinalScore,
                        er.CompanyWideRank,
                        er.DepartmentRank,
                        er.RoleRank,
                        er.CompletedAt
                    })
                    .ToListAsync();

                return Ok(new { rankings });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving rankings", error = ex.Message });
            }
        }

        // =============================================
        // HELPER: Get Employee Raters
        // =============================================
        private async Task<List<(int EmployeeId, string Role)>> GetEmployeeRaters(int employeeId)
        {
            var raters = new List<(int EmployeeId, string Role)>();

            var employee = await _context.Employees
                .Include(e => e.Manager)
                .Include(e => e.User)
                .ThenInclude(u => u.Role)
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

            if (employee == null) return raters;

            // Direct Manager
            if (employee.ManagerId.HasValue)
            {
                raters.Add((employee.ManagerId.Value, "Direct Manager"));

                // Get manager's manager (skip one level up in hierarchy)
                var manager = await _context.Employees
                    .Include(e => e.Manager)
                    .FirstOrDefaultAsync(e => e.EmployeeId == employee.ManagerId.Value);

                if (manager?.ManagerId != null)
                {
                    raters.Add((manager.ManagerId.Value, "Upper Management"));
                }
            }

            // Get all Executives
            var executives = await _context.Users
                .Include(u => u.Employee)
                .Where(u => u.Role.RoleName == "Executive" && u.Employee != null)
                .Select(u => u.Employee.EmployeeId)
                .ToListAsync();

            foreach (var execId in executives)
            {
                if (!raters.Any(r => r.EmployeeId == execId))
                    raters.Add((execId, "Executive"));
            }

            // Get Admin
            var admin = await _context.Users
                .Include(u => u.Employee)
                .Where(u => u.Role.RoleName == "Admin" && u.Employee != null)
                .Select(u => u.Employee.EmployeeId)
                .FirstOrDefaultAsync();

            if (admin > 0 && !raters.Any(r => r.EmployeeId == admin))
                raters.Add((admin, "Admin"));

            return raters;
        }

        // =============================================
        // HELPER: Calculate Final Score
        // =============================================
        private async Task CalculateFinalScore(int employeeReviewId)
        {
            var ratings = await _context.Ratings
                .Where(r => r.EmployeeReviewId == employeeReviewId)
                .ToListAsync();

            if (ratings.Count == 0) return;

            var avgScore = ratings.Average(r => r.OverallRating);

            var review = await _context.EmployeeReviews.FindAsync(employeeReviewId);
            review.FinalScore = Math.Round((decimal)avgScore, 2);
            review.Status = "Completed";
            review.CompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Calculate rankings for this period
            await CalculateRankings(review.PeriodId);
        }

        // =============================================
        // HELPER: Calculate Rankings
        // =============================================
        private async Task CalculateRankings(int periodId)
        {
            var completedReviews = await _context.EmployeeReviews
                .Include(er => er.Employee)
                .Where(er => er.PeriodId == periodId && er.Status == "Completed")
                .OrderByDescending(er => er.FinalScore)
                .ToListAsync();

            // Company-wide rank
            for (int i = 0; i < completedReviews.Count; i++)
            {
                completedReviews[i].CompanyWideRank = i + 1;
            }

            // Department rank
            var byDept = completedReviews
                .GroupBy(er => er.Employee.DepartmentId)
                .ToList();

            foreach (var group in byDept)
            {
                var sorted = group.OrderByDescending(er => er.FinalScore).ToList();
                for (int i = 0; i < sorted.Count; i++)
                {
                    sorted[i].DepartmentRank = i + 1;
                }
            }

            // Role rank
            var byRole = completedReviews
                .GroupBy(er => er.Employee.JobTitle)
                .ToList();

            foreach (var group in byRole)
            {
                var sorted = group.OrderByDescending(er => er.FinalScore).ToList();
                for (int i = 0; i < sorted.Count; i++)
                {
                    sorted[i].RoleRank = i + 1;
                }
            }

            await _context.SaveChangesAsync();
        }
    }

    // =============================================
    // REQUEST MODELS
    // =============================================
    public class CreatePeriodRequest
    {
        public string PeriodName { get; set; }
        public string PeriodType { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime RatingDeadline { get; set; }
    }

    public class SubmitRatingRequest
    {
        public int EmployeeReviewId { get; set; }
        public int OverallRating { get; set; }
        public int? QualityOfWork { get; set; }
        public int? Punctuality { get; set; }
        public int? Teamwork { get; set; }
        public int? Initiative { get; set; }
        public int? Reliability { get; set; }
        public int? Communication { get; set; }
        public int? ProblemSolving { get; set; }
        public int? Leadership { get; set; }
        public int? TeamManagement { get; set; }
        public string Comments { get; set; }
    }
}