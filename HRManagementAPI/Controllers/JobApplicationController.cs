// HRManagementAPI/Controllers/JobApplicationController.cs
using HRManagementAPI.Data;
using HRManagementAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HRManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JobApplicationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public JobApplicationController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // POST: api/JobApplication/Submit
        // SIMPLIFIED VERSION - No file uploads to avoid Swagger error
        [HttpPost("Submit")]
        public async Task<IActionResult> SubmitApplication([FromBody] JobApplicationDto application)
        {
            try
            {
                var newApplication = new JobApplication
                {
                    // Personal Information
                    ApplicationDate = application.ApplicationDate ?? DateTime.UtcNow,
                    FirstName = application.FirstName,
                    LastName = application.LastName,
                    MiddleName = application.MiddleName,
                    DateOfBirth = application.DateOfBirth,
                    Gender = application.Gender,
                    PhoneNumber = application.PhoneNumber,
                    CellNumber = application.CellNumber,
                    Email = application.Email,
                    Address = application.Address,
                    AptNumber = application.AptNumber,
                    City = application.City,
                    State = application.State,
                    ZipCode = application.ZipCode,
                    Country = application.Country ?? "USA",
                    SocialSecurityNumber = application.SocialSecurityNumber,
                    DriversLicenseNumber = application.DriversLicenseNumber,
                    DriversLicenseState = application.DriversLicenseState,

                    // Emergency Contact
                    EmergencyContactPerson = application.EmergencyContactPerson,
                    EmergencyContactRelationship = application.EmergencyContactRelationship,
                    EmergencyContactAddress = application.EmergencyContactAddress,
                    EmergencyContactPhone = application.EmergencyContactPhone,

                    // Position Details
                    PositionAppliedFor = application.Position1,
                    Position2 = application.Position2,
                    DesiredSalary = application.SalaryDesired,
                    SalaryType = application.SalaryType,
                    ExpectedStartDate = application.AvailableStartDate,
                    EmploymentSought = application.EmploymentSought,
                    DesiredLocations = application.DesiredLocations,
                    DesiredLocationOther = application.DesiredLocationOther,
                    ShiftPreferences = application.ShiftPreferences,
                    DaysAvailable = application.DaysAvailable,

                    // Background Questions
                    PreviouslyAppliedToTPA = application.PreviouslyAppliedToTPA,
                    PreviouslyAppliedWhen = application.PreviouslyAppliedWhen,
                    PreviouslyWorkedForTPA = application.PreviouslyWorkedForTPA,
                    PreviouslyWorkedWhen = application.PreviouslyWorkedWhen,
                    FamilyMembersAtTPA = application.FamilyMembersAtTPA,
                    FamilyMembersWho = application.FamilyMembersWho,
                    USCitizenOrResident = application.USCitizenOrResident,
                    AlienNumber = application.AlienNumber,
                    LegallyEntitledToWork = application.LegallyEntitledToWork,
                    EighteenOrOlder = application.EighteenOrOlder,
                    ServedInArmedForces = application.ServedInArmedForces,
                    ConvictedOfCrime = application.ConvictedOfCrime,
                    CrimeDetails = application.CrimeDetails,
                    NameOnAbuseRegistry = application.NameOnAbuseRegistry,
                    FoundGuiltyOfAbuse = application.FoundGuiltyOfAbuse,
                    HealthcareLicenseIssues = application.HealthcareLicenseIssues,

                    // Education
                    EducationHistory = application.EducationHistory,
                    SpecialSkillsKnowledge = application.SpecialSkillsKnowledge,
                    TypingSpeedWPM = application.TypingSpeedWPM,

                    // Licenses
                    LicensesAndCertifications = application.LicensesAndCertifications,
                    DIDDTrainingClasses = application.DIDDTrainingClasses,

                    // References
                    References = application.References,

                    // Employment History
                    EmploymentHistory = application.EmploymentHistory,

                    // Authorizations
                    BackgroundCheckConsent = application.BackgroundCheckConsent,
                    BackgroundCheckDate = application.BackgroundCheckDate,
                    BackgroundCheckSignature = application.BackgroundCheckSignature,
                    NYApplicant = application.NYApplicant,
                    MNOKApplicant = application.MNOKApplicant,
                    CAApplicant = application.CAApplicant,
                    ReferenceCheckConsent = application.ReferenceCheckConsent,
                    ReferenceCheckSSNLast4 = application.ReferenceCheckSSNLast4,
                    ReferenceCheckDate = application.ReferenceCheckDate,
                    ReferenceCheckSignature = application.ReferenceCheckSignature,
                    HasNoAbuseCaseAgainstMe = application.HasNoAbuseCaseAgainstMe,
                    HasAbuseCaseAgainstMe = application.HasAbuseCaseAgainstMe,
                    DIDDAuthorizationConsent = application.DIDDAuthorizationConsent,
                    DIDDFullName = application.DIDDFullName,
                    DIDDSSN = application.DIDDSSN,
                    DIDDOB = application.DIDDOB,
                    DIDDDriverLicense = application.DIDDDriverLicense,
                    DIDDWitnessName = application.DIDDWitnessName,
                    ProtectionNoAbuseCase = application.ProtectionNoAbuseCase,
                    ProtectionHasAbuseCase = application.ProtectionHasAbuseCase,
                    ProtectionAuthorizationConsent = application.ProtectionAuthorizationConsent,

                    // Status and timestamps
                    Status = "Submitted",
                    SubmittedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.JobApplications.Add(newApplication);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Application submitted successfully",
                    applicationId = newApplication.ApplicationId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Error submitting application",
                    error = ex.Message,
                    innerError = ex.InnerException?.Message
                });
            }
        }

        // GET: api/JobApplication
        [HttpGet]
        public async Task<IActionResult> GetAllApplications()
        {
            try
            {
                var applications = await _context.JobApplications
                    .OrderByDescending(a => a.SubmittedAt)
                    .Select(a => new
                    {
                        a.ApplicationId,
                        a.FirstName,
                        a.LastName,
                        FullName = $"{a.FirstName} {a.LastName}",
                        a.Email,
                        a.PhoneNumber,
                        a.PositionAppliedFor,
                        a.Status,
                        a.SubmittedAt,
                        a.ApplicationDate
                    })
                    .ToListAsync();

                return Ok(applications);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving applications", error = ex.Message });
            }
        }

        // GET: api/JobApplication/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetApplication(int id)
        {
            try
            {
                var application = await _context.JobApplications
                    .FirstOrDefaultAsync(a => a.ApplicationId == id);

                if (application == null)
                {
                    return NotFound(new { message = "Application not found" });
                }

                return Ok(application);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving application", error = ex.Message });
            }
        }

       
        [HttpGet("All")]
        [Authorize(Roles = "Admin,Executive,HRManager")]
        public async Task<IActionResult> GetAllApplications(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null,
            [FromQuery] string? approvalStatus = null,
            [FromQuery] string? searchTerm = null)
        {
            var query = _context.JobApplications.AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(a => a.Status == status);

            if (!string.IsNullOrEmpty(approvalStatus))
                query = query.Where(a => a.ApprovalStatus == approvalStatus);

            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(a =>
                    a.FirstName.Contains(searchTerm) ||
                    a.LastName.Contains(searchTerm) ||
                    a.CellPhone.Contains(searchTerm) ||
                    a.ApplicationNumber.Contains(searchTerm));

            var totalCount = await query.CountAsync();

            var applications = await query
                .OrderByDescending(a => a.SubmissionDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new {
                    a.ApplicationId,
                    a.ApplicationNumber,
                    a.FirstName,
                    a.MiddleName,
                    a.LastName,
                    a.Position1,
                    a.Position2,
                    a.CellPhone,
                    a.HomePhone,
                    a.City,
                    a.State,
                    a.SubmissionDate,
                    a.Status,
                    a.ApprovalStatus,
                    a.ReviewedBy,
                    a.ReviewedDate
                })
                .ToListAsync();

            return Ok(new
            {
                applications,
                totalCount,
                pageNumber,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        // 2. GET STATISTICS
        [HttpGet("Statistics")]
        [Authorize(Roles = "Admin,Executive,HRManager")]
        public async Task<IActionResult> GetStatistics()
        {
            var totalApplications = await _context.JobApplications.CountAsync();
            var pendingApplications = await _context.JobApplications
                .Where(a => a.ApprovalStatus == "Pending")
                .CountAsync();
            var approvedApplications = await _context.JobApplications
                .Where(a => a.ApprovalStatus == "Approved")
                .CountAsync();

            return Ok(new
            {
                totalApplications,
                pendingApplications,
                approvedApplications
            });
        }

        // 3. UPDATE STATUS
        [HttpPut("{id}/Status")]
        [Authorize(Roles = "Admin,Executive,HRManager")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] StatusUpdateRequest request)
        {
            var application = await _context.JobApplications.FindAsync(id);
            if (application == null) return NotFound();

            application.Status = request.Status;
            application.LastModified = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(request.Notes))
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                application.ReviewedBy = userId;
                application.ReviewedDate = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Status updated" });
        }

        

        // 5. ADD REVIEW NOTES
        [HttpPost("{id}/Notes")]
        [Authorize(Roles = "Admin,Executive,HRManager")]
        public async Task<IActionResult> AddApplicationReviewNotes(int id, [FromBody] NotesRequest request)
        {
            var application = await _context.JobApplications.FindAsync(id);
            if (application == null) return NotFound();

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var newNote = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm}] {request.Notes}";
            application.ReviewNotes = string.IsNullOrEmpty(application.ReviewNotes)
                ? newNote
                : application.ReviewNotes + "\n\n" + newNote;

            application.ReviewedBy = userId;
            application.ReviewedDate = DateTime.UtcNow;
            application.LastModified = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Notes added" });
        }
        // 1. REJECT APPLICATION ENDPOINT
        [HttpPost("{id}/Reject")]
        [Authorize(Roles = "Admin,Executive,HRManager")]
        public async Task<IActionResult> RejectApplication(int id, [FromBody] RejectRequest request)
        {
            try
            {
                var application = await _context.JobApplications.FindAsync(id);
                if (application == null)
                    return NotFound(new { message = "Application not found" });

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                application.ApprovalStatus = "Rejected";
                application.Status = "Rejected";
                application.RejectionReason = request.RejectionReason;
                application.ReviewedBy = userId;
                application.ReviewedDate = DateTime.UtcNow;
                application.LastModified = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Application rejected successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error rejecting application", error = ex.Message });
            }
        }

      

        // 3. UPDATE APPROVAL STATUS ENDPOINT
        [HttpPut("{id}/Approval")]
        [Authorize(Roles = "Admin,Executive,HRManager")]
        public async Task<IActionResult> UpdateApprovalStatus(int id, [FromBody] ApprovalUpdateRequest request)
        {
            try
            {
                var application = await _context.JobApplications.FindAsync(id);
                if (application == null)
                    return NotFound(new { message = "Application not found" });

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                application.ApprovalStatus = request.ApprovalStatus;
                application.ReviewedBy = userId;
                application.ReviewedDate = DateTime.UtcNow;
                application.LastModified = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(request.Notes))
                {
                    var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
                    var newNote = $"[{timestamp}] {request.Notes}";
                    application.ReviewNotes = string.IsNullOrEmpty(application.ReviewNotes)
                        ? newNote
                        : application.ReviewNotes + "\n\n" + newNote;
                }

                if (request.ApprovalStatus == "Approved")
                    application.Status = "Approved";
                else if (request.ApprovalStatus == "Rejected")
                    application.Status = "Rejected";

                await _context.SaveChangesAsync();

                return Ok(new { message = "Approval status updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating approval status", error = ex.Message });
            }
        }

        // GET: api/JobApplication/DownloadPDF/{id}
        [HttpGet("DownloadPDF/{id}")]
        public async Task<IActionResult> DownloadApplicationPDF(int id)
        {
            try
            {
                var application = await _context.JobApplications
                    .FirstOrDefaultAsync(a => a.ApplicationId == id);

                if (application == null)
                {
                    return NotFound(new { message = "Application not found" });
                }

                // TODO: Implement PDF generation
                // For now, return a simple text file
                var content = $"Application #{application.ApplicationId}\n" +
                             $"Name: {application.FirstName} {application.LastName}\n" +
                             $"Email: {application.Email}\n" +
                             $"Position: {application.PositionAppliedFor}\n" +
                             $"Status: {application.Status}\n" +
                             $"Submitted: {application.SubmittedAt}";

                var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                return File(bytes, "text/plain", $"Application_{application.ApplicationId}.txt");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error generating PDF", error = ex.Message });
            }
        }

        // POST: api/JobApplication/{id}/Hire
        // HR hires a candidate - creates User, Employee, initializes onboarding, and queues welcome email
        [HttpPost("{id}/Hire")]
        [Authorize(Roles = "Admin,Executive,HRManager")]
        public async Task<IActionResult> HireCandidate(int id, [FromBody] HireRequest request)
        {
            try
            {
                // Get the job application
                var application = await _context.JobApplications.FindAsync(id);
                if (application == null)
                {
                    return NotFound(new { message = "Application not found" });
                }

                // Check if already hired
                if (application.Status == "Hired" || application.ApprovalStatus == "Hired")
                {
                    return BadRequest(new { message = "Candidate has already been hired" });
                }

                // Validate email uniqueness
                if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                {
                    return BadRequest(new { message = "Email already exists in the system" });
                }

                // Validate employee code uniqueness
                if (await _context.Employees.AnyAsync(e => e.EmployeeCode == request.EmployeeCode))
                {
                    return BadRequest(new { message = "Employee code already exists" });
                }

                // Validate department
                var department = await _context.Departments.FindAsync(request.DepartmentId);
                if (department == null)
                {
                    return BadRequest(new { message = "Invalid department" });
                }

                // Validate role
                var role = await _context.Roles.FindAsync(request.RoleId);
                if (role == null)
                {
                    return BadRequest(new { message = "Invalid role" });
                }

                // Generate temporary password
                var temporaryPassword = HRManagementAPI.Utilities.PasswordGenerator.GenerateTpaPassword();
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(temporaryPassword);

                // Create User account
                var user = new User
                {
                    Email = request.Email,
                    PasswordHash = hashedPassword,
                    RoleId = request.RoleId,
                    IsActive = true,
                    AccountStatus = "PendingOnboarding",
                    OnboardingStatus = "NotStarted",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync(); // Save to get UserId

                // Create Employee record
                var employee = new Employee
                {
                    UserId = user.UserId,
                    FirstName = application.FirstName,
                    LastName = application.LastName,
                    MiddleName = application.MiddleName,
                    DateOfBirth = application.DateOfBirth,
                    Gender = application.Gender,
                    PhoneNumber = application.PhoneNumber,
                    PersonalEmail = application.Email,
                    Address = application.Address,
                    City = application.City,
                    State = application.State,
                    ZipCode = application.ZipCode,
                    Country = application.Country,
                    EmergencyContactName = application.EmergencyContactPerson,
                    EmergencyContactPhone = application.EmergencyContactPhone,
                    EmergencyContactRelationship = application.EmergencyContactRelationship,
                    EmployeeCode = request.EmployeeCode,
                    DepartmentId = request.DepartmentId,
                    JobTitle = request.JobTitle,
                    EmployeeType = request.EmployeeType,
                    EmploymentStatus = "Pending", // Will become "Active" after onboarding
                    EmploymentType = request.EmploymentType ?? "Full-Time",
                    HireDate = request.HireDate,
                    Salary = request.Salary,
                    ManagerId = request.ManagerId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Set PTO eligibility based on employee type
                if (request.EmployeeType == "AdminStaff")
                {
                    employee.IsEligibleForPTO = true;
                    employee.PTOBalance = 0;
                    employee.IsEligibleForInsurance = true;
                }

                _context.Employees.Add(employee);
                await _context.SaveChangesAsync(); // Save to get EmployeeId

                // Initialize onboarding tasks
                var tasks = await _context.OnboardingTasks
                    .Where(t => t.IsActive &&
                        (t.ApplicableEmployeeType == "Both" || t.ApplicableEmployeeType == employee.EmployeeType))
                    .ToListAsync();

                foreach (var task in tasks)
                {
                    var employeeTask = new EmployeeOnboardingTask
                    {
                        EmployeeId = employee.EmployeeId,
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

                // Queue welcome email
                var emailBody = $@"
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #f59e42; color: white; padding: 20px; text-align: center; }}
        .content {{ background-color: #f9f9f9; padding: 20px; margin-top: 20px; }}
        .credentials {{ background-color: #fff; padding: 15px; border-left: 4px solid #f59e42; margin: 20px 0; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #f59e42; color: white; text-decoration: none; border-radius: 4px; margin: 20px 0; }}
        .footer {{ margin-top: 30px; padding-top: 20px; border-top: 1px solid #ddd; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Welcome to TPA!</h1>
        </div>
        <div class='content'>
            <h2>Hi {application.FirstName},</h2>
            <p>Congratulations! We're excited to have you join the TPA team as a <strong>{request.JobTitle}</strong> in the <strong>{department.DepartmentName}</strong> department.</p>

            <p>Your account has been created. Here are your login credentials:</p>

            <div class='credentials'>
                <p><strong>Username:</strong> {request.Email}</p>
                <p><strong>Temporary Password:</strong> {temporaryPassword}</p>
                <p><strong>Start Date:</strong> {request.HireDate:MMMM dd, yyyy}</p>
            </div>

            <p><strong>⚠️ IMPORTANT:</strong> Please change your password after your first login for security.</p>

            <h3>Next Steps:</h3>
            <ol>
                <li>Log in using the credentials above</li>
                <li>Complete your onboarding tasks (estimated time: 30-45 minutes)</li>
                <li>Once all required tasks are completed, you'll gain full access to the system</li>
            </ol>

            <p>You have been assigned <strong>{tasks.Count} onboarding tasks</strong> to complete. These include uploading documents, completing forms, and acknowledging company policies.</p>

            <a href='http://localhost:3000/login' class='button'>Login Now</a>

            <div class='footer'>
                <p>If you have any questions, please contact HR at hr@tpa.com</p>
                <p>Welcome aboard!</p>
                <p><strong>The TPA HR Team</strong></p>
            </div>
        </div>
    </div>
</body>
</html>";

                var email = new EmailQueue
                {
                    ToEmail = request.Email,
                    ToName = $"{application.FirstName} {application.LastName}",
                    FromEmail = "hr@tpa.com",
                    FromName = "TPA HR Team",
                    Subject = "Welcome to TPA - Your Account Details",
                    Body = emailBody,
                    EmailType = "WelcomeEmail",
                    Status = "Pending",
                    Priority = 1, // High priority
                    CreatedAt = DateTime.UtcNow
                };

                _context.EmailQueue.Add(email);

                // Update application status
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                application.Status = "Hired";
                application.ApprovalStatus = "Hired";
                application.ReviewedBy = userId;
                application.ReviewedDate = DateTime.UtcNow;
                application.LastModified = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Candidate hired successfully",
                    userId = user.UserId,
                    employeeId = employee.EmployeeId,
                    employeeCode = employee.EmployeeCode,
                    email = user.Email,
                    temporaryPassword = temporaryPassword, // Return for HR to communicate if needed
                    onboardingTasksCreated = tasks.Count,
                    welcomeEmailQueued = true
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error hiring candidate", error = ex.Message });
            }
        }
    }

    // DTO Classes

    public class RejectRequest
    {
        public string RejectionReason { get; set; }
    }

    public class NotesRequest
    {
        public string Notes { get; set; }
    }

    public class ApprovalUpdateRequest
    {
        public string ApprovalStatus { get; set; }
        public string? Notes { get; set; }
    }

    public class HireRequest
    {
        public string Email { get; set; }
        public string EmployeeCode { get; set; }
        public int DepartmentId { get; set; }
        public string JobTitle { get; set; }
        public string EmployeeType { get; set; } // AdminStaff or FieldStaff
        public string? EmploymentType { get; set; } // Full-Time, Part-Time, Contract
        public int RoleId { get; set; }
        public DateTime HireDate { get; set; }
        public decimal? Salary { get; set; }
        public int? ManagerId { get; set; }
    }
    public class JobApplicationDto
    {
        // Personal Information
        public DateTime? ApplicationDate { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string? MiddleName { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string PhoneNumber { get; set; }
        public string? CellNumber { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string? AptNumber { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string ZipCode { get; set; }
        public string? Country { get; set; }
        public string SocialSecurityNumber { get; set; }
        public string DriversLicenseNumber { get; set; }
        public string DriversLicenseState { get; set; }
        public string EmergencyContactPerson { get; set; }
        public string EmergencyContactRelationship { get; set; }
        public string? EmergencyContactAddress { get; set; }
        public string EmergencyContactPhone { get; set; }

        // Position Details
        public string Position1 { get; set; }
        public string? Position2 { get; set; }
        public decimal? SalaryDesired { get; set; }
        public string? SalaryType { get; set; }
        public DateTime? AvailableStartDate { get; set; }
        public string? EmploymentSought { get; set; }
        public string? DesiredLocations { get; set; }
        public string? DesiredLocationOther { get; set; }
        public string? ShiftPreferences { get; set; }
        public string? DaysAvailable { get; set; }

        // Background Questions
        public bool? PreviouslyAppliedToTPA { get; set; }
        public string? PreviouslyAppliedWhen { get; set; }
        public bool? PreviouslyWorkedForTPA { get; set; }
        public string? PreviouslyWorkedWhen { get; set; }
        public bool? FamilyMembersAtTPA { get; set; }
        public string? FamilyMembersWho { get; set; }
        public bool? USCitizenOrResident { get; set; }
        public string? AlienNumber { get; set; }
        public bool? LegallyEntitledToWork { get; set; }
        public bool? EighteenOrOlder { get; set; }
        public bool? ServedInArmedForces { get; set; }
        public bool? ConvictedOfCrime { get; set; }
        public string? CrimeDetails { get; set; }
        public bool? NameOnAbuseRegistry { get; set; }
        public bool? FoundGuiltyOfAbuse { get; set; }
        public bool? HealthcareLicenseIssues { get; set; }

        // Education
        public string? EducationHistory { get; set; }
        public string? SpecialSkillsKnowledge { get; set; }
        public int? TypingSpeedWPM { get; set; }

        // Licenses
        public string? LicensesAndCertifications { get; set; }
        public string? DIDDTrainingClasses { get; set; }

        // References
        public string? References { get; set; }

        // Employment History
        public string? EmploymentHistory { get; set; }

        // Authorizations
        public bool BackgroundCheckConsent { get; set; }
        public DateTime? BackgroundCheckDate { get; set; }
        public string? BackgroundCheckSignature { get; set; }
        public bool? NYApplicant { get; set; }
        public bool? MNOKApplicant { get; set; }
        public bool? CAApplicant { get; set; }
        public bool ReferenceCheckConsent { get; set; }
        public string? ReferenceCheckSSNLast4 { get; set; }
        public DateTime? ReferenceCheckDate { get; set; }
        public string? ReferenceCheckSignature { get; set; }
        public bool? HasNoAbuseCaseAgainstMe { get; set; }
        public bool? HasAbuseCaseAgainstMe { get; set; }
        public bool DIDDAuthorizationConsent { get; set; }
        public string? DIDDFullName { get; set; }
        public string? DIDDSSN { get; set; }
        public DateTime? DIDDOB { get; set; }
        public string? DIDDDriverLicense { get; set; }
        public string? DIDDWitnessName { get; set; }
        public bool? ProtectionNoAbuseCase { get; set; }
        public bool? ProtectionHasAbuseCase { get; set; }
        public bool ProtectionAuthorizationConsent { get; set; }
    }
    public class StatusUpdateRequest
    {
        public string Status { get; set; }
        public string? Notes { get; set; }
    }
    public class UpdateStatusDto
    {
        public string Status { get; set; }
        public int? ReviewedBy { get; set; }
        public string? Notes { get; set; }
    }
}