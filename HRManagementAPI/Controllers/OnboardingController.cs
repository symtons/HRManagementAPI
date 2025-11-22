using HRManagementAPI.Data;
using HRManagementAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;

namespace HRManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OnboardingController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public OnboardingController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // POST: api/Onboarding/Initialize/{employeeId}
        // Manually initialize onboarding tasks for an employee (Admin/HR only)
        [HttpPost("Initialize/{employeeId}")]
        [Authorize(Roles = "Admin,Executive,HRManager")]
        public async Task<IActionResult> InitializeOnboarding(int employeeId)
        {
            try
            {
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

                if (employee == null)
                {
                    return NotFound(new { message = "Employee not found" });
                }

                // Check if onboarding already initialized
                var existingTasks = await _context.EmployeeOnboardingTasks
                    .AnyAsync(t => t.EmployeeId == employeeId);

                if (existingTasks)
                {
                    return BadRequest(new { message = "Onboarding tasks already initialized for this employee" });
                }

                // Get all active tasks applicable to this employee type
                var tasks = await _context.OnboardingTasks
                    .Where(t => t.IsActive &&
                        (t.ApplicableEmployeeType == "Both" || t.ApplicableEmployeeType == employee.EmployeeType))
                    .ToListAsync();

                foreach (var task in tasks)
                {
                    var employeeTask = new EmployeeOnboardingTask
                    {
                        EmployeeId = employeeId,
                        TaskId = task.TaskId,
                        AssignedDate = DateTime.UtcNow,
                        DueDate = DateTime.UtcNow.AddDays(task.DefaultDueDays),
                        Status = "Pending",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.EmployeeOnboardingTasks.Add(employeeTask);
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Onboarding tasks initialized successfully",
                    tasksCreated = tasks.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error initializing onboarding", error = ex.Message });
            }
        }

        // GET: api/Onboarding/MyTasks
        // Employee gets their own onboarding tasks
        [HttpGet("MyTasks")]
        public async Task<IActionResult> GetMyTasks()
        {
            try
            {
                // Get employee ID from token
                var employeeIdClaim = User.FindFirst("EmployeeId")?.Value;
                if (string.IsNullOrEmpty(employeeIdClaim))
                {
                    return BadRequest(new { message = "Employee ID not found in token" });
                }
                int employeeId = int.Parse(employeeIdClaim);

                var tasks = await _context.EmployeeOnboardingTasks
                    .Include(t => t.Task)
                    .Where(t => t.EmployeeId == employeeId)
                    .OrderBy(t => t.Task.DisplayOrder)
                    .Select(t => new
                    {
                        t.OnboardingTaskId,
                        t.TaskId,
                        TaskName = t.Task.TaskName,
                        TaskDescription = t.Task.TaskDescription,
                        TaskCategory = t.Task.TaskCategory,
                        TaskType = t.Task.TaskType,
                        InstructionText = t.Task.InstructionText,
                        RequiredFileTypes = t.Task.RequiredFileTypes,
                        IsRequired = t.Task.IsRequired,
                        t.AssignedDate,
                        t.DueDate,
                        t.CompletedDate,
                        t.Status,
                        t.SubmittedData,
                        t.DocumentPath,
                        t.DocumentOriginalName,
                        t.Notes,
                        IsOverdue = t.Status != "Completed" && t.DueDate < DateTime.UtcNow.Date,
                        t.IsVerified,
                        t.VerificationNotes
                    })
                    .ToListAsync();

                // Calculate progress
                var totalTasks = tasks.Count;
                var completedTasks = tasks.Count(t => t.Status == "Completed");
                var progressPercentage = totalTasks > 0 ? Math.Round((double)completedTasks / totalTasks * 100, 2) : 0;

                return Ok(new
                {
                    tasks,
                    totalTasks,
                    completedTasks,
                    pendingTasks = totalTasks - completedTasks,
                    progressPercentage
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving tasks", error = ex.Message });
            }
        }

        // GET: api/Onboarding/Employee/{employeeId}
        // HR gets onboarding tasks for specific employee
        [HttpGet("Employee/{employeeId}")]
        [Authorize(Roles = "Admin,Executive,HRManager")]
        public async Task<IActionResult> GetEmployeeTasks(int employeeId)
        {
            try
            {
                var employee = await _context.Employees
                    .Include(e => e.Department)
                    .Include(e => e.User)
                    .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

                if (employee == null)
                {
                    return NotFound(new { message = "Employee not found" });
                }

                var tasks = await _context.EmployeeOnboardingTasks
                    .Include(t => t.Task)
                    .Where(t => t.EmployeeId == employeeId)
                    .OrderBy(t => t.Task.DisplayOrder)
                    .Select(t => new
                    {
                        t.OnboardingTaskId,
                        t.TaskId,
                        TaskName = t.Task.TaskName,
                        TaskDescription = t.Task.TaskDescription,
                        TaskCategory = t.Task.TaskCategory,
                        TaskType = t.Task.TaskType,
                        IsRequired = t.Task.IsRequired,
                        t.AssignedDate,
                        t.DueDate,
                        t.CompletedDate,
                        t.Status,
                        t.SubmittedData,
                        t.DocumentPath,
                        t.DocumentOriginalName,
                        t.Notes,
                        IsOverdue = t.Status != "Completed" && t.DueDate < DateTime.UtcNow.Date,
                        t.IsVerified,
                        t.VerificationNotes,
                        CompletedByEmail = t.CompletedBy.HasValue ?
                            _context.Users.Where(u => u.UserId == t.CompletedBy).Select(u => u.Email).FirstOrDefault() : null,
                        VerifiedByEmail = t.VerifiedBy.HasValue ?
                            _context.Users.Where(u => u.UserId == t.VerifiedBy).Select(u => u.Email).FirstOrDefault() : null
                    })
                    .ToListAsync();

                var totalTasks = tasks.Count;
                var completedTasks = tasks.Count(t => t.Status == "Completed");
                var progressPercentage = totalTasks > 0 ? Math.Round((double)completedTasks / totalTasks * 100, 2) : 0;

                return Ok(new
                {
                    employee = new
                    {
                        employee.EmployeeId,
                        employee.EmployeeCode,
                        employee.FirstName,
                        employee.LastName,
                        FullName = $"{employee.FirstName} {employee.LastName}",
                        employee.JobTitle,
                        Department = employee.Department?.DepartmentName,
                        employee.HireDate,
                        employee.EmploymentStatus,
                        Email = employee.User?.Email,
                        AccountStatus = employee.User?.AccountStatus,
                        OnboardingStatus = employee.User?.OnboardingStatus
                    },
                    tasks,
                    totalTasks,
                    completedTasks,
                    pendingTasks = totalTasks - completedTasks,
                    progressPercentage
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving employee tasks", error = ex.Message });
            }
        }

        // GET: api/Onboarding/Monitor
        // HR gets all employees currently in onboarding
        [HttpGet("Monitor")]
        [Authorize(Roles = "Admin,Executive,HRManager")]
        public async Task<IActionResult> GetOnboardingMonitor()
        {
            try
            {
                // Get all employees with PendingOnboarding status
                var employees = await _context.Employees
                    .Include(e => e.Department)
                    .Include(e => e.User)
                    .Where(e => e.User != null && e.User.AccountStatus == "PendingOnboarding")
                    .Select(e => new
                    {
                        e.EmployeeId,
                        e.EmployeeCode,
                        e.FirstName,
                        e.LastName,
                        FullName = $"{e.FirstName} {e.LastName}",
                        e.JobTitle,
                        Department = e.Department.DepartmentName,
                        e.HireDate,
                        Email = e.User.Email,
                        OnboardingStatus = e.User.OnboardingStatus,
                        DaysSinceHire = (DateTime.UtcNow.Date - e.HireDate.Value.Date).Days
                    })
                    .ToListAsync();

                // Get task completion stats for each employee
                var employeeStats = new List<object>();

                foreach (var emp in employees)
                {
                    var tasks = await _context.EmployeeOnboardingTasks
                        .Include(t => t.Task)
                        .Where(t => t.EmployeeId == emp.EmployeeId)
                        .ToListAsync();

                    var totalTasks = tasks.Count;
                    var completedTasks = tasks.Count(t => t.Status == "Completed");
                    var overdueTasks = tasks.Count(t => t.Status != "Completed" && t.DueDate < DateTime.UtcNow.Date);
                    var progressPercentage = totalTasks > 0 ? Math.Round((double)completedTasks / totalTasks * 100, 2) : 0;

                    employeeStats.Add(new
                    {
                        emp.EmployeeId,
                        emp.EmployeeCode,
                        emp.FirstName,
                        emp.LastName,
                        emp.FullName,
                        emp.JobTitle,
                        emp.Department,
                        emp.HireDate,
                        emp.Email,
                        emp.OnboardingStatus,
                        emp.DaysSinceHire,
                        totalTasks,
                        completedTasks,
                        pendingTasks = totalTasks - completedTasks,
                        overdueTasks,
                        progressPercentage
                    });
                }

                return Ok(employeeStats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving onboarding monitor", error = ex.Message });
            }
        }

        // PUT: api/Onboarding/Task/{taskId}/Complete
        // Employee completes a task (text submission or acknowledgment)
        [HttpPut("Task/{taskId}/Complete")]
        public async Task<IActionResult> CompleteTask(int taskId, [FromBody] CompleteTaskRequest request)
        {
            try
            {
                // Get user ID from token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return Unauthorized(new { message = "User ID not found in token" });
                }
                int userId = int.Parse(userIdClaim);

                // Get employee ID from token
                var employeeIdClaim = User.FindFirst("EmployeeId")?.Value;
                if (string.IsNullOrEmpty(employeeIdClaim))
                {
                    return BadRequest(new { message = "Employee ID not found in token" });
                }
                int employeeId = int.Parse(employeeIdClaim);

                var task = await _context.EmployeeOnboardingTasks
                    .Include(t => t.Task)
                    .FirstOrDefaultAsync(t => t.OnboardingTaskId == taskId && t.EmployeeId == employeeId);

                if (task == null)
                {
                    return NotFound(new { message = "Task not found" });
                }

                if (task.Status == "Completed")
                {
                    return BadRequest(new { message = "Task is already completed" });
                }

                // Update task
                task.Status = "Completed";
                task.CompletedDate = DateTime.UtcNow;
                task.CompletedBy = userId;
                task.SubmittedData = request.SubmittedData;
                task.Notes = request.Notes;
                task.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Check if all required tasks are completed
                await CheckAndActivateAccount(employeeId);

                return Ok(new { message = "Task completed successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error completing task", error = ex.Message });
            }
        }

        // POST: api/Onboarding/Task/{taskId}/Upload
        // Employee uploads document for a task
        [HttpPost("Task/{taskId}/Upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadDocument(int taskId, IFormFile file)

        {
            try
            {
                // Get user ID from token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return Unauthorized(new { message = "User ID not found in token" });
                }
                int userId = int.Parse(userIdClaim);

                // Get employee ID from token
                var employeeIdClaim = User.FindFirst("EmployeeId")?.Value;
                if (string.IsNullOrEmpty(employeeIdClaim))
                {
                    return BadRequest(new { message = "Employee ID not found in token" });
                }
                int employeeId = int.Parse(employeeIdClaim);

                var task = await _context.EmployeeOnboardingTasks
                    .Include(t => t.Task)
                    .FirstOrDefaultAsync(t => t.OnboardingTaskId == taskId && t.EmployeeId == employeeId);

                if (task == null)
                {
                    return NotFound(new { message = "Task not found" });
                }

                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { message = "No file uploaded" });
                }

                // Validate file type if RequiredFileTypes is specified
                if (!string.IsNullOrEmpty(task.Task.RequiredFileTypes))
                {
                    var allowedTypes = task.Task.RequiredFileTypes.Split(',').Select(t => t.Trim().ToLower()).ToList();
                    var fileExtension = Path.GetExtension(file.FileName).TrimStart('.').ToLower();

                    if (!allowedTypes.Contains(fileExtension))
                    {
                        return BadRequest(new { message = $"Invalid file type. Allowed types: {task.Task.RequiredFileTypes}" });
                    }
                }

                // Create uploads directory if it doesn't exist
                var uploadsPath = Path.Combine(_environment.ContentRootPath, "uploads", "onboarding", employeeId.ToString());
                Directory.CreateDirectory(uploadsPath);

                // Generate unique filename
                var fileName = $"{taskId}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Update task
                task.DocumentPath = Path.Combine("uploads", "onboarding", employeeId.ToString(), fileName);
                task.DocumentOriginalName = file.FileName;
                task.Status = "Completed";
                task.CompletedDate = DateTime.UtcNow;
                task.CompletedBy = userId;
                task.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Check if all required tasks are completed
                await CheckAndActivateAccount(employeeId);

                return Ok(new
                {
                    message = "Document uploaded successfully",
                    fileName = file.FileName,
                    filePath = task.DocumentPath
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error uploading document", error = ex.Message });
            }
        }

        // PUT: api/Onboarding/Task/{taskId}/Verify
        // HR verifies a completed task
        [HttpPut("Task/{taskId}/Verify")]
        [Authorize(Roles = "Admin,Executive,HRManager")]
        public async Task<IActionResult> VerifyTask(int taskId, [FromBody] VerifyTaskRequest request)
        {
            try
            {
                // Get user ID from token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return Unauthorized(new { message = "User ID not found in token" });
                }
                int userId = int.Parse(userIdClaim);

                var task = await _context.EmployeeOnboardingTasks
                    .FirstOrDefaultAsync(t => t.OnboardingTaskId == taskId);

                if (task == null)
                {
                    return NotFound(new { message = "Task not found" });
                }

                if (task.Status != "Completed")
                {
                    return BadRequest(new { message = "Task must be completed before verification" });
                }

                task.IsVerified = request.IsVerified;
                task.VerifiedBy = userId;
                task.VerificationNotes = request.VerificationNotes;
                task.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Task verified successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error verifying task", error = ex.Message });
            }
        }

        // PUT: api/Onboarding/Task/{taskId}/Extend
        // HR extends the due date for a task
        [HttpPut("Task/{taskId}/Extend")]
        [Authorize(Roles = "Admin,Executive,HRManager")]
        public async Task<IActionResult> ExtendDueDate(int taskId, [FromBody] ExtendDueDateRequest request)
        {
            try
            {
                var task = await _context.EmployeeOnboardingTasks
                    .FirstOrDefaultAsync(t => t.OnboardingTaskId == taskId);

                if (task == null)
                {
                    return NotFound(new { message = "Task not found" });
                }

                task.DueDate = request.NewDueDate;
                task.Notes = request.Notes;
                task.UpdatedAt = DateTime.UtcNow;

                // Update status if it was overdue
                if (task.Status == "Overdue" && request.NewDueDate >= DateTime.UtcNow.Date)
                {
                    task.Status = "Pending";
                }

                await _context.SaveChangesAsync();

                return Ok(new { message = "Due date extended successfully", newDueDate = request.NewDueDate });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error extending due date", error = ex.Message });
            }
        }

        // POST: api/Onboarding/Task/Assign
        // HR manually assigns a new task to an employee
        [HttpPost("Task/Assign")]
        [Authorize(Roles = "Admin,Executive,HRManager")]
        public async Task<IActionResult> AssignTask([FromBody] AssignTaskRequest request)
        {
            try
            {
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.EmployeeId == request.EmployeeId);

                if (employee == null)
                {
                    return NotFound(new { message = "Employee not found" });
                }

                var taskTemplate = await _context.OnboardingTasks
                    .FirstOrDefaultAsync(t => t.TaskId == request.TaskId);

                if (taskTemplate == null)
                {
                    return NotFound(new { message = "Task template not found" });
                }

                // Check if task already assigned
                var existingTask = await _context.EmployeeOnboardingTasks
                    .AnyAsync(t => t.EmployeeId == request.EmployeeId && t.TaskId == request.TaskId);

                if (existingTask)
                {
                    return BadRequest(new { message = "Task already assigned to this employee" });
                }

                var employeeTask = new EmployeeOnboardingTask
                {
                    EmployeeId = request.EmployeeId,
                    TaskId = request.TaskId,
                    AssignedDate = DateTime.UtcNow,
                    DueDate = request.DueDate ?? DateTime.UtcNow.AddDays(taskTemplate.DefaultDueDays),
                    Status = "Pending",
                    Notes = request.Notes,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.EmployeeOnboardingTasks.Add(employeeTask);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Task assigned successfully", onboardingTaskId = employeeTask.OnboardingTaskId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error assigning task", error = ex.Message });
            }
        }

        // DELETE: api/Onboarding/Task/{taskId}
        // HR removes a task from employee's onboarding
        [HttpDelete("Task/{taskId}")]
        [Authorize(Roles = "Admin,Executive,HRManager")]
        public async Task<IActionResult> RemoveTask(int taskId)
        {
            try
            {
                var task = await _context.EmployeeOnboardingTasks
                    .FirstOrDefaultAsync(t => t.OnboardingTaskId == taskId);

                if (task == null)
                {
                    return NotFound(new { message = "Task not found" });
                }

                _context.EmployeeOnboardingTasks.Remove(task);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Task removed successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error removing task", error = ex.Message });
            }
        }

        // GET: api/Onboarding/Progress/{employeeId}
        // Get onboarding progress for an employee
        [HttpGet("Progress/{employeeId}")]
        [Authorize(Roles = "Admin,Executive,HRManager")]
        public async Task<IActionResult> GetProgress(int employeeId)
        {
            try
            {
                var tasks = await _context.EmployeeOnboardingTasks
                    .Include(t => t.Task)
                    .Where(t => t.EmployeeId == employeeId)
                    .ToListAsync();

                var totalTasks = tasks.Count;
                var completedTasks = tasks.Count(t => t.Status == "Completed");
                var pendingTasks = tasks.Count(t => t.Status == "Pending");
                var overdueTasks = tasks.Count(t => t.Status != "Completed" && t.DueDate < DateTime.UtcNow.Date);
                var progressPercentage = totalTasks > 0 ? Math.Round((double)completedTasks / totalTasks * 100, 2) : 0;

                var tasksByCategory = tasks
                    .GroupBy(t => t.Task.TaskCategory)
                    .Select(g => new
                    {
                        Category = g.Key,
                        Total = g.Count(),
                        Completed = g.Count(t => t.Status == "Completed"),
                        Pending = g.Count(t => t.Status == "Pending")
                    })
                    .ToList();

                return Ok(new
                {
                    totalTasks,
                    completedTasks,
                    pendingTasks,
                    overdueTasks,
                    progressPercentage,
                    tasksByCategory
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving progress", error = ex.Message });
            }
        }

        // GET: api/Onboarding/Statistics
        // Get overall onboarding statistics (HR Dashboard)
        [HttpGet("Statistics")]
        [Authorize(Roles = "Admin,Executive,HRManager")]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                var employeesInOnboarding = await _context.Users
                    .Where(u => u.AccountStatus == "PendingOnboarding")
                    .CountAsync();

                var completedOnboardings = await _context.Users
                    .Where(u => u.OnboardingStatus == "Completed" && u.OnboardingCompletedDate.HasValue)
                    .CountAsync();

                var averageCompletionDays = await _context.Users
                    .Where(u => u.OnboardingStatus == "Completed" &&
                                u.OnboardingCompletedDate.HasValue &&
                                u.Employee != null &&
                                u.Employee.HireDate.HasValue)
                    .Select(u => (u.OnboardingCompletedDate.Value - u.Employee.HireDate.Value).Days)
                    .AverageAsync();

                return Ok(new
                {
                    employeesInOnboarding,
                    completedOnboardings,
                    averageCompletionDays = Math.Round(averageCompletionDays, 1)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving statistics", error = ex.Message });
            }
        }

        // Helper method to check if all required tasks are complete and activate account
        private async Task CheckAndActivateAccount(int employeeId)
        {
            var allRequiredTasksCompleted = await _context.EmployeeOnboardingTasks
                .Include(t => t.Task)
                .Where(t => t.EmployeeId == employeeId && t.Task.IsRequired)
                .AllAsync(t => t.Status == "Completed");

            if (allRequiredTasksCompleted)
            {
                // Find the employee first
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

                if (employee == null || !employee.UserId.HasValue)
                {
                    return;
                }

                // Find the user by UserId (not by EmployeeId which doesn't exist)
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserId == employee.UserId.Value);

                if (user != null && user.AccountStatus == "PendingOnboarding")
                {
                    user.AccountStatus = "Active";
                    user.OnboardingStatus = "Completed";
                    user.OnboardingCompletedDate = DateTime.UtcNow;

                    // Update employee status
                    employee.EmploymentStatus = "Active";
                    employee.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    // TODO: Send congratulations/activation email
                }
            }
        }

        public class CompleteTaskRequest
        {
            public string? SubmittedData { get; set; }
            public string? Notes { get; set; }
        }

        public class VerifyTaskRequest
        {
            public bool IsVerified { get; set; }
            public string? VerificationNotes { get; set; }
        }

        public class ExtendDueDateRequest
        {
            public DateTime NewDueDate { get; set; }
            public string? Notes { get; set; }
        }

        public class AssignTaskRequest
        {
            public int EmployeeId { get; set; }
            public int TaskId { get; set; }
            public DateTime? DueDate { get; set; }
            public string? Notes { get; set; }
        }
    }
}