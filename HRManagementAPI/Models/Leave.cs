using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRManagementAPI.Models
{
    // ============================================
    // LEAVE TYPE MODEL
    // ============================================

    [Table("LeaveTypes")]
    public class LeaveType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LeaveTypeId { get; set; }

        [Required]
        [StringLength(50)]
        public string TypeName { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        public bool IsPaidLeave { get; set; } = true;

        public bool RequiresApproval { get; set; } = true;

        public int? MaxDaysPerYear { get; set; }

        public bool RequiresFullTimeStatus { get; set; } = false;

        [StringLength(7)]
        public string Color { get; set; } = "#5B8FCC";

        public bool IsActive { get; set; } = true;

        public int DisplayOrder { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
    }

    // ============================================
    // LEAVE REQUEST MODEL
    // ============================================

    [Table("LeaveRequests")]
    public class LeaveRequest
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LeaveRequestId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public int LeaveTypeId { get; set; }

        [Required]
        [Column(TypeName = "date")]
        public DateTime StartDate { get; set; }

        [Required]
        [Column(TypeName = "date")]
        public DateTime EndDate { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal TotalDays { get; set; }

        [StringLength(500)]
        public string? Reason { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Cancelled

        // Request tracking
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        // Approval workflow
        public int? ApproverRoleLevel { get; set; } // 2=Executive, 3=Director, NULL=Auto-approved

        public bool RequiresApproval { get; set; } = true;

        // Approval tracking
        public int? ApprovedBy { get; set; } // FK to Users

        public DateTime? ApprovedAt { get; set; }

        [StringLength(500)]
        public string? RejectionReason { get; set; }

        [StringLength(500)]
        public string? ApprovalNotes { get; set; }

        // Metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("EmployeeId")]
        public virtual Employee Employee { get; set; } = null!;

        [ForeignKey("LeaveTypeId")]
        public virtual LeaveType LeaveType { get; set; } = null!;

        [ForeignKey("ApprovedBy")]
        public virtual User? ApprovedByUser { get; set; }

        public virtual ICollection<LeaveCalendar> LeaveCalendarEntries { get; set; } = new List<LeaveCalendar>();
    }

    // ============================================
    // LEAVE BALANCE MODEL
    // ============================================

    [Table("LeaveBalance")]
    public class LeaveBalance
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LeaveBalanceId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public int Year { get; set; }

        // PTO Tracking
        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal TotalPTODays { get; set; } = 20;

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal UsedPTODays { get; set; } = 0;

        // Computed property - calculated in database
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [Column(TypeName = "decimal(5,2)")]
        public decimal? RemainingPTODays { get; set; }

        // Accrual system
        [Column(TypeName = "decimal(5,2)")]
        public decimal? AccrualRate { get; set; }

        [Column(TypeName = "date")]
        public DateTime? LastAccrualDate { get; set; }

        // Metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("EmployeeId")]
        public virtual Employee Employee { get; set; } = null!;
    }

    // ============================================
    // LEAVE CALENDAR MODEL
    // ============================================

    [Table("LeaveCalendar")]
    public class LeaveCalendar
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LeaveCalendarId { get; set; }

        [Required]
        public int LeaveRequestId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        [Column(TypeName = "date")]
        public DateTime LeaveDate { get; set; }

        public bool IsFullDay { get; set; } = true;

        public bool IsFirstHalf { get; set; } = false;

        public bool IsSecondHalf { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("LeaveRequestId")]
        public virtual LeaveRequest LeaveRequest { get; set; } = null!;

        [ForeignKey("EmployeeId")]
        public virtual Employee Employee { get; set; } = null!;
    }
}