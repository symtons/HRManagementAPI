using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRManagementAPI.Models
{
    // =============================================
    // Review Cycles
    // =============================================
    [Table("ReviewCycles")]
    public class ReviewCycle
    {
        [Key]
        public int ReviewCycleId { get; set; }

        [Required]
        [StringLength(100)]
        public string CycleName { get; set; }

        [Required]
        [StringLength(50)]
        public string ReviewType { get; set; } = "Annual"; // Annual, Quarterly, Mid-Year

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        public bool IsActive { get; set; } = true;

        [StringLength(500)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<PerformanceReview>? PerformanceReviews { get; set; }
    }

    // =============================================
    // Performance Reviews
    // =============================================
    [Table("PerformanceReviews")]
    public class PerformanceReview
    {
        [Key]
        public int ReviewId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        public int? ReviewerId { get; set; }

        [Required]
        public int ReviewCycleId { get; set; }

        public string? SelfAssessment { get; set; }
        public string? ManagerAssessment { get; set; }

        [Column(TypeName = "decimal(3,2)")]
        public decimal? OverallRating { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Draft"; // Draft, Submitted, UnderReview, Completed

        public DateTime? ReviewDate { get; set; }
        public DateTime? DueDate { get; set; }

        public string? Strengths { get; set; }
        public string? AreasForImprovement { get; set; }
        public string? Goals { get; set; }
        public string? Comments { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("EmployeeId")]
        public virtual Employee? Employee { get; set; }

        [ForeignKey("ReviewerId")]
        public virtual Employee? Reviewer { get; set; }

        [ForeignKey("ReviewCycleId")]
        public virtual ReviewCycle? ReviewCycle { get; set; }

        public virtual ICollection<ReviewRating>? ReviewRatings { get; set; }
    }

    // =============================================
    // Review Ratings (Competency-based)
    // =============================================
    [Table("ReviewRatings")]
    public class ReviewRating
    {
        [Key]
        public int RatingId { get; set; }

        [Required]
        public int ReviewId { get; set; }

        [Required]
        [StringLength(100)]
        public string CompetencyName { get; set; }

        public int? SelfRating { get; set; } // 1-5
        public int? ManagerRating { get; set; } // 1-5

        [StringLength(500)]
        public string? Comments { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("ReviewId")]
        public virtual PerformanceReview? PerformanceReview { get; set; }
    }

    // =============================================
    // Goals
    // =============================================
    [Table("Goals")]
    public class Goal
    {
        [Key]
        public int GoalId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        public string? Description { get; set; }

        [StringLength(50)]
        public string? Category { get; set; } // Professional Development, Performance, Project, Custom

        [StringLength(20)]
        public string Priority { get; set; } = "Medium"; // Low, Medium, High

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        public int Progress { get; set; } = 0; // 0-100%

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "NotStarted"; // NotStarted, InProgress, Completed, Cancelled

        [Required]
        public int CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("EmployeeId")]
        public virtual Employee? Employee { get; set; }

        [ForeignKey("CreatedBy")]
        public virtual Employee? Creator { get; set; }

        public virtual ICollection<GoalUpdate>? GoalUpdates { get; set; }
    }

    // =============================================
    // Goal Updates
    // =============================================
    [Table("GoalUpdates")]
    public class GoalUpdate
    {
        [Key]
        public int UpdateId { get; set; }

        [Required]
        public int GoalId { get; set; }

        [Required]
        public string UpdateText { get; set; }

        [Required]
        public int Progress { get; set; } // 0-100%

        [Required]
        public int UpdatedBy { get; set; }

        public DateTime UpdateDate { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("GoalId")]
        public virtual Goal? Goal { get; set; }

        [ForeignKey("UpdatedBy")]
        public virtual Employee? Updater { get; set; }
    }

    // =============================================
    // Feedback
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
        public string FeedbackType { get; set; } // Positive, Constructive, General

        [Required]
        public string Content { get; set; }

        public bool IsAnonymous { get; set; } = false;
        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("FromEmployeeId")]
        public virtual Employee? FromEmployee { get; set; }

        [ForeignKey("ToEmployeeId")]
        public virtual Employee? ToEmployee { get; set; }
    }
}
