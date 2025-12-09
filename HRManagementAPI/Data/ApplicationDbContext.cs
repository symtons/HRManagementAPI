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

        public DbSet<JobApplication> JobApplications { get; set; }
        public DbSet<OnboardingTask> OnboardingTasks { get; set; }
        public DbSet<EmployeeOnboardingTask> EmployeeOnboardingTasks { get; set; }
        public DbSet<EmailQueue> EmailQueue { get; set; }

        // Time and Attendance
        public DbSet<Shift> Shifts { get; set; }
        public DbSet<EmployeeShift> EmployeeShifts { get; set; }
        public DbSet<TimeEntry> TimeEntries { get; set; }
        public DbSet<Attendance> Attendance { get; set; }
        public DbSet<Timesheet> Timesheets { get; set; }
        public DbSet<TimesheetEntry> TimesheetEntries { get; set; }
        public DbSet<OvertimeRequest> OvertimeRequests { get; set; }

        // Performance Management
        public DbSet<ReviewCycle> ReviewCycles { get; set; }
        public DbSet<PerformanceReview> PerformanceReviews { get; set; }
        public DbSet<ReviewRating> ReviewRatings { get; set; }
        public DbSet<Goal> Goals { get; set; }
        public DbSet<GoalUpdate> GoalUpdates { get; set; }
        public DbSet<Feedback> Feedback { get; set; }

        public DbSet<HRActionType> HRActionTypes { get; set; }
        public DbSet<HRActionRequest> HRActionRequests { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Tell EF that JobApplications has a trigger
            modelBuilder.Entity<JobApplication>()
                .ToTable(tb => tb.HasTrigger("trg_CreateUserOnApproval"));
        }
    }
}