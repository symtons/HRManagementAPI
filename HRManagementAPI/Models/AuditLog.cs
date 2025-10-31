using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRManagementAPI.Models
{
    [Table("AuditLogs")]
    public class AuditLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long AuditLogId { get; set; }

        // Who performed the action
        public int? UserId { get; set; }

        [StringLength(255)]
        public string? UserEmail { get; set; }

        [StringLength(100)]
        public string? UserRole { get; set; }

        // What action was performed
        [Required]
        [StringLength(50)]
        public string Action { get; set; }

        [Required]
        [StringLength(100)]
        public string EntityName { get; set; }

        public int? EntityId { get; set; }

        // Details of the action
        [Column(TypeName = "nvarchar(max)")]
        public string? OldValues { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? NewValues { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        // Request Information
        [StringLength(255)]
        public string? Endpoint { get; set; }

        [StringLength(10)]
        public string? HttpMethod { get; set; }

        [StringLength(50)]
        public string? IpAddress { get; set; }

        [StringLength(500)]
        public string? UserAgent { get; set; }

        // Status
        [Required]
        [StringLength(20)]
        public string Status { get; set; }

        [StringLength(1000)]
        public string? ErrorMessage { get; set; }

        // Timestamp
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Additional metadata
        [Column(TypeName = "nvarchar(max)")]
        public string? AdditionalData { get; set; }

        // Navigation property
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }
    }
}