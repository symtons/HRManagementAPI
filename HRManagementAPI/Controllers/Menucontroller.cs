using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using HRManagementAPI.Data;
using HRManagementAPI.Models;

namespace HRManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MenuController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MenuController(ApplicationDbContext context)
        {
            _context = context;
        }

        // UPDATED MenuController.cs - Add restriction for "InProgress" status
        // Replace the GetMyMenus method in MenuController.cs

        [HttpGet("MyMenus")]
        public async Task<IActionResult> GetMyMenus()
        {
            // Get user ID and role from JWT token
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userRole) || string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized(new { message = "User authentication failed" });
            }

            int userId = int.Parse(userIdClaim);

            // Check user's onboarding status
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            // ========================================
            // ✅ NEW: Handle both "NotStarted" AND "InProgress"
            // ========================================
            bool isOnboardingIncomplete = user.OnboardingStatus == "NotStarted" ||
                                          user.OnboardingStatus == "InProgress";

            if (isOnboardingIncomplete)
            {
                // Get only essential menus during onboarding
                var onboardingMenus = await _context.MenuItems
                    .Where(m => m.IsActive && m.IsVisible &&
                           (m.MenuName == "Dashboard" ||
                            m.MenuName == "Onboarding" ||
                            m.MenuName == "Profile" ||
                            m.MenuName == "Help"))
                    .Select(m => new MenuItemDto
                    {
                        MenuId = m.MenuId,
                        ParentMenuId = m.ParentMenuId,
                        MenuName = m.MenuName,
                        MenuTitle = m.MenuTitle,
                        MenuIcon = m.MenuIcon,
                        MenuUrl = m.MenuUrl,
                        MenuOrder = m.MenuOrder,
                        CanView = true,
                        CanCreate = false,
                        CanEdit = false,
                        CanDelete = false,
                        SubMenus = new List<MenuItemDto>()
                    })
                    .OrderBy(m => m.MenuOrder)
                    .ToListAsync();

                return Ok(new
                {
                    menus = onboardingMenus,
                    onboardingRequired = true,
                    message = user.OnboardingStatus == "NotStarted" ?
                             "Complete onboarding to access all features" :
                             "Complete all onboarding tasks to unlock full access"
                });
            }

            // ========================================
            // Normal flow for users who completed onboarding
            // ========================================
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == userRole);

            if (role == null)
            {
                return NotFound(new { message = "Role not found" });
            }

            var menus = await _context.MenuItems
                .Where(m => m.IsActive && m.IsVisible)
                .Join(_context.RoleMenuPermissions,
                    menu => menu.MenuId,
                    perm => perm.MenuId,
                    (menu, perm) => new { Menu = menu, Permission = perm })
                .Where(x => x.Permission.RoleId == role.RoleId && x.Permission.CanView)
                .Select(x => new MenuItemDto
                {
                    MenuId = x.Menu.MenuId,
                    ParentMenuId = x.Menu.ParentMenuId,
                    MenuName = x.Menu.MenuName,
                    MenuTitle = x.Menu.MenuTitle,
                    MenuIcon = x.Menu.MenuIcon,
                    MenuUrl = x.Menu.MenuUrl,
                    MenuOrder = x.Menu.MenuOrder,
                    CanView = x.Permission.CanView,
                    CanCreate = x.Permission.CanCreate,
                    CanEdit = x.Permission.CanEdit,
                    CanDelete = x.Permission.CanDelete
                })
                .OrderBy(m => m.MenuOrder)
                .ToListAsync();

            var hierarchicalMenus = BuildMenuHierarchy(menus);

            return Ok(new
            {
                menus = hierarchicalMenus,
                onboardingRequired = false
            });
        }

        // GET: api/Menu/AllMenus
        // Admin endpoint to get all menus
        [HttpGet("AllMenus")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllMenus()
        {
            var menus = await _context.MenuItems
                .OrderBy(m => m.MenuOrder)
                .Select(m => new
                {
                    m.MenuId,
                    m.ParentMenuId,
                    m.MenuName,
                    m.MenuTitle,
                    m.MenuIcon,
                    m.MenuUrl,
                    m.MenuOrder,
                    m.IsActive,
                    m.IsVisible
                })
                .ToListAsync();

            return Ok(menus);
        }

        // GET: api/Menu/RolePermissions/{roleId}
        // Get all menu permissions for a specific role
        [HttpGet("RolePermissions/{roleId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetRolePermissions(int roleId)
        {
            var permissions = await _context.RoleMenuPermissions
                .Include(p => p.MenuItem)
                .Where(p => p.RoleId == roleId)
                .Select(p => new
                {
                    p.PermissionId,
                    p.RoleId,
                    p.MenuId,
                    MenuName = p.MenuItem.MenuName,
                    MenuTitle = p.MenuItem.MenuTitle,
                    p.CanView,
                    p.CanCreate,
                    p.CanEdit,
                    p.CanDelete
                })
                .ToListAsync();

            return Ok(permissions);
        }

        // POST: api/Menu/UpdatePermission
        // Update menu permission for a role
        [HttpPost("UpdatePermission")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdatePermission([FromBody] UpdatePermissionRequest request)
        {
            var permission = await _context.RoleMenuPermissions
                .FirstOrDefaultAsync(p => p.RoleId == request.RoleId && p.MenuId == request.MenuId);

            if (permission == null)
            {
                // Create new permission
                permission = new RoleMenuPermission
                {
                    RoleId = request.RoleId,
                    MenuId = request.MenuId,
                    CanView = request.CanView,
                    CanCreate = request.CanCreate,
                    CanEdit = request.CanEdit,
                    CanDelete = request.CanDelete
                };
                _context.RoleMenuPermissions.Add(permission);
            }
            else
            {
                // Update existing permission
                permission.CanView = request.CanView;
                permission.CanCreate = request.CanCreate;
                permission.CanEdit = request.CanEdit;
                permission.CanDelete = request.CanDelete;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Permission updated successfully" });
        }

        // Helper method to build hierarchical menu structure
        private List<MenuItemDto> BuildMenuHierarchy(List<MenuItemDto> allMenus)
        {
            // Get top-level menus (no parent)
            var topLevelMenus = allMenus
                .Where(m => m.ParentMenuId == null)
                .OrderBy(m => m.MenuOrder)
                .ToList();

            // Add children to each top-level menu
            foreach (var menu in topLevelMenus)
            {
                menu.SubMenus = GetSubMenus(menu.MenuId, allMenus);
            }

            return topLevelMenus;
        }

        // Recursive method to get sub-menus
        private List<MenuItemDto> GetSubMenus(int parentMenuId, List<MenuItemDto> allMenus)
        {
            var subMenus = allMenus
                .Where(m => m.ParentMenuId == parentMenuId)
                .OrderBy(m => m.MenuOrder)
                .ToList();

            foreach (var subMenu in subMenus)
            {
                subMenu.SubMenus = GetSubMenus(subMenu.MenuId, allMenus);
            }

            return subMenus;
        }
    }

    // DTOs
    public class MenuItemDto
    {
        public int MenuId { get; set; }
        public int? ParentMenuId { get; set; }
        public string MenuName { get; set; }
        public string MenuTitle { get; set; }
        public string? MenuIcon { get; set; }
        public string? MenuUrl { get; set; }
        public int MenuOrder { get; set; }
        public bool CanView { get; set; }
        public bool CanCreate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public List<MenuItemDto> SubMenus { get; set; } = new List<MenuItemDto>();
    }

    public class UpdatePermissionRequest
    {
        public int RoleId { get; set; }
        public int MenuId { get; set; }
        public bool CanView { get; set; }
        public bool CanCreate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }
}