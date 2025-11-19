using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRManagementAPI.Models
{
    [Table("Employees")]
    public class Employee
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int EmployeeId { get; set; }

        public int? UserId { get; set; }

        // Personal Information
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(100)]
        public string LastName { get; set; }

        [StringLength(100)]
        public string? MiddleName { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [StringLength(20)]
        public string? Gender { get; set; }

        [StringLength(20)]
        public string? MaritalStatus { get; set; }

        // Contact Information
        [StringLength(20)]
        public string? PhoneNumber { get; set; }

        [StringLength(255)]
        [EmailAddress]
        public string? PersonalEmail { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }
       
        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(100)]
        public string? State { get; set; }


        [StringLength(20)]
        public string? ZipCode { get; set; }

        [StringLength(100)]
        public string? Country { get; set; }

        // Emergency Contact
        [StringLength(200)]
        public string? EmergencyContactName { get; set; }

        [StringLength(20)]
        public string? EmergencyContactPhone { get; set; }

        [StringLength(50)]
        public string? EmergencyContactRelationship { get; set; }

        // Employment Information
        [Required]
        [StringLength(50)]
        public string EmployeeCode { get; set; }

        public int? DepartmentId { get; set; }

        public int? ManagerId { get; set; }

        [StringLength(100)]
        public string? JobTitle { get; set; }

        [Required]
        [StringLength(50)]
        public string EmployeeType { get; set; } // AdminStaff or FieldStaff

        [StringLength(50)]
        public string EmploymentStatus { get; set; } = "Active";

        public DateTime? HireDate { get; set; }

        public DateTime? TerminationDate { get; set; }

        // Compensation
        [Column(TypeName = "decimal(18,2)")]
        public decimal? Salary { get; set; }

        [StringLength(50)]
        public string? PayFrequency { get; set; }

        // Bank Information
        [StringLength(200)]
        public string? BankName { get; set; }

        [StringLength(100)]
        public string? BankAccountNumber { get; set; }

        [StringLength(50)]
        public string? BankRoutingNumber { get; set; }

        // Benefits
        public bool IsEligibleForPTO { get; set; } = false;

        [Column(TypeName = "decimal(10,2)")]
        public decimal PTOBalance { get; set; } = 0;

        public bool IsEligibleForInsurance { get; set; } = false;

        // Documents
        [StringLength(500)]
        public string? ProfilePicturePath { get; set; }

        [StringLength(500)]
        public string? ResumePath { get; set; }

        // Status
        public bool IsActive { get; set; } = true;

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public int? CreatedBy { get; set; }

        public int? UpdatedBy { get; set; }

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        [ForeignKey("DepartmentId")]
        public virtual Department? Department { get; set; }

        [ForeignKey("ManagerId")]
        public virtual Employee? Manager { get; set; }

        public virtual ICollection<Employee> Subordinates { get; set; }

        public virtual Banking? Banking { get; set; }
    }
}