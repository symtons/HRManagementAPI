using HRManagementAPI.Data;
using HRManagementAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HRManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JobApplicationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public JobApplicationController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // POST: api/JobApplication/Submit (Public - No Auth)
        [HttpPost("Submit")]
        [AllowAnonymous]
        public async Task<IActionResult> SubmitApplication(
      [FromForm] JobApplicationSubmitRequest request,
      [FromForm] IFormFile? resumeFile,
      [FromForm] IFormFile? coverLetterFile,
      [FromForm] List<IFormFile>? certificationFiles)
        {
            try
            {
                // Generate application number
                var appNumber = GenerateApplicationNumber();

                var application = new JobApplication
                {
                    // System Fields
                    ApplicationNumber = appNumber,
                    IsSubmitted = true,
                    SubmissionDate = DateTime.UtcNow,
                    CreatedDate = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow,
                    Status = "Submitted",
                    ApprovalStatus = "Pending",
                    ApplicationDate = DateTime.UtcNow.ToString("MM/dd/yyyy"),

                    // Personal Information
                    FirstName = request.FirstName,
                    MiddleName = request.MiddleName,
                    LastName = request.LastName,
                    HomeAddress = request.HomeAddress,
                    AptNumber = request.AptNumber,
                    City = request.City,
                    State = request.State,
                    Zip = request.Zip,
                    HomePhone = request.HomePhone,
                    CellPhone = request.CellPhone,
                    SSN = request.SSN,
                    SSNLast4 = request.SSN?.Length >= 4 ? request.SSN.Substring(request.SSN.Length - 4) : null,
                    DriversLicense = request.DriversLicense,
                    DLState = request.DLState,

                    // Emergency Contact
                    EmergencyContactName = request.EmergencyContactName,
                    EmergencyContactRelationship = request.EmergencyContactRelationship,
                    EmergencyContactAddress = request.EmergencyContactAddress,

                    // Position & Availability
                    Position1 = request.Position1,
                    Position2 = request.Position2,
                    SalaryDesired = request.SalaryDesired,
                    AvailableStartDate = request.AvailableStartDate,
                    SalaryType = request.SalaryType,
                    EmploymentType = request.EmploymentType,

                    // Locations
                    NashvilleLocation = request.NashvilleLocation,
                    FranklinLocation = request.FranklinLocation,
                    ShelbyvilleLocation = request.ShelbyvilleLocation,
                    WaynesboroLocation = request.WaynesboroLocation,
                    OtherLocation = request.OtherLocation,

                    // Shifts
                    FirstShift = request.FirstShift,
                    SecondShift = request.SecondShift,
                    ThirdShift = request.ThirdShift,
                    WeekendsOnly = request.WeekendsOnly,

                    // Days Available
                    MondayAvailable = request.MondayAvailable,
                    TuesdayAvailable = request.TuesdayAvailable,
                    WednesdayAvailable = request.WednesdayAvailable,
                    ThursdayAvailable = request.ThursdayAvailable,
                    FridayAvailable = request.FridayAvailable,
                    SaturdayAvailable = request.SaturdayAvailable,
                    SundayAvailable = request.SundayAvailable,

                    // Previous Application/Employment
                    AppliedBefore = request.AppliedBefore,
                    AppliedBeforeWhen = request.AppliedBeforeWhen,
                    WorkedBefore = request.WorkedBefore,
                    WorkedBeforeWhen = request.WorkedBeforeWhen,
                    FamilyEmployed = request.FamilyEmployed,
                    FamilyEmployedWho = request.FamilyEmployedWho,

                    // Legal Status
                    USCitizen = request.USCitizen,
                    AlienNumber = request.AlienNumber,
                    LegallyEntitled = request.LegallyEntitled,
                    Over18 = request.Over18,
                    ArmedForces = request.ArmedForces,
                    ConvictedOfCrime = request.ConvictedOfCrime,
                    AbuseRegistry = request.AbuseRegistry,

                    // Education
                    ElementarySchool = request.ElementarySchool,
                    HighSchool = request.HighSchool,
                    UndergraduateSchool = request.UndergraduateSchool,
                    GraduateSchool = request.GraduateSchool,
                    Elem1 = request.Elem1,
                    Elem2 = request.Elem2,
                    Elem3 = request.Elem3,
                    Elem4 = request.Elem4,
                    Elem5 = request.Elem5,
                    HS9 = request.HS9,
                    HS10 = request.HS10,
                    HS11 = request.HS11,
                    HS12 = request.HS12,
                    UG1 = request.UG1,
                    UG2 = request.UG2,
                    UG3 = request.UG3,
                    UG4 = request.UG4,
                    UG5 = request.UG5,
                    Grad1 = request.Grad1,
                    Grad2 = request.Grad2,
                    Grad3 = request.Grad3,
                    Grad4 = request.Grad4,
                    Grad5 = request.Grad5,
                    ElemDiploma = request.ElemDiploma,
                    HSDiploma = request.HSDiploma,
                    UGDegree = request.UGDegree,
                    GradDegree = request.GradDegree,
                    UGSkills = request.UGSkills,
                    GradSkills = request.GradSkills,
                    SpecialKnowledge = request.SpecialKnowledge,

                    // Licenses
                    LicenseType1 = request.LicenseType1,
                    LicenseState1 = request.LicenseState1,
                    LicenseNumber1 = request.LicenseNumber1,
                    LicenseExpiration1 = request.LicenseExpiration1,
                    LicenseType2 = request.LicenseType2,
                    LicenseState2 = request.LicenseState2,
                    LicenseNumber2 = request.LicenseNumber2,
                    LicenseExpiration2 = request.LicenseExpiration2,

                    // Employment History 1
                    Employer1 = request.Employer1,
                    EmploymentFrom1 = request.EmploymentFrom1,
                    EmploymentTo1 = request.EmploymentTo1,
                    JobTitle1 = request.JobTitle1,
                    Supervisor1 = request.Supervisor1,
                    EmployerAddress1 = request.EmployerAddress1,
                    EmployerCityStateZip1 = request.EmployerCityStateZip1,
                    EmployerPhone1 = request.EmployerPhone1,
                    StartingPay1 = request.StartingPay1,
                    FinalPay1 = request.FinalPay1,
                    WorkPerformed1 = request.WorkPerformed1,
                    ReasonLeaving1 = request.ReasonLeaving1,

                    // Employment History 2
                    Employer2 = request.Employer2,
                    EmploymentFrom2 = request.EmploymentFrom2,
                    EmploymentTo2 = request.EmploymentTo2,
                    JobTitle2 = request.JobTitle2,
                    Supervisor2 = request.Supervisor2,
                    EmployerAddress2 = request.EmployerAddress2,
                    EmployerCityStateZip2 = request.EmployerCityStateZip2,
                    EmployerPhone2 = request.EmployerPhone2,
                    StartingPay2 = request.StartingPay2,
                    FinalPay2 = request.FinalPay2,
                    WorkPerformed2 = request.WorkPerformed2,
                    ReasonLeaving2 = request.ReasonLeaving2,

                    // Employment History 3
                    Employer3 = request.Employer3,
                    EmploymentFrom3 = request.EmploymentFrom3,
                    EmploymentTo3 = request.EmploymentTo3,
                    JobTitle3 = request.JobTitle3,
                    Supervisor3 = request.Supervisor3,
                    EmployerAddress3 = request.EmployerAddress3,
                    EmployerCityStateZip3 = request.EmployerCityStateZip3,
                    EmployerPhone3 = request.EmployerPhone3,
                    StartingPay3 = request.StartingPay3,
                    FinalPay3 = request.FinalPay3,
                    WorkPerformed3 = request.WorkPerformed3,
                    ReasonLeaving3 = request.ReasonLeaving3,

                    // References
                    Reference1Name = request.Reference1Name,
                    Reference1Phone = request.Reference1Phone,
                    Reference1Email = request.Reference1Email,
                    Reference1Years = request.Reference1Years,
                    Reference2Name = request.Reference2Name,
                    Reference2Phone = request.Reference2Phone,
                    Reference2Email = request.Reference2Email,
                    Reference2Years = request.Reference2Years,
                    Reference3Name = request.Reference3Name,
                    Reference3Phone = request.Reference3Phone,
                    Reference3Email = request.Reference3Email,
                    Reference3Years = request.Reference3Years,

                    // Background Check Info
                    BGLastName = request.BGLastName,
                    BGFirstName = request.BGFirstName,
                    BGMiddleName = request.BGMiddleName,
                    BGStreet = request.BGStreet,
                    BGCity = request.BGCity,
                    BGState = request.BGState,
                    BGZipCode = request.BGZipCode,
                    BGSSN = request.BGSSN,
                    BGPhone = request.BGPhone,
                    BGOtherName = request.BGOtherName,
                    BGNameChangeYear = request.BGNameChangeYear,
                    BGDriversLicense = request.BGDriversLicense,
                    BGDLState = request.BGDLState,
                    BGDateOfBirth = request.BGDateOfBirth,
                    BGNameOnLicense = request.BGNameOnLicense,

                    // Acknowledgments
                    ProtectionNoAbuse = request.ProtectionNoAbuse,
                    ProtectionHadAbuse = request.ProtectionHadAbuse,
                    ProtectionWitness = request.ProtectionWitness,
                    ReferenceAuthName = request.ReferenceAuthName,
                    ApplicantSignature = request.ApplicantSignature,
                    SignatureDate = request.SignatureDate,
                    FinalAcknowledgment = request.FinalAcknowledgment
                };

                _context.JobApplications.Add(application);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Application submitted successfully",
                    applicationId = application.ApplicationId,
                    applicationNumber = appNumber,
                    submittedDate = application.SubmissionDate
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error submitting application", error = ex.Message });
            }
        }

        // GET: api/JobApplication/All (HR/Admin only)
        [HttpGet("All")]
        [Authorize(Roles = "Admin,Executive,HRManager")]
        public async Task<IActionResult> GetAllApplications(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null,
            [FromQuery] string? approvalStatus = null,
            [FromQuery] string? search = null)
        {
            try
            {
                var query = _context.JobApplications.AsQueryable();

                // Filter by status
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(a => a.Status == status);
                }

                // Filter by approval status
                if (!string.IsNullOrEmpty(approvalStatus))
                {
                    query = query.Where(a => a.ApprovalStatus == approvalStatus);
                }

                // Search by name, email, or application number
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(a =>
                        (a.FirstName != null && a.FirstName.Contains(search)) ||
                        (a.LastName != null && a.LastName.Contains(search)) ||
                        (a.ApplicationNumber != null && a.ApplicationNumber.Contains(search)) ||
                        (a.CellPhone != null && a.CellPhone.Contains(search)));
                }

                // Get total count
                var totalCount = await query.CountAsync();

                // Pagination
                var applications = await query
                    .OrderByDescending(a => a.SubmissionDate)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(a => new
                    {
                        a.ApplicationId,
                        a.ApplicationNumber,
                        a.FirstName,
                        a.LastName,
                        FullName = $"{a.FirstName} {a.LastName}",
                        a.CellPhone,
                        a.Position1,
                        a.Position2,
                        a.SubmissionDate,
                        a.Status,
                        a.ApprovalStatus,
                        a.ReviewedDate,
                        ReviewedByName = a.ReviewedBy.HasValue ?
                            _context.Users.Where(u => u.UserId == a.ReviewedBy).Select(u => u.Email).FirstOrDefault() : null
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
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving applications", error = ex.Message });
            }
        }

        // GET: api/JobApplication/Pending (HR/Admin only)
        [HttpGet("Pending")]
        [Authorize(Roles = "Admin,Executive,HRManager")]
        public async Task<IActionResult> GetPendingApplications()
        {
            try
            {
                var applications = await _context.JobApplications
                    .Where(a => a.ApprovalStatus == "Pending")
                    .OrderBy(a => a.SubmissionDate)
                    .Select(a => new
                    {
                        a.ApplicationId,
                        a.ApplicationNumber,
                        a.FirstName,
                        a.LastName,
                        FullName = $"{a.FirstName} {a.LastName}",
                        a.CellPhone,
                        a.Position1,
                        a.Position2,
                        a.SubmissionDate,
                        DaysWaiting = a.SubmissionDate.HasValue ? (DateTime.UtcNow - a.SubmissionDate.Value).Days : 0
                    })
                    .ToListAsync();

                return Ok(applications);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving pending applications", error = ex.Message });
            }
        }

        // GET: api/JobApplication/Status/123
        [HttpGet("Status/{applicationId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetApplicationStatus(int applicationId)
        {
            try
            {
                var application = await _context.JobApplications
                    .Include(a => a.Reviewer) // ✅ Use Reviewer, not ReviewedByUser
                    .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

                if (application == null)
                {
                    return NotFound(new { message = "Application not found" });
                }

                return Ok(new
                {
                    applicationId = application.ApplicationId,
                    applicationNumber = application.ApplicationNumber,
                    applicantName = $"{application.FirstName} {application.LastName}",
                    positionAppliedFor = application.Position1,
                    applicationDate = application.SubmissionDate,
                    status = application.Status,
                    approvalStatus = application.ApprovalStatus,
                    reviewedBy = application.Reviewer != null
                        ? application.Reviewer.Email
                        : null,
                    reviewedDate = application.ReviewedDate
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching status", error = ex.Message });
            }
        }



        // GET: api/JobApplication/{id}
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,Executive,HRManager")]
        public async Task<IActionResult> GetApplicationById(int id)
        {
            try
            {
                var application = await _context.JobApplications
                    .FirstOrDefaultAsync(a => a.ApplicationId == id);

                if (application == null)
                {
                    return NotFound(new { message = "Application not found" });
                }

                // Get reviewer name if reviewed
                string? reviewerName = null;
                if (application.ReviewedBy.HasValue)
                {
                    var reviewer = await _context.Users
                        .FirstOrDefaultAsync(u => u.UserId == application.ReviewedBy.Value);
                    reviewerName = reviewer?.Email;
                }

                // Get employee info if approved
                object? employeeInfo = null;
                if (application.EmployeeId.HasValue)
                {
                    var employee = await _context.Employees
                        .Include(e => e.Department)
                        .FirstOrDefaultAsync(e => e.EmployeeId == application.EmployeeId.Value);

                    if (employee != null)
                    {
                        employeeInfo = new
                        {
                            employee.EmployeeId,
                            employee.EmployeeCode,
                            employee.JobTitle,
                            Department = employee.Department?.DepartmentName,
                            employee.HireDate,
                            employee.EmploymentStatus
                        };
                    }
                }

                return Ok(new
                {
                    application,
                    reviewerName,
                    employeeInfo
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving application", error = ex.Message });
            }
        }

        // PUT: api/JobApplication/{id}/Approve
        [HttpPut("{id}/Approve")]
        [Authorize(Roles = "Admin,Executive,HRManager")]
        public async Task<IActionResult> ApproveApplication(int id, [FromBody] ApprovalRequest request)
        {
            try
            {
                // Get current user ID
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return Unauthorized(new { message = "User ID not found in token" });
                }
                int currentUserId = int.Parse(userIdClaim);

                // Get the application
                var application = await _context.JobApplications
                    .FirstOrDefaultAsync(a => a.ApplicationId == id);

                if (application == null)
                {
                    return NotFound(new { message = "Application not found" });
                }

                if (application.ApprovalStatus != "Pending")
                {
                    return BadRequest(new { message = "Application has already been reviewed" });
                }

                // Validate required approval fields
                if (request.DepartmentId == null || string.IsNullOrEmpty(request.JobTitle) ||
                    string.IsNullOrEmpty(request.EmployeeType) || request.RoleId == null)
                {
                    return BadRequest(new { message = "Department, Job Title, Employee Type, and Role are required for approval" });
                }

                // Update application
                application.ApprovalStatus = "Approved";
                application.ReviewedDate = DateTime.UtcNow;
                application.ReviewedBy = currentUserId;
                application.ReviewNotes = request.ReviewNotes;
                application.DepartmentId = request.DepartmentId;
                application.JobTitle = request.JobTitle;
                application.EmployeeType = request.EmployeeType;
                application.HireDate = request.HireDate ?? DateTime.UtcNow;
                application.LastModified = DateTime.UtcNow;

                // Step 1: Create Employee Record
                var employee = new Employee
                {
                    // Personal Info from Application
                    FirstName = application.FirstName ?? "",
                    MiddleName = application.MiddleName,
                    LastName = application.LastName ?? "",
                    DateOfBirth = !string.IsNullOrEmpty(application.BGDateOfBirth) ?
                        DateTime.Parse(application.BGDateOfBirth) : (DateTime?)null,
                    Gender = null,
                    MaritalStatus = null,
                    PhoneNumber = application.CellPhone,
                    PersonalEmail = null,

                    // Address from Application
                    Address = application.HomeAddress,
                    City = application.City,
                    State = application.State,
                    ZipCode = application.Zip,
                    Country = "USA",

                    // Emergency Contact from Application
                    EmergencyContactName = application.EmergencyContactName,
                    EmergencyContactPhone = null,
                    EmergencyContactRelationship = application.EmergencyContactRelationship,

                    // Employment Info
                    EmployeeCode = await GenerateEmployeeCode(),
                    DepartmentId = request.DepartmentId.Value,
                    ManagerId = request.ManagerId,
                    JobTitle = request.JobTitle,
                    EmployeeType = request.EmployeeType,
                    EmploymentStatus = "PendingOnboarding",
                    HireDate = request.HireDate ?? DateTime.UtcNow,

                    // Compensation
                    Salary = request.Salary,
                    PayFrequency = request.PayFrequency ?? "Monthly",

                    // Banking
                    BankName = null,
                    BankAccountNumber = null,
                    BankRoutingNumber = null,

                    // Benefits
                    IsEligibleForPTO = request.EmployeeType == "AdminStaff",
                    PTOBalance = 0,
                    IsEligibleForInsurance = request.EmployeeType == "AdminStaff",

                    // Status
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Employees.Add(employee);
                await _context.SaveChangesAsync();

                // Step 2: Create User Account
                var workEmail = GenerateWorkEmail(application.FirstName, application.LastName);
                var temporaryPassword = GenerateTemporaryPassword();

                var user = new User
                {
                    Email = workEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword),
                    RoleId = request.RoleId.Value,
                    EmployeeId = employee.EmployeeId,
                    IsActive = true,
                    AccountStatus = "PendingOnboarding",
                    OnboardingStatus = "NotStarted",
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = null
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Step 3: Link Application to Employee and User
                application.EmployeeId = employee.EmployeeId;
                application.UserId = user.UserId;

                await _context.SaveChangesAsync();

                // Step 4: Initialize Onboarding Tasks
                await InitializeOnboardingTasks(employee.EmployeeId, request.EmployeeType);

                // Step 5: Send Welcome Email (TODO: Implement email service)
                // await SendWelcomeEmail(workEmail, temporaryPassword, employee.FirstName);

                return Ok(new
                {
                    message = "Application approved successfully",
                    employeeId = employee.EmployeeId,
                    employeeCode = employee.EmployeeCode,
                    userId = user.UserId,
                    email = workEmail,
                    temporaryPassword = temporaryPassword,
                    onboardingTasksCreated = true
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error approving application", error = ex.Message });
            }
        }

        // PUT: api/JobApplication/{id}/Reject
        [HttpPut("{id}/Reject")]
        [Authorize(Roles = "Admin,Executive,HRManager")]
        public async Task<IActionResult> RejectApplication(int id, [FromBody] RejectionRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return Unauthorized(new { message = "User ID not found in token" });
                }
                int currentUserId = int.Parse(userIdClaim);

                var application = await _context.JobApplications
                    .FirstOrDefaultAsync(a => a.ApplicationId == id);

                if (application == null)
                {
                    return NotFound(new { message = "Application not found" });
                }

                if (application.ApprovalStatus != "Pending")
                {
                    return BadRequest(new { message = "Application has already been reviewed" });
                }

                application.ApprovalStatus = "Rejected";
                application.ReviewedDate = DateTime.UtcNow;
                application.ReviewedBy = currentUserId;
                application.RejectionReason = request.RejectionReason;
                application.ReviewNotes = request.ReviewNotes;
                application.LastModified = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Application rejected successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error rejecting application", error = ex.Message });
            }
        }

        // GET: api/JobApplication/Statistics
        [HttpGet("Statistics")]
        [Authorize(Roles = "Admin,Executive,HRManager")]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                var totalApplications = await _context.JobApplications.CountAsync();
                var pendingApplications = await _context.JobApplications
                    .CountAsync(a => a.ApprovalStatus == "Pending");
                var approvedApplications = await _context.JobApplications
                    .CountAsync(a => a.ApprovalStatus == "Approved");
                var rejectedApplications = await _context.JobApplications
                    .CountAsync(a => a.ApprovalStatus == "Rejected");

                return Ok(new
                {
                    totalApplications,
                    pendingApplications,
                    approvedApplications,
                    rejectedApplications,
                    approvalRate = totalApplications > 0 ?
                        Math.Round((double)approvedApplications / totalApplications * 100, 2) : 0
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving statistics", error = ex.Message });
            }
        }

        // PRIVATE HELPER METHODS
        private string GenerateApplicationNumber()
        {
            var year = DateTime.UtcNow.Year;
            var random = new Random();
            var randomNumber = random.Next(10000, 99999);
            return $"APP-{year}-{randomNumber}";
        }

        private async Task<string> GenerateEmployeeCode()
        {
            var lastEmployee = await _context.Employees
                .OrderByDescending(e => e.EmployeeId)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastEmployee != null && !string.IsNullOrEmpty(lastEmployee.EmployeeCode))
            {
                var parts = lastEmployee.EmployeeCode.Split('-');
                if (parts.Length > 1 && int.TryParse(parts[1], out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"EMP-{nextNumber:D4}";
        }

        private string GenerateWorkEmail(string? firstName, string? lastName)
        {
            var emailBase = $"{firstName?.ToLower()}.{lastName?.ToLower()}@tpa.com";
            return emailBase;
        }

        private string GenerateTemporaryPassword()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 10)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private async Task InitializeOnboardingTasks(int employeeId, string employeeType)
        {
            var tasks = await _context.OnboardingTasks
                .Where(t => t.IsActive &&
                    (t.ApplicableEmployeeType == "Both" || t.ApplicableEmployeeType == employeeType))
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
        }
    }

    // REQUEST MODELS - Put these in a separate file if needed
    public class JobApplicationSubmitRequest
    {
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string? HomeAddress { get; set; }
        public string? AptNumber { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Zip { get; set; }
        public string? HomePhone { get; set; }
        public string? CellPhone { get; set; }
        public string? SSN { get; set; }
        public string? DriversLicense { get; set; }
        public string? DLState { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactRelationship { get; set; }
        public string? EmergencyContactAddress { get; set; }
        public string? Position1 { get; set; }
        public string? Position2 { get; set; }
        public string? SalaryDesired { get; set; }
        public string? AvailableStartDate { get; set; }
        public string? SalaryType { get; set; }
        public string? EmploymentType { get; set; }
        public bool? NashvilleLocation { get; set; }
        public bool? FranklinLocation { get; set; }
        public bool? ShelbyvilleLocation { get; set; }
        public bool? WaynesboroLocation { get; set; }
        public bool? OtherLocation { get; set; }
        public bool? FirstShift { get; set; }
        public bool? SecondShift { get; set; }
        public bool? ThirdShift { get; set; }
        public bool? WeekendsOnly { get; set; }
        public bool? MondayAvailable { get; set; }
        public bool? TuesdayAvailable { get; set; }
        public bool? WednesdayAvailable { get; set; }
        public bool? ThursdayAvailable { get; set; }
        public bool? FridayAvailable { get; set; }
        public bool? SaturdayAvailable { get; set; }
        public bool? SundayAvailable { get; set; }
        public bool? AppliedBefore { get; set; }
        public string? AppliedBeforeWhen { get; set; }
        public bool? WorkedBefore { get; set; }
        public string? WorkedBeforeWhen { get; set; }
        public bool? FamilyEmployed { get; set; }
        public string? FamilyEmployedWho { get; set; }
        public bool? USCitizen { get; set; }
        public string? AlienNumber { get; set; }
        public bool? LegallyEntitled { get; set; }
        public bool? Over18 { get; set; }
        public bool? ArmedForces { get; set; }
        public bool? ConvictedOfCrime { get; set; }
        public bool? AbuseRegistry { get; set; }
        public string? ElementarySchool { get; set; }
        public string? HighSchool { get; set; }
        public string? UndergraduateSchool { get; set; }
        public string? GraduateSchool { get; set; }
        public bool? Elem1 { get; set; }
        public bool? Elem2 { get; set; }
        public bool? Elem3 { get; set; }
        public bool? Elem4 { get; set; }
        public bool? Elem5 { get; set; }
        public bool? HS9 { get; set; }
        public bool? HS10 { get; set; }
        public bool? HS11 { get; set; }
        public bool? HS12 { get; set; }
        public bool? UG1 { get; set; }
        public bool? UG2 { get; set; }
        public bool? UG3 { get; set; }
        public bool? UG4 { get; set; }
        public bool? UG5 { get; set; }
        public bool? Grad1 { get; set; }
        public bool? Grad2 { get; set; }
        public bool? Grad3 { get; set; }
        public bool? Grad4 { get; set; }
        public bool? Grad5 { get; set; }
        public bool? ElemDiploma { get; set; }
        public bool? HSDiploma { get; set; }
        public bool? UGDegree { get; set; }
        public bool? GradDegree { get; set; }
        public string? UGSkills { get; set; }
        public string? GradSkills { get; set; }
        public string? SpecialKnowledge { get; set; }
        public string? LicenseType1 { get; set; }
        public string? LicenseState1 { get; set; }
        public string? LicenseNumber1 { get; set; }
        public string? LicenseExpiration1 { get; set; }
        public string? LicenseType2 { get; set; }
        public string? LicenseState2 { get; set; }
        public string? LicenseNumber2 { get; set; }
        public string? LicenseExpiration2 { get; set; }
        public string? Employer1 { get; set; }
        public string? EmploymentFrom1 { get; set; }
        public string? EmploymentTo1 { get; set; }
        public string? JobTitle1 { get; set; }
        public string? Supervisor1 { get; set; }
        public string? EmployerAddress1 { get; set; }
        public string? EmployerCityStateZip1 { get; set; }
        public string? EmployerPhone1 { get; set; }
        public string? StartingPay1 { get; set; }
        public string? FinalPay1 { get; set; }
        public string? WorkPerformed1 { get; set; }
        public string? ReasonLeaving1 { get; set; }
        public string? Employer2 { get; set; }
        public string? EmploymentFrom2 { get; set; }
        public string? EmploymentTo2 { get; set; }
        public string? JobTitle2 { get; set; }
        public string? Supervisor2 { get; set; }
        public string? EmployerAddress2 { get; set; }
        public string? EmployerCityStateZip2 { get; set; }
        public string? EmployerPhone2 { get; set; }
        public string? StartingPay2 { get; set; }
        public string? FinalPay2 { get; set; }
        public string? WorkPerformed2 { get; set; }
        public string? ReasonLeaving2 { get; set; }
        public string? Employer3 { get; set; }
        public string? EmploymentFrom3 { get; set; }
        public string? EmploymentTo3 { get; set; }
        public string? JobTitle3 { get; set; }
        public string? Supervisor3 { get; set; }
        public string? EmployerAddress3 { get; set; }
        public string? EmployerCityStateZip3 { get; set; }
        public string? EmployerPhone3 { get; set; }
        public string? StartingPay3 { get; set; }
        public string? FinalPay3 { get; set; }
        public string? WorkPerformed3 { get; set; }
        public string? ReasonLeaving3 { get; set; }
        public string? Reference1Name { get; set; }
        public string? Reference1Phone { get; set; }
        public string? Reference1Email { get; set; }
        public string? Reference1Years { get; set; }
        public string? Reference2Name { get; set; }
        public string? Reference2Phone { get; set; }
        public string? Reference2Email { get; set; }
        public string? Reference2Years { get; set; }
        public string? Reference3Name { get; set; }
        public string? Reference3Phone { get; set; }
        public string? Reference3Email { get; set; }
        public string? Reference3Years { get; set; }
        public string? BGLastName { get; set; }
        public string? BGFirstName { get; set; }
        public string? BGMiddleName { get; set; }
        public string? BGStreet { get; set; }
        public string? BGCity { get; set; }
        public string? BGState { get; set; }
        public string? BGZipCode { get; set; }
        public string? BGSSN { get; set; }
        public string? BGPhone { get; set; }
        public string? BGOtherName { get; set; }
        public string? BGNameChangeYear { get; set; }
        public string? BGDriversLicense { get; set; }
        public string? BGDLState { get; set; }
        public string? BGDateOfBirth { get; set; }
        public string? BGNameOnLicense { get; set; }
        public bool? ProtectionNoAbuse { get; set; }
        public bool? ProtectionHadAbuse { get; set; }
        public string? ProtectionWitness { get; set; }
        public string? ReferenceAuthName { get; set; }
        public string? ApplicantSignature { get; set; }
        public string? SignatureDate { get; set; }
        public bool? FinalAcknowledgment { get; set; }
    }

    public class ApprovalRequest
    {
        public int? DepartmentId { get; set; }
        public string? JobTitle { get; set; }
        public string? EmployeeType { get; set; }
        public int? RoleId { get; set; }
        public int? ManagerId { get; set; }
        public DateTime? HireDate { get; set; }
        public decimal? Salary { get; set; }
        public string? PayFrequency { get; set; }
        public string? ReviewNotes { get; set; }
    }

    public class RejectionRequest
    {
        public string? RejectionReason { get; set; }
        public string? ReviewNotes { get; set; }
    }
}