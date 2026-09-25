using System.ComponentModel.DataAnnotations;
using Admission.Entity.Enums;
using Shared.Kernel.Validation;

namespace Admission.Entity.Models;

// Requests and views for the adm API (S2, TK-62).

public sealed record AdmissionMessage(string Message);

public sealed class SaveEnquiryRequest
{
    public DateOnly EnquiryDate { get; set; }

    [Required(ErrorMessage = "Child's name is required.")]
    [MaxLength(200, ErrorMessage = "Child's name cannot exceed 200 characters.")]
    public string ChildName { get; set; } = null!;

    public DateOnly? DateOfBirth { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose the class sought.")]
    public long SeekingClassId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose the school year.")]
    public long AcademicYearId { get; set; }

    [Required(ErrorMessage = "Parent's name is required.")]
    [MaxLength(200, ErrorMessage = "Parent's name cannot exceed 200 characters.")]
    public string ParentName { get; set; } = null!;

    [Required(ErrorMessage = "Phone is required.")]
    [Mobile(ErrorMessage = "Phone cannot exceed 20 characters.")]
    public string Phone { get; set; } = null!;

    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [MaxLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
    public string? Email { get; set; }

    public EnquirySource EnquirySource { get; set; } = EnquirySource.WalkIn;

    public EnquiryStatus EnquiryStatus { get; set; } = EnquiryStatus.Open;

    public DateOnly? FollowUpDate { get; set; }
}

public sealed class EnquiryView
{
    public long EnquiryId { get; set; }

    public DateOnly EnquiryDate { get; set; }

    public string ChildName { get; set; } = null!;

    public DateOnly? DateOfBirth { get; set; }

    public long SeekingClassId { get; set; }

    public long AcademicYearId { get; set; }

    public string ParentName { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string? Email { get; set; }

    public EnquirySource EnquirySource { get; set; }

    public EnquiryStatus EnquiryStatus { get; set; }

    public DateOnly? FollowUpDate { get; set; }
}

public sealed class SaveApplicationRequest
{
    public long? EnquiryId { get; set; }

    public DateOnly ApplicationDate { get; set; }

    [Required(ErrorMessage = "Child's first name is required.")]
    [MaxLength(100, ErrorMessage = "Child's first name cannot exceed 100 characters.")]
    public string ChildFirstName { get; set; } = null!;

    [MaxLength(100, ErrorMessage = "Child's last name cannot exceed 100 characters.")]
    public string? ChildLastName { get; set; }

    public DateOnly DateOfBirth { get; set; }

    public ChildGender ChildGender { get; set; } = ChildGender.NotStated;

    [Range(1, long.MaxValue, ErrorMessage = "Choose the class sought.")]
    public long SeekingClassId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose the school year.")]
    public long AcademicYearId { get; set; }

    [Required(ErrorMessage = "Guardian's name is required.")]
    [MaxLength(200, ErrorMessage = "Guardian's name cannot exceed 200 characters.")]
    public string GuardianName { get; set; } = null!;

    [Required(ErrorMessage = "Guardian's mobile is required.")]
    [Mobile(ErrorMessage = "Guardian's mobile cannot exceed 20 characters.")]
    public string GuardianPhone { get; set; } = null!;

    [EmailAddress(ErrorMessage = "Guardian's email must be a valid email address.")]
    [MaxLength(255, ErrorMessage = "Guardian's email cannot exceed 255 characters.")]
    public string? GuardianEmail { get; set; }

    public ParentRelationship GuardianRelationship { get; set; } = ParentRelationship.Guardian;

    [Range(typeof(decimal), "0", "10000000", ErrorMessage = "The application fee cannot be negative.")]
    public decimal ApplicationFee { get; set; }

    public List<ApplicationDocumentModel> Documents { get; set; } = [];
}

public sealed class ApplicationDocumentModel
{
    public long ApplicationDocumentId { get; set; }

    public DocumentKind DocumentKind { get; set; }

    [MaxLength(500, ErrorMessage = "Attachment key cannot exceed 500 characters.")]
    public string? AttachmentKey { get; set; }

    [MaxLength(200, ErrorMessage = "Remarks cannot exceed 200 characters.")]
    public string? Remarks { get; set; }

    public bool IsVerified { get; set; }
}

public sealed class ApplicationView
{
    public long ApplicationId { get; set; }

    public string ApplicationNo { get; set; } = null!;

    public long? EnquiryId { get; set; }

    public DateOnly ApplicationDate { get; set; }

    public string ChildFirstName { get; set; } = null!;

    public string? ChildLastName { get; set; }

    public DateOnly DateOfBirth { get; set; }

    public ChildGender ChildGender { get; set; }

    public long SeekingClassId { get; set; }

    public long AcademicYearId { get; set; }

    public string GuardianName { get; set; } = null!;

    public string GuardianPhone { get; set; } = null!;

    public string? GuardianEmail { get; set; }

    public ParentRelationship GuardianRelationship { get; set; }

    public long? GuardianContactId { get; set; }

    public ApplicationStage ApplicationStage { get; set; }

    public decimal? AssessmentScore { get; set; }

    public long? AdmittedStudentId { get; set; }

    public string? AdmissionNo { get; set; }

    public decimal ApplicationFee { get; set; }

    public List<ApplicationDocumentModel> Documents { get; set; } = [];
}

public sealed class MoveApplicationRequest
{
    public ApplicationStage ApplicationStage { get; set; }

    [Range(typeof(decimal), "0", "1000", ErrorMessage = "The assessment score must be between 0 and 1000.")]
    public decimal? AssessmentScore { get; set; }
}

public sealed class AdmitRequest
{
    public DateOnly AdmissionDate { get; set; }

    /// <summary>Enrol the new student straight into this section of the application's year and class.</summary>
    public long? SectionId { get; set; }

    [Range(1, 1000, ErrorMessage = "Roll number must be between 1 and 1000.")]
    public int? RollNo { get; set; }
}

public sealed class AdmitResponse
{
    public long StudentId { get; set; }

    public string AdmissionNo { get; set; } = null!;

    public long GuardianContactId { get; set; }
}
