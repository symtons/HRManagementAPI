using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRManagementAPI.Models
{
    public class BulkImportDetail
    {
        [Key]
        public int ImportDetailId { get; set; }
        public int ImportLogId { get; set; }
        public int RowNumber { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string ImportStatus { get; set; } = "Pending";
        public string? ErrorMessage { get; set; }
        public string? WarningMessage { get; set; }
        public int? EmployeeId { get; set; }
        public int? UserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("ImportLogId")]
        public virtual BulkImportLog? ImportLog { get; set; }
    }
}