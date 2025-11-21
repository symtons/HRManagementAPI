using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRManagementAPI.Models
{

    // ===== OnboardingTask Model (Master Template) =====
    [Table("OnboardingTasks")]
    public class OnboardingTask
    {
        [Key]
        public int TaskId { get; set; }

        [Required]
        [MaxLength(200)]
        public string TaskName { get; set; }

        public string? TaskDescription { get; set; }

        [MaxLength(100)]
        public string? TaskCategory { get; set; } // Document/Form/Policy/Training/Information

        [MaxLength(50)]
        public string? TaskType { get; set; } // Upload/Acknowledgment/Input/ESign

        public bool IsRequired { get; set; } = true;
        public int DefaultDueDays { get; set; } = 7;

        [MaxLength(20)]
        public string ApplicableEmployeeType { get; set; } = "Both"; // AdminStaff/FieldStaff/Both

        [MaxLength(200)]
        public string? RequiredFileTypes { get; set; } // e.g., "pdf,jpg,png"

        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;

        public string? InstructionText { get; set; } // Instructions shown to employee

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Property
        public virtual ICollection<EmployeeOnboardingTask> EmployeeOnboardingTasks { get; set; }
    }

    // ===== EmployeeOnboardingTask Model (Assigned Tasks) =====
    [Table("EmployeeOnboardingTasks")]
    public class EmployeeOnboardingTask
    {
        [Key]
        public int OnboardingTaskId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public int TaskId { get; set; }

        // Task Tracking
        public DateTime AssignedDate { get; set; } = DateTime.UtcNow;
        public DateTime DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Pending"; // Pending/InProgress/Completed/Overdue

        // Task Response
        public string? SubmittedData { get; set; } // JSON or text data

        [MaxLength(500)]
        public string? DocumentPath { get; set; }

        [MaxLength(200)]
        public string? DocumentOriginalName { get; set; }

        public string? Notes { get; set; }

        // Approval/Verification
        public int? CompletedBy { get; set; }
        public int? VerifiedBy { get; set; }
        public string? VerificationNotes { get; set; }
        public bool IsVerified { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("EmployeeId")]
        public virtual Employee Employee { get; set; }

        [ForeignKey("TaskId")]
        public virtual OnboardingTask Task { get; set; }

        [ForeignKey("CompletedBy")]
        public virtual User? CompletedByUser { get; set; }

        [ForeignKey("VerifiedBy")]
        public virtual User? VerifiedByUser { get; set; }
    }
}