using System.ComponentModel.DataAnnotations;
using Admission.Entity.Enums;
using Shared.Kernel.Tenancy;
using Shared.Kernel.Validation;

namespace Admission.Entity.TableEntities;

/// <summary>A parent asking about a seat. Becomes an application, or is lost.</summary>
public class Enquiry : OrgScopedEntity
{
    public long EnquiryId { get; set; }

    public DateOnly EnquiryDate { get; set; }

    [Required(ErrorMessage = "Child's name is required.")]
    [MaxLength(200, ErrorMessage = "Child's name cannot exceed 200 characters.")]
    public string ChildName { get; set; } = null!;

    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Unenforced: <c>sis.SchoolClasses</c>, checked through Student.</summary>
    public long SeekingClassId { get; set; }

    /// <summary>Unenforced: <c>sis.AcademicYears</c>, checked through Student.</summary>
    public long AcademicYearId { get; set; }

    [Required(ErrorMessage = "Parent's name is required.")]
    [MaxLength(200, ErrorMessage = "Parent's name cannot exceed 200 characters.")]
    public string ParentName { get; set; } = null!;

    [Required(ErrorMessage = "Phone is required.")]
    [MaxLength(20, ErrorMessage = "Phone cannot exceed 20 characters.")]
    [Mobile(ErrorMessage = "Phone cannot exceed 20 characters.")]
    public string Phone { get; set; } = null!;

    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [MaxLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
    public string? Email { get; set; }

    public EnquirySource EnquirySource { get; set; } = EnquirySource.WalkIn;

    public EnquiryStatus EnquiryStatus { get; set; } = EnquiryStatus.Open;

    public DateOnly? FollowUpDate { get; set; }
}

/// <summary>
/// An application for a seat, numbered from the APL series. Admitting it makes
/// the student in Student and the guardian contact in Master, both idempotently:
/// the student is keyed on this application, the guardian on the mobile number.
///
/// The design names no guardian columns here; they are added because admit has
/// to make a guardian, and an application need not come from an enquiry.
/// </summary>
public class Application : OrgScopedEntity
{
    public long ApplicationId { get; set; }

    [Required(ErrorMessage = "Application number is required.")]
    [MaxLength(30, ErrorMessage = "Application number cannot exceed 30 characters.")]
    public string ApplicationNo { get; set; } = null!;

    public long? EnquiryId { get; set; }

    public DateOnly ApplicationDate { get; set; }

    [Required(ErrorMessage = "Child's first name is required.")]
    [MaxLength(100, ErrorMessage = "Child's first name cannot exceed 100 characters.")]
    public string ChildFirstName { get; set; } = null!;

    [MaxLength(100, ErrorMessage = "Child's last name cannot exceed 100 characters.")]
    public string? ChildLastName { get; set; }

    public DateOnly DateOfBirth { get; set; }

    public ChildGender ChildGender { get; set; } = ChildGender.NotStated;

    public long SeekingClassId { get; set; }

    public long AcademicYearId { get; set; }

    [Required(ErrorMessage = "Guardian's name is required.")]
    [MaxLength(200, ErrorMessage = "Guardian's name cannot exceed 200 characters.")]
    public string GuardianName { get; set; } = null!;

    [Required(ErrorMessage = "Guardian's mobile is required.")]
    [MaxLength(20, ErrorMessage = "Guardian's mobile cannot exceed 20 characters.")]
    [Mobile(ErrorMessage = "Guardian's mobile cannot exceed 20 characters.")]
    public string GuardianPhone { get; set; } = null!;

    [EmailAddress(ErrorMessage = "Guardian's email must be a valid email address.")]
    [MaxLength(255, ErrorMessage = "Guardian's email cannot exceed 255 characters.")]
    public string? GuardianEmail { get; set; }

    public ParentRelationship GuardianRelationship { get; set; } = ParentRelationship.Guardian;

    /// <summary>Unenforced: the <c>con.Contacts</c> guardian admit found or made.</summary>
    public long? GuardianContactId { get; set; }

    public ApplicationStage ApplicationStage { get; set; } = ApplicationStage.Submitted;

    public decimal? AssessmentScore { get; set; }

    /// <summary>Unenforced: the <c>sis.Students</c> row admit created.</summary>
    public long? AdmittedStudentId { get; set; }

    [MaxLength(30, ErrorMessage = "Admission number cannot exceed 30 characters.")]
    public string? AdmissionNo { get; set; }

    /// <summary>Collected as a fee receipt, not here.</summary>
    public decimal ApplicationFee { get; set; }

    public ICollection<ApplicationDocument> Documents { get; set; } = [];
}

public class ApplicationDocument : OrgScopedEntity
{
    public long ApplicationDocumentId { get; set; }

    public long ApplicationId { get; set; }

    public DocumentKind DocumentKind { get; set; }

    [MaxLength(500, ErrorMessage = "Attachment key cannot exceed 500 characters.")]
    public string? AttachmentKey { get; set; }

    [MaxLength(200, ErrorMessage = "Remarks cannot exceed 200 characters.")]
    public string? Remarks { get; set; }

    public bool IsVerified { get; set; }
}
