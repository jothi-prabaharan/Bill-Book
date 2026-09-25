using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;
using Student.Entity.Enums;

namespace Student.Entity.TableEntities;

/// <summary>
/// A student. Not a contact: the guardians are the contacts, and the primary
/// one is invoiced. <c>AdmissionNo</c> comes from the <c>ADM</c> series and is
/// never reused.
/// </summary>
public class StudentRecord : OrgScopedEntity
{
    public long StudentId { get; set; }

    [Required(ErrorMessage = "Admission number is required.")]
    [MaxLength(30, ErrorMessage = "Admission number cannot exceed 30 characters.")]
    public string AdmissionNo { get; set; } = null!;

    [Required(ErrorMessage = "First name is required.")]
    [MaxLength(100, ErrorMessage = "First name cannot exceed 100 characters.")]
    public string FirstName { get; set; } = null!;

    [MaxLength(100, ErrorMessage = "Last name cannot exceed 100 characters.")]
    public string? LastName { get; set; }

    public DateOnly DateOfBirth { get; set; }

    public Gender Gender { get; set; } = Gender.NotStated;

    public DateOnly AdmissionDate { get; set; }

    public StudentStatus StudentStatus { get; set; } = StudentStatus.Active;

    public DateOnly? LeavingDate { get; set; }

    [MaxLength(5, ErrorMessage = "Blood group cannot exceed 5 characters.")]
    public string? BloodGroup { get; set; }

    /// <summary>APAAR or Aadhaar. Masked on every list.</summary>
    [MaxLength(20, ErrorMessage = "National id cannot exceed 20 characters.")]
    public string? NationalId { get; set; }

    [MaxLength(500, ErrorMessage = "Photo key cannot exceed 500 characters.")]
    public string? PhotoAttachmentKey { get; set; }

    /// <summary>Unenforced: the <c>adm.Applications</c> row admitted, which makes admitting idempotent.</summary>
    public long? SourceApplicationId { get; set; }

    public ICollection<StudentGuardian> Guardians { get; set; } = [];
}

/// <summary>A student's guardian: a <c>con</c> contact with <c>IsGuardian</c>, checked through Master.</summary>
public class StudentGuardian : OrgScopedEntity
{
    public long StudentGuardianId { get; set; }

    public long StudentId { get; set; }

    /// <summary>Unenforced: <c>con.Contacts</c>.</summary>
    public long ContactId { get; set; }

    public GuardianRelationship Relationship { get; set; } = GuardianRelationship.Guardian;

    /// <summary>Exactly one per student: the guardian who is invoiced.</summary>
    public bool IsPrimary { get; set; }

    public bool HasPortalAccess { get; set; }
}

/// <summary>A student in a section for a year. One per student per year.</summary>
public class Enrolment : OrgScopedEntity
{
    public long EnrolmentId { get; set; }

    public long StudentId { get; set; }

    public long AcademicYearId { get; set; }

    public long SectionId { get; set; }

    [Range(1, 1000, ErrorMessage = "Roll number must be between 1 and 1000.")]
    public int? RollNo { get; set; }

    public EnrolmentStatus EnrolmentStatus { get; set; } = EnrolmentStatus.Active;
}
