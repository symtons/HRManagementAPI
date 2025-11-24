using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRManagementAPI.Models
{
    // =============================================
    // Shift Model
    // =============================================
    [Table("Shifts")]
    public class Shift
    {
        [Key]
        public int ShiftId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ShiftName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string ShiftCode { get; set; } = string.Empty;

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal WorkingHours { get; set; }

        public int BreakDuration { get; set; } // in minutes

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual ICollection<EmployeeShift> EmployeeShifts { get; set; } = new List<EmployeeShift>();
        public virtual ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
    }

    // =============================================
    // EmployeeShift Model
    // =============================================
    [Table("EmployeeShifts")]
    public class EmployeeShift
    {
        [Key]
        public int EmployeeShiftId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public int ShiftId { get; set; }

        [Required]
        public DateTime EffectiveDate { get; set; }

        public DateTime? EndDate { get; set; }

        public bool IsActive { get; set; } = true;

        public int? AssignedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("EmployeeId")]
        public virtual Employee? Employee { get; set; }

        [ForeignKey("ShiftId")]
        public virtual Shift? Shift { get; set; }
    }

    // =============================================
    // TimeEntry Model (Clock In/Out)
    // =============================================
    [Table("TimeEntries")]
    public class TimeEntry
    {
        [Key]
        public int TimeEntryId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public DateTime ClockInTime { get; set; }

        public DateTime? ClockOutTime { get; set; }

        [Required]
        public DateTime WorkDate { get; set; }

        public int? ShiftId { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? TotalHours { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? RegularHours { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? OvertimeHours { get; set; }

        public int BreakMinutes { get; set; } = 0;

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Open"; // Open, Closed, Approved

        [MaxLength(255)]
        public string? ClockInLocation { get; set; }

        [MaxLength(255)]
        public string? ClockOutLocation { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        [ForeignKey("EmployeeId")]
        public virtual Employee? Employee { get; set; }

        [ForeignKey("ShiftId")]
        public virtual Shift? Shift { get; set; }

        public virtual ICollection<TimesheetEntry> TimesheetEntries { get; set; } = new List<TimesheetEntry>();
    }

    // =============================================
    // Attendance Model
    // =============================================
    [Table("Attendance")]
    public class Attendance
    {
        [Key]
        public int AttendanceId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public DateTime AttendanceDate { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Present"; // Present, Absent, Late, HalfDay, Leave, Holiday

        public DateTime? ClockInTime { get; set; }

        public DateTime? ClockOutTime { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? WorkingHours { get; set; }

        public bool IsLate { get; set; } = false;

        public int? LateMinutes { get; set; }

        public bool IsEarlyLeave { get; set; } = false;

        public int? EarlyLeaveMinutes { get; set; }

        [MaxLength(500)]
        public string? Remarks { get; set; }

        public int? ApprovedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        [ForeignKey("EmployeeId")]
        public virtual Employee? Employee { get; set; }
    }

    // =============================================
    // Timesheet Model
    // =============================================
    [Table("Timesheets")]
    public class Timesheet
    {
        [Key]
        public int TimesheetId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Column(TypeName = "decimal(7,2)")]
        public decimal? TotalHours { get; set; }

        [Column(TypeName = "decimal(7,2)")]
        public decimal? RegularHours { get; set; }

        [Column(TypeName = "decimal(7,2)")]
        public decimal? OvertimeHours { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Draft"; // Draft, Submitted, Approved, Rejected

        public DateTime? SubmittedAt { get; set; }

        public int? ApprovedBy { get; set; }

        public DateTime? ApprovedAt { get; set; }

        [MaxLength(500)]
        public string? RejectionReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        [ForeignKey("EmployeeId")]
        public virtual Employee? Employee { get; set; }

        public virtual ICollection<TimesheetEntry> TimesheetEntries { get; set; } = new List<TimesheetEntry>();
    }

    // =============================================
    // TimesheetEntry Model
    // =============================================
    [Table("TimesheetEntries")]
    public class TimesheetEntry
    {
        [Key]
        public int TimesheetEntryId { get; set; }

        [Required]
        public int TimesheetId { get; set; }

        [Required]
        public DateTime WorkDate { get; set; }

        public int? TimeEntryId { get; set; }

        public DateTime? StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? Hours { get; set; }

        [MaxLength(500)]
        public string? TaskDescription { get; set; }

        public bool IsBillable { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        [ForeignKey("TimesheetId")]
        public virtual Timesheet? Timesheet { get; set; }

        [ForeignKey("TimeEntryId")]
        public virtual TimeEntry? TimeEntry { get; set; }
    }

    // =============================================
    // OvertimeRequest Model
    // =============================================
    [Table("OvertimeRequests")]
    public class OvertimeRequest
    {
        [Key]
        public int OvertimeRequestId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public DateTime RequestDate { get; set; }

        [Required]
        public DateTime OvertimeDate { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal RequestedHours { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected

        public int? ReviewedBy { get; set; }

        public DateTime? ReviewedAt { get; set; }

        [MaxLength(500)]
        public string? ReviewComments { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        [ForeignKey("EmployeeId")]
        public virtual Employee? Employee { get; set; }
    }
}
