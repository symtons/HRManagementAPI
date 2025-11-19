using Microsoft.EntityFrameworkCore;
using HRManagementAPI.Models;

namespace HRManagementAPI.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSets - represent tables in the database
        public DbSet<Role> Roles { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<RoleMenuPermission> RoleMenuPermissions { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Banking> Banking { get; set; }
        public DbSet<LeaveType> LeaveTypes { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<LeaveBalance> LeaveBalance { get; set; }
        public DbSet<LeaveCalendar> LeaveCalendar { get; set; }
    }
}