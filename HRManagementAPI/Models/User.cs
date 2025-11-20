using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRManagementAPI.Models
{
    [Table("Users")]
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserId { get; set; }

        [Required]
        public int RoleId { get; set; }

        [Required]
        [StringLength(255)]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [StringLength(255)]
        public string PasswordHash { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastLoginAt { get; set; }

        // Navigation properties
        [ForeignKey("RoleId")]
        public virtual Role Role { get; set; }

        public virtual Employee Employee { get; set; }



        [MaxLength(50)]
        public string AccountStatus { get; set; } = "Active";
        // PendingOnboarding/Active/Inactive/Suspended

        [MaxLength(50)]
        public string OnboardingStatus { get; set; } = "NotStarted";
        // NotStarted/InProgress/Completed

        public DateTime? OnboardingCompletedDate { get; set; }
       


        public int? EmployeeId { get; set; }
    }
}