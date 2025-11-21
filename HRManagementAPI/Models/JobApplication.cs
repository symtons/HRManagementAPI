// HRManagementAPI/Models/JobApplication.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRManagementAPI.Models
{
    [Table("JobApplications")]
    public class JobApplication
    {
        [Key]
        public int ApplicationId { get; set; }

        // ================================================================
        // PERSONAL INFORMATION
        // ================================================================

        public DateTime? ApplicationDate { get; set; }

        [Required]
        [StringLength(50)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(50)]
        public string LastName { get; set; }

        [StringLength(50)]
        public string? MiddleName { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [StringLength(20)]
        public string? Gender { get; set; }

        [Required]
        [StringLength(20)]
        public string PhoneNumber { get; set; }

        [StringLength(20)]
        public string? CellNumber { get; set; }

        [Required]
        [StringLength(100)]
        public string Email { get; set; }

        [Required]
        [StringLength(200)]
        public string Address { get; set; }

        [StringLength(20)]
        public string? AptNumber { get; set; }

        [Required]
        [StringLength(50)]
        public string City { get; set; }

        [Required]
        [StringLength(2)]
        public string State { get; set; }

        [Required]
        [StringLength(10)]
        public string ZipCode { get; set; }

        [StringLength(50)]
        public string? Country { get; set; }

        [StringLength(11)]
        public string? SocialSecurityNumber { get; set; }

        [StringLength(50)]
        public string? DriversLicenseNumber { get; set; }

        [StringLength(2)]
        public string? DriversLicenseState { get; set; }

        [StringLength(100)]
        public string? EmergencyContactPerson { get; set; }

        [StringLength(50)]
        public string? EmergencyContactRelationship { get; set; }

        [StringLength(200)]
        public string? EmergencyContactAddress { get; set; }

        [StringLength(20)]
        public string? EmergencyContactPhone { get; set; }

        // ================================================================
        // POSITION DETAILS
        // ================================================================

        [Required]
        [StringLength(100)]
        public string PositionAppliedFor { get; set; }

        [StringLength(100)]
        public string? Position2 { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? DesiredSalary { get; set; }

        [StringLength(20)]
        public string? SalaryType { get; set; } // Hourly or Yearly

        public DateTime? ExpectedStartDate { get; set; }

        [StringLength(20)]
        public string? EmploymentSought { get; set; } // FullTime, PartTime, Temporary

        public string? DesiredLocations { get; set; } // JSON array

        [StringLength(100)]
        public string? DesiredLocationOther { get; set; }

        public string? ShiftPreferences { get; set; } // JSON array

        public string? DaysAvailable { get; set; } // JSON array

        // ================================================================
        // BACKGROUND QUESTIONS
        // ================================================================

        public bool? PreviouslyAppliedToTPA { get; set; }

        [StringLength(100)]
        public string? PreviouslyAppliedWhen { get; set; }

        public bool? PreviouslyWorkedForTPA { get; set; }

        [StringLength(100)]
        public string? PreviouslyWorkedWhen { get; set; }

        public bool? FamilyMembersAtTPA { get; set; }

        [StringLength(200)]
        public string? FamilyMembersWho { get; set; }

        public bool? USCitizenOrResident { get; set; }

        [StringLength(50)]
        public string? AlienNumber { get; set; }

        public bool? LegallyEntitledToWork { get; set; }

        public bool? EighteenOrOlder { get; set; }

        public bool? ServedInArmedForces { get; set; }

        public bool? ConvictedOfCrime { get; set; }

        public string? CrimeDetails { get; set; } // JSON array

        public bool? NameOnAbuseRegistry { get; set; }

        public bool? FoundGuiltyOfAbuse { get; set; }

        public bool? HealthcareLicenseIssues { get; set; }

        // ================================================================
        // EDUCATION
        // ================================================================

        public string? EducationHistory { get; set; } // JSON array

        public string? SpecialSkillsKnowledge { get; set; }

        public int? TypingSpeedWPM { get; set; }

        // ================================================================
        // LICENSES & CERTIFICATIONS
        // ================================================================

        public string? LicensesAndCertifications { get; set; } // JSON array

        public string? DIDDTrainingClasses { get; set; }

        // ================================================================
        // REFERENCES
        // ================================================================

        public string? References { get; set; } // JSON array

        // ================================================================
        // EMPLOYMENT HISTORY
        // ================================================================

        public string? EmploymentHistory { get; set; } // JSON array

        // ================================================================
        // AUTHORIZATIONS
        // ================================================================

        public bool BackgroundCheckConsent { get; set; }

        public DateTime? BackgroundCheckDate { get; set; }

        [StringLength(200)]
        public string? BackgroundCheckSignature { get; set; }

        public bool? NYApplicant { get; set; }

        public bool? MNOKApplicant { get; set; }

        public bool? CAApplicant { get; set; }

        public bool ReferenceCheckConsent { get; set; }

        [StringLength(4)]
        public string? ReferenceCheckSSNLast4 { get; set; }

        public DateTime? ReferenceCheckDate { get; set; }

        [StringLength(200)]
        public string? ReferenceCheckSignature { get; set; }

        public bool? HasNoAbuseCaseAgainstMe { get; set; }

        public bool? HasAbuseCaseAgainstMe { get; set; }

        public bool DIDDAuthorizationConsent { get; set; }

        [StringLength(200)]
        public string? DIDDFullName { get; set; }

        [StringLength(11)]
        public string? DIDDSSN { get; set; }

        public DateTime? DIDDOB { get; set; }

        [StringLength(50)]
        public string? DIDDDriverLicense { get; set; }

        [StringLength(200)]
        public string? DIDDWitnessName { get; set; }

        public bool? ProtectionNoAbuseCase { get; set; }

        public bool? ProtectionHasAbuseCase { get; set; }

        public bool ProtectionAuthorizationConsent { get; set; }

        // ================================================================
        // STATUS & METADATA
        // ================================================================

        [StringLength(50)]
        public string? Status { get; set; } // Submitted, UnderReview, Approved, Rejected

        public DateTime? SubmittedAt { get; set; }

        public DateTime? ReviewedAt { get; set; }

        public int? ReviewedBy { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        // ================================================================
        // NAVIGATION PROPERTIES
        // ================================================================

        [ForeignKey("ReviewedBy")]
        public virtual User? Reviewer { get; set; }
    }
}