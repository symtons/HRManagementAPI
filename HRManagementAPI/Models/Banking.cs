// Models/Banking.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRManagementAPI.Models
{
    [Table("Banking")]
    public class Banking
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BankingId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        // Banking Information
        [Required]
        [StringLength(200)]
        public string BankName { get; set; }

        [Required]
        [StringLength(200)]
        public string AccountHolderName { get; set; }

        [Required]
        [StringLength(100)]
        public string BankAccountNumber { get; set; }

        [Required]
        [StringLength(50)]
        public string BankRoutingNumber { get; set; }

        [StringLength(50)]
        public string? AccountType { get; set; } // 'Checking' or 'Savings'

        // Status
        public bool IsActive { get; set; } = true;

        public bool IsVerified { get; set; } = false;

        // Audit Trail
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public int? CreatedBy { get; set; }

        public int? UpdatedBy { get; set; }

        // Navigation property
        [ForeignKey("EmployeeId")]
        public virtual Employee Employee { get; set; }
    }
}