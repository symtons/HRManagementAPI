using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRManagementAPI.Models
{
    [Table("MenuItems")]
    public class MenuItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MenuId { get; set; }

        public int? ParentMenuId { get; set; }

        [Required]
        [StringLength(100)]
        public string MenuName { get; set; }

        [Required]
        [StringLength(100)]
        public string MenuTitle { get; set; }

        [StringLength(50)]
        public string? MenuIcon { get; set; }

        [StringLength(255)]
        public string? MenuUrl { get; set; }

        public int MenuOrder { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public bool IsVisible { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("ParentMenuId")]
        public virtual MenuItem? ParentMenu { get; set; }

        public virtual ICollection<MenuItem> SubMenus { get; set; }

        public virtual ICollection<RoleMenuPermission> RoleMenuPermissions { get; set; }
    }
}