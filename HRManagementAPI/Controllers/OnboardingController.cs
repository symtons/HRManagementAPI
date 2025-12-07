using HRManagementAPI.Data;
using HRManagementAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        // ========================================
        // EMPLOYEE ENDPOINTS
        // ========================================

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

                // Get all tasks for this employee
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
                        InstructionText = t.Task.InstructionText,
                        RequiredFileTypes = t.Task.RequiredFileTypes,
                        t.AssignedDate,
                        t.DueDate,
                        t.CompletedDate,
                        t.Status,
                        t.SubmittedData,
                        t.DocumentPath,
                        t.DocumentOriginalName,
                        t.Notes,
                        IsOverdue = t.Status != "Completed" && t.DueDate < DateTime.UtcNow.Date
                    })
                    .ToListAsync();

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

        // PUT: api/Onboarding/Task/{taskId}/Complete
        // Employee completes a task (text submission or acknowledgment)
        [HttpPut("Task/{taskId}/Complete")]
        public async Task<IActionResult> CompleteTask(int taskId, [FromBody] CompleteTaskRequest request)
        {
            try
            {
                // Get user ID and employee ID from token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var employeeIdClaim = User.FindFirst("EmployeeId")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(employeeIdClaim))
                {
                    return Unauthorized(new { message = "User or Employee ID not found in token" });
                }

                int userId = int.Parse(userIdClaim);
                int employeeId = int.Parse(employeeIdClaim);

                // Get the task
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

                // ✅ NEW: Check if all required tasks are completed and update onboarding status
                await UpdateOnboardingStatus(employeeId);

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
        public async Task<IActionResult> UploadTaskDocument(int taskId, IFormFile file)
        {
            try
            {
                // Get employee ID from token
                var employeeIdClaim = User.FindFirst("EmployeeId")?.Value;
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(employeeIdClaim) || string.IsNullOrEmpty(userIdClaim))
                {
                    return Unauthorized(new { message = "Employee or User ID not found in token" });
                }

                int employeeId = int.Parse(employeeIdClaim);
                int userId = int.Parse(userIdClaim);

                // Validate file
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { message = "No file uploaded" });
                }

                // Get the task
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

                // Validate file type
                var fileExtension = Path.GetExtension(file.FileName).ToLower().TrimStart('.');
                var allowedTypes = task.Task.RequiredFileTypes?.Split(',').Select(t => t.Trim().ToLower()).ToList();

                if (allowedTypes != null && allowedTypes.Any() && !allowedTypes.Contains(fileExtension))
                {
                    return BadRequest(new { message = $"Invalid file type. Allowed types: {task.Task.RequiredFileTypes}" });
                }

                // Validate file size (10MB max)
                if (file.Length > 10 * 1024 * 1024)
                {
                    return BadRequest(new { message = "File size exceeds 10MB limit" });
                }

                // Create uploads directory if it doesn't exist
                var uploadsPath = Path.Combine(_environment.WebRootPath ?? _environment.ContentRootPath, "uploads", "onboarding");
                if (!Directory.Exists(uploadsPath))
                {
                    Directory.CreateDirectory(uploadsPath);
                }

                // Generate unique filename
                var fileName = $"{employeeId}_{taskId}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Update task
                task.DocumentPath = $"/uploads/onboarding/{fileName}";
                task.DocumentOriginalName = file.FileName;
                task.Status = "Completed";
                task.CompletedDate = DateTime.UtcNow;
                task.CompletedBy = userId;
                task.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // ✅ NEW: Check if all required tasks are completed
                await UpdateOnboardingStatus(employeeId);

                return Ok(new
                {
                    message = "Document uploaded successfully",
                    documentPath = task.DocumentPath,
                    documentName = task.DocumentOriginalName
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error uploading document", error = ex.Message });
            }
        }

        // ========================================
        // HR ENDPOINTS
        // ========================================

        // POST: api/Onboarding/Initialize/{employeeId}
        // HR manually initializes onboarding tasks for an employee
        [HttpPost("Initialize/{employeeId}")]
        [Authorize(Roles = "Admin,Executive,Director")]
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
                        t.IsRequired &&  // Only mandatory tasks
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

        // GET: api/Onboarding/Monitor
        // HR gets all employees currently in onboarding
        [HttpGet("Monitor")]
        [Authorize(Roles = "Admin,Executive,Director")]
        public async Task<IActionResult> GetOnboardingMonitor()
        {
            try
            {
                // Get all employees with onboarding status NotStarted or InProgress
                var employees = await _context.Employees
                    .Include(e => e.Department)
                    .Include(e => e.User)
                    .Where(e => e.User != null &&
                        (e.User.OnboardingStatus == "NotStarted" || e.User.OnboardingStatus == "InProgress"))
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

        // GET: api/Onboarding/Employee/{employeeId}
        // HR gets onboarding tasks for specific employee
        [HttpGet("Employee/{employeeId}")]
        [Authorize(Roles = "Admin,Executive,Director")]
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
                        t.VerificationNotes
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

        // POST: api/Onboarding/Task/Assign
        // HR assigns a task to an employee
        [HttpPost("Task/Assign")]
        [Authorize(Roles = "Admin,Executive,Director")]
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
                    .FirstOrDefaultAsync(t => t.EmployeeId == request.EmployeeId && t.TaskId == request.TaskId);

                if (existingTask != null)
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
        [Authorize(Roles = "Admin,Executive,Director")]
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

        // ========================================
        // ✅ NEW: HELPER METHOD - Update Onboarding Status
        // ========================================
        private async Task UpdateOnboardingStatus(int employeeId)
        {
            try
            {
                // Get employee and user
                var employee = await _context.Employees
                    .Include(e => e.User)
                    .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

                if (employee == null || employee.User == null)
                    return;

                // Get all tasks for this employee
                var allTasks = await _context.EmployeeOnboardingTasks
                    .Include(t => t.Task)
                    .Where(t => t.EmployeeId == employeeId)
                    .ToListAsync();

                var requiredTasks = allTasks.Where(t => t.Task.IsRequired).ToList();
                var completedRequiredTasks = requiredTasks.Where(t => t.Status == "Completed").ToList();

                // Update status based on progress
                if (completedRequiredTasks.Count == 0)
                {
                    // No tasks completed yet
                    if (employee.User.OnboardingStatus != "NotStarted")
                    {
                        employee.User.OnboardingStatus = "NotStarted";
                        await _context.SaveChangesAsync();
                    }
                }
                else if (completedRequiredTasks.Count < requiredTasks.Count)
                {
                    // Some tasks completed, but not all
                    if (employee.User.OnboardingStatus != "InProgress")
                    {
                        employee.User.OnboardingStatus = "InProgress";
                        await _context.SaveChangesAsync();
                    }
                }
                else if (completedRequiredTasks.Count == requiredTasks.Count)
                {
                    // All required tasks completed!
                    if (employee.User.OnboardingStatus != "Completed")
                    {
                        employee.User.OnboardingStatus = "Completed";
                        employee.User.OnboardingCompletedDate = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't throw - status update is not critical
                Console.WriteLine($"Error updating onboarding status: {ex.Message}");
            }
        }
    }

    // ========================================
    // REQUEST MODELS
    // ========================================

    public class CompleteTaskRequest
    {
        public string? SubmittedData { get; set; }
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