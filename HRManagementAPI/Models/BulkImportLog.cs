using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRManagementAPI.Models
{
    public class BulkImportLog
    {
        [Key]
        public int ImportLogId { get; set; }
        public DateTime ImportDate { get; set; } = DateTime.UtcNow;
        public int ImportedBy { get; set; }
        public string? FileName { get; set; }
        public int TotalRecords { get; set; }
        public int SuccessfulRecords { get; set; } = 0;
        public int FailedRecords { get; set; } = 0;
        public int WarningRecords { get; set; } = 0;
        public string ImportStatus { get; set; } = "Pending";
        public string? ErrorSummary { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("ImportedBy")]
        public virtual User? ImportedByUser { get; set; }
        public virtual ICollection<BulkImportDetail> Details { get; set; } = new List<BulkImportDetail>();
    }
}
