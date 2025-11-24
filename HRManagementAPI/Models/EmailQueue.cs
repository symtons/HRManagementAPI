using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRManagementAPI.Models
{
    [Table("EmailQueue")]
    public class EmailQueue
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int EmailId { get; set; }

        [Required]
        [MaxLength(255)]
        [EmailAddress]
        public string ToEmail { get; set; }

        [MaxLength(255)]
        public string? ToName { get; set; }

        [MaxLength(255)]
        [EmailAddress]
        public string? FromEmail { get; set; }

        [MaxLength(255)]
        public string? FromName { get; set; }

        [Required]
        [MaxLength(500)]
        public string Subject { get; set; }

        [Required]
        public string Body { get; set; } // HTML or plain text

        [MaxLength(50)]
        public string EmailType { get; set; } // WelcomeEmail, PasswordReset, LeaveApproval, etc.

        [MaxLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, Sent, Failed, Cancelled

        public int? RetryCount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? SentAt { get; set; }

        public DateTime? FailedAt { get; set; }

        public string? ErrorMessage { get; set; }

        public string? Metadata { get; set; } // JSON for any additional data

        // Optional: Priority for email sending
        public int Priority { get; set; } = 5; // 1=High, 5=Normal, 10=Low
    }
}
