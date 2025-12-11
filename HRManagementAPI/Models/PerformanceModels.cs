using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRManagementAPI.Models
{
    // =============================================
    // REVIEW PERIOD
    // =============================================
    [Table("ReviewPeriods")]
    public class ReviewPeriod
    {
        [Key]
        public int PeriodId { get; set; }

        [Required]
        [StringLength(200)]
        public string PeriodName { get; set; }

        [Required]
        [StringLength(50)]
        public string PeriodType { get; set; } // 'Monthly', 'Quarterly', 'Annual'

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        public DateTime RatingDeadline { get; set; }

        [StringLength(50)]
        public string Status { get; set; } = "Active"; // 'Active', 'Closed'

        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // =============================================
    // EMPLOYEE REVIEW
    // =============================================
    [Table("EmployeeReviews")]
    public class EmployeeReview
    {
        [Key]
        public int EmployeeReviewId { get; set; }

        [Required]
        public int PeriodId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        public int TotalRaters { get; set; }
        public int CompletedRatings { get; set; } = 0;

        [Column(TypeName = "decimal(5,2)")]
        public decimal? FinalScore { get; set; }

        [StringLength(50)]
        public string Status { get; set; } = "Open"; // 'Open', 'InProgress', 'Completed'

        public int? CompanyWideRank { get; set; }
        public int? DepartmentRank { get; set; }
        public int? RoleRank { get; set; }

        public DateTime? CompletedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("EmployeeId")]
        public virtual Employee Employee { get; set; }

        [ForeignKey("PeriodId")]
        public virtual ReviewPeriod Period { get; set; }
    }

    // =============================================
    // RATER ASSIGNMENT
    // =============================================
    [Table("RaterAssignments")]
    public class RaterAssignment
    {
        [Key]
        public int AssignmentId { get; set; }

        [Required]
        public int EmployeeReviewId { get; set; }

        [Required]
        public int RaterId { get; set; }

        [Required]
        [StringLength(100)]
        public string RaterRole { get; set; } // 'Direct Manager', 'Director', 'Executive', 'Admin'

        public bool IsCompleted { get; set; } = false;
        public DateTime? CompletedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("RaterId")]
        public virtual Employee Rater { get; set; }
    }

    // =============================================
    // RATING
    // =============================================
    [Table("Ratings")]
    public class Rating
    {
        [Key]
        public int RatingId { get; set; }

        [Required]
        public int EmployeeReviewId { get; set; }

        [Required]
        public int RaterId { get; set; }

        [Required]
        [StringLength(100)]
        public string RaterRole { get; set; }

        // Ratings (0-100)
        [Required]
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
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("RaterId")]
        public virtual Employee Rater { get; set; }
    }

    // =============================================
    // GOAL
    // =============================================
    [Table("Goals")]
    public class Goal
    {
        [Key]
        public int GoalId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public int CreatedBy { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        public string Description { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        public int Progress { get; set; } = 0; // 0-100

        [StringLength(50)]
        public string Status { get; set; } = "Active"; // 'Active', 'Completed', 'Cancelled'

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("EmployeeId")]
        public virtual Employee Employee { get; set; }

        [ForeignKey("CreatedBy")]
        public virtual Employee Creator { get; set; }
    }

    // =============================================
    // FEEDBACK
    // =============================================
    [Table("Feedback")]
    public class Feedback
    {
        [Key]
        public int FeedbackId { get; set; }

        [Required]
        public int FromEmployeeId { get; set; }

        [Required]
        public int ToEmployeeId { get; set; }

        [Required]
        [StringLength(50)]
        public string FeedbackType { get; set; } // 'Positive', 'Constructive', 'General'

        [Required]
        public string Content { get; set; }

        public bool IsAnonymous { get; set; } = false;
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("FromEmployeeId")]
        public virtual Employee FromEmployee { get; set; }

        [ForeignKey("ToEmployeeId")]
        public virtual Employee ToEmployee { get; set; }
    }
}