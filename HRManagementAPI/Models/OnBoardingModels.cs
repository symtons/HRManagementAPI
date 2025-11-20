using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRManagementAPI.Models
{
    // ===== JobApplication Model =====
    [Table("JobApplications")]
    public class JobApplication
    {
        [Key]
        public int ApplicationId { get; set; }

        public string? ApplicationNumber { get; set; }
        public bool IsSubmitted { get; set; }
        public DateTime? SubmissionDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastModified { get; set; }
        public string Status { get; set; } = "Pending";

        // Review & Approval Fields (NEW)
        public string ApprovalStatus { get; set; } = "Pending"; // Pending/Approved/Rejected
        public DateTime? ReviewedDate { get; set; }
        public int? ReviewedBy { get; set; }
        public string? ReviewNotes { get; set; }
        public string? RejectionReason { get; set; }

        // Assignment Fields (NEW)
        public int? EmployeeId { get; set; }
        public int? UserId { get; set; }
        public int? DepartmentId { get; set; }
        public string? JobTitle { get; set; }
        public string? EmployeeType { get; set; }
        public DateTime? HireDate { get; set; }

        // Personal Information
        public string? ApplicationDate { get; set; }
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

        // Emergency Contact
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactRelationship { get; set; }
        public string? EmergencyContactAddress { get; set; }

        // Position Applied For
        public string? Position1 { get; set; }
        public string? Position2 { get; set; }
        public string? SalaryDesired { get; set; }
        public string? AvailableStartDate { get; set; }
        public string? SalaryType { get; set; }
        public string? EmploymentType { get; set; }

        // Location Preferences
        public bool? NashvilleLocation { get; set; }
        public bool? FranklinLocation { get; set; }
        public bool? ShelbyvilleLocation { get; set; }
        public bool? WaynesboroLocation { get; set; }
        public bool? OtherLocation { get; set; }

        // Shift Preferences
        public bool? FirstShift { get; set; }
        public bool? SecondShift { get; set; }
        public bool? ThirdShift { get; set; }
        public bool? WeekendsOnly { get; set; }

        // Days Available
        public bool? MondayAvailable { get; set; }
        public bool? TuesdayAvailable { get; set; }
        public bool? WednesdayAvailable { get; set; }
        public bool? ThursdayAvailable { get; set; }
        public bool? FridayAvailable { get; set; }
        public bool? SaturdayAvailable { get; set; }
        public bool? SundayAvailable { get; set; }

        // Previous Application/Employment
        public bool? AppliedBefore { get; set; }
        public string? AppliedBeforeWhen { get; set; }
        public bool? WorkedBefore { get; set; }
        public string? WorkedBeforeWhen { get; set; }
        public bool? FamilyEmployed { get; set; }
        public string? FamilyEmployedWho { get; set; }

        // Legal Status
        public bool? USCitizen { get; set; }
        public string? AlienNumber { get; set; }
        public bool? LegallyEntitled { get; set; }
        public bool? Over18 { get; set; }
        public bool? ArmedForces { get; set; }
        public bool? ConvictedOfCrime { get; set; }
        public bool? AbuseRegistry { get; set; }

        // Education
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

        // Licenses
        public string? LicenseType1 { get; set; }
        public string? LicenseState1 { get; set; }
        public string? LicenseNumber1 { get; set; }
        public string? LicenseExpiration1 { get; set; }
        public string? LicenseType2 { get; set; }
        public string? LicenseState2 { get; set; }
        public string? LicenseNumber2 { get; set; }
        public string? LicenseExpiration2 { get; set; }

        // Employment History 1
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

        // Employment History 2
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

        // Employment History 3
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

        // References
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

        // DIDD Section
        public string? DIDDFullName { get; set; }
        public string? DIDDSSN { get; set; }
        public string? DIDDDateOfBirth { get; set; }
        public string? DIDDDriversLicense { get; set; }
        public string? DIDDWitness { get; set; }

        // Background Check Info
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

        // Protection & Acknowledgments
        public bool? ProtectionNoAbuse { get; set; }
        public bool? ProtectionHadAbuse { get; set; }
        public string? ProtectionWitness { get; set; }
        public string? ReferenceAuthName { get; set; }
        public string? SSNLast4 { get; set; }
        public string? ApplicantSignature { get; set; }
        public string? SignatureDate { get; set; }
        public bool? FinalAcknowledgment { get; set; }

        // Navigation Properties (NEW)
        [ForeignKey("ReviewedBy")]
        public virtual User? Reviewer { get; set; }

        [ForeignKey("EmployeeId")]
        public virtual Employee? Employee { get; set; }

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        [ForeignKey("DepartmentId")]
        public virtual Department? Department { get; set; }
    }

    // ===== OnboardingTask Model (Master Template) =====
    [Table("OnboardingTasks")]
    public class OnboardingTask
    {
        [Key]
        public int TaskId { get; set; }

        [Required]
        [MaxLength(200)]
        public string TaskName { get; set; }

        public string? TaskDescription { get; set; }

        [MaxLength(100)]
        public string? TaskCategory { get; set; } // Document/Form/Policy/Training/Information

        [MaxLength(50)]
        public string? TaskType { get; set; } // Upload/Acknowledgment/Input/ESign

        public bool IsRequired { get; set; } = true;
        public int DefaultDueDays { get; set; } = 7;

        [MaxLength(20)]
        public string ApplicableEmployeeType { get; set; } = "Both"; // AdminStaff/FieldStaff/Both

        [MaxLength(200)]
        public string? RequiredFileTypes { get; set; } // e.g., "pdf,jpg,png"

        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;

        public string? InstructionText { get; set; } // Instructions shown to employee

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Property
        public virtual ICollection<EmployeeOnboardingTask> EmployeeOnboardingTasks { get; set; }
    }

    // ===== EmployeeOnboardingTask Model (Assigned Tasks) =====
    [Table("EmployeeOnboardingTasks")]
    public class EmployeeOnboardingTask
    {
        [Key]
        public int OnboardingTaskId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public int TaskId { get; set; }

        // Task Tracking
        public DateTime AssignedDate { get; set; } = DateTime.UtcNow;
        public DateTime DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Pending"; // Pending/InProgress/Completed/Overdue

        // Task Response
        public string? SubmittedData { get; set; } // JSON or text data

        [MaxLength(500)]
        public string? DocumentPath { get; set; }

        [MaxLength(200)]
        public string? DocumentOriginalName { get; set; }

        public string? Notes { get; set; }

        // Approval/Verification
        public int? CompletedBy { get; set; }
        public int? VerifiedBy { get; set; }
        public string? VerificationNotes { get; set; }
        public bool IsVerified { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("EmployeeId")]
        public virtual Employee Employee { get; set; }

        [ForeignKey("TaskId")]
        public virtual OnboardingTask Task { get; set; }

        [ForeignKey("CompletedBy")]
        public virtual User? CompletedByUser { get; set; }

        [ForeignKey("VerifiedBy")]
        public virtual User? VerifiedByUser { get; set; }
    }
}