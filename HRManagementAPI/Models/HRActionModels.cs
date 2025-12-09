using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRManagementAPI.Models
{
    // ============================================
    // HR ACTION TYPES TABLE
    // ============================================
    [Table("HRActionTypes")]
    public class HRActionType
    {
        [Key]
        public int ActionTypeId { get; set; }

        [Required]
        [StringLength(50)]
        public string ActionTypeName { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public bool RequiresFinanceApproval { get; set; }
        public bool RequiresAdminApproval { get; set; }
        public bool IsActive { get; set; }
        public int DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ============================================
    // HR ACTION REQUESTS TABLE
    // ============================================
    [Table("HRActionRequests")]
    public class HRActionRequest
    {
        [Key]
        public int RequestId { get; set; }

        [StringLength(50)]
        public string? RequestNumber { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public int ActionTypeId { get; set; }

        public DateTime RequestDate { get; set; }
        public DateTime? EffectiveDate { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Pending";

        public int CurrentApprovalLevel { get; set; } = 1;

        // Approval Workflow
        public int? SupervisorId { get; set; }
        [StringLength(50)]
        public string? SupervisorApprovalStatus { get; set; }
        public DateTime? SupervisorApprovedAt { get; set; }
        public string? SupervisorComments { get; set; }

        public int? AdminId { get; set; }
        [StringLength(50)]
        public string? AdminApprovalStatus { get; set; }
        public DateTime? AdminApprovedAt { get; set; }
        public string? AdminComments { get; set; }

        public int? HRId { get; set; }
        [StringLength(50)]
        public string? HRApprovalStatus { get; set; }
        public DateTime? HRApprovedAt { get; set; }
        public string? HRComments { get; set; }

        public int? FinanceId { get; set; }
        [StringLength(50)]
        public string? FinanceApprovalStatus { get; set; }
        public DateTime? FinanceApprovedAt { get; set; }
        public string? FinanceComments { get; set; }

        // Documents
        public bool HasAttachments { get; set; }
        public bool? W4Attached { get; set; }
        [StringLength(255)]
        public string? W4FileName { get; set; }
        [StringLength(500)]
        public string? W4FilePath { get; set; }

        public bool? DoctorNoteAttached { get; set; }
        [StringLength(255)]
        public string? DoctorNoteFileName { get; set; }
        [StringLength(500)]
        public string? DoctorNoteFilePath { get; set; }

        public bool? OtherDocumentAttached { get; set; }
        [StringLength(255)]
        public string? OtherDocumentFileName { get; set; }
        [StringLength(500)]
        public string? OtherDocumentFilePath { get; set; }
        [StringLength(100)]
        public string? OtherDocumentType { get; set; }

        // Common
        public string? Reason { get; set; }
        public string? Notes { get; set; }

        // Rate Change
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? OldRate { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? NewRate { get; set; }
        [StringLength(20)]
        public string? OldRateType { get; set; }
        [StringLength(20)]
        public string? NewRateType { get; set; }
        [StringLength(200)]
        public string? PremiumIncentive { get; set; }

        // Transfer
        public int? OldDepartmentId { get; set; }
        public int? NewDepartmentId { get; set; }
        [StringLength(200)]
        public string? OldLocation { get; set; }
        [StringLength(200)]
        public string? NewLocation { get; set; }
        public int? OldSupervisorId { get; set; }
        public int? NewSupervisorId { get; set; }
        [StringLength(10)]
        public string? OldClassification { get; set; }
        [StringLength(10)]
        public string? NewClassification { get; set; }
        [StringLength(100)]
        public string? OldShiftHours { get; set; }
        [StringLength(100)]
        public string? NewShiftHours { get; set; }

        // Promotion
        [StringLength(200)]
        public string? OldJobTitle { get; set; }
        [StringLength(200)]
        public string? NewJobTitle { get; set; }

        // Status Change
        [StringLength(20)]
        public string? OldEmploymentType { get; set; }
        [StringLength(20)]
        public string? NewEmploymentType { get; set; }
        [StringLength(20)]
        public string? OldPayType { get; set; }
        [StringLength(20)]
        public string? NewPayType { get; set; }
        [StringLength(20)]
        public string? OldMaritalStatus { get; set; }
        [StringLength(20)]
        public string? NewMaritalStatus { get; set; }

        // Personal Info
        [StringLength(100)]
        public string? OldFirstName { get; set; }
        [StringLength(100)]
        public string? NewFirstName { get; set; }
        [StringLength(100)]
        public string? OldLastName { get; set; }
        [StringLength(100)]
        public string? NewLastName { get; set; }
        [StringLength(500)]
        public string? OldAddress { get; set; }
        [StringLength(500)]
        public string? NewAddress { get; set; }
        [StringLength(100)]
        public string? OldCity { get; set; }
        [StringLength(100)]
        public string? NewCity { get; set; }
        [StringLength(50)]
        public string? OldState { get; set; }
        [StringLength(50)]
        public string? NewState { get; set; }
        [StringLength(20)]
        public string? OldZip { get; set; }
        [StringLength(20)]
        public string? NewZip { get; set; }
        [StringLength(20)]
        public string? OldPhone { get; set; }
        [StringLength(20)]
        public string? NewPhone { get; set; }
        [StringLength(20)]
        public string? OldCellPhone { get; set; }
        [StringLength(20)]
        public string? NewCellPhone { get; set; }
        [StringLength(255)]
        public string? OldEmail { get; set; }
        [StringLength(255)]
        public string? NewEmail { get; set; }

        // Insurance
        [StringLength(50)]
        public string? HealthInsuranceChange { get; set; }
        [Column(TypeName = "decimal(10, 2)")]
        public decimal? HealthInsuranceDeduction { get; set; }
        [StringLength(50)]
        public string? DentalInsuranceChange { get; set; }
        [Column(TypeName = "decimal(10, 2)")]
        public decimal? DentalInsuranceDeduction { get; set; }
        public bool? Retirement403bEnroll { get; set; }
        [Column(TypeName = "decimal(10, 2)")]
        public decimal? Retirement403bDeduction { get; set; }
        public DateTime? InsuranceEffectiveDate { get; set; }

        // Payroll Deduction
        [StringLength(500)]
        public string? PayrollDeductionDescription { get; set; }
        [Column(TypeName = "decimal(10, 2)")]
        public decimal? PayrollDeductionAmount { get; set; }
        [StringLength(50)]
        public string? PayrollDeductionFrequency { get; set; }

        // Leave of Absence
        [StringLength(100)]
        public string? LeaveType { get; set; }
        public DateTime? LeaveStartDate { get; set; }
        public DateTime? LeaveEndDate { get; set; }
        public int? LeaveDays { get; set; }
        public DateTime? LeaveReturnDate { get; set; }
        public DateTime? LeaveLastDayWorked { get; set; }
        public bool? LeaveExcused { get; set; }
        public bool? LeaveDoctorSlipReceived { get; set; }
        public string? LeaveAccommodation { get; set; }
        [StringLength(100)]
        public string? LeaveRelationToDeceased { get; set; }

        // Workflow
        [Required]
        public int SubmittedBy { get; set; }
        public DateTime SubmittedAt { get; set; }
        public int? FinalApprovedBy { get; set; }
        public DateTime? FinalApprovedAt { get; set; }
        public int? RejectedBy { get; set; }
        public DateTime? RejectedAt { get; set; }
        public string? RejectionReason { get; set; }

        // System
        public bool IsProcessed { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public int? ProcessedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation Properties
        [ForeignKey("EmployeeId")]
        public virtual Employee Employee { get; set; }

        [ForeignKey("ActionTypeId")]
        public virtual HRActionType ActionType { get; set; }

        [ForeignKey("OldDepartmentId")]
        public virtual Department? OldDepartment { get; set; }

        [ForeignKey("NewDepartmentId")]
        public virtual Department? NewDepartment { get; set; }
    }
}