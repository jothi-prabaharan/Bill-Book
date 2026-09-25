using System.ComponentModel.DataAnnotations;
using Sis.Entity.Enums;

namespace Sis.Entity.Models;

// Requests and views for the sis API (S1, TK-61).

public sealed record SisMessage(string Message);

public sealed class SaveAcademicYearRequest
{
    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(20, ErrorMessage = "Code cannot exceed 20 characters.")]
    public string Code { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsCurrent { get; set; }

    public bool IsClosed { get; set; }
}

public sealed class AcademicYearView
{
    public long AcademicYearId { get; set; }

    public string Code { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsCurrent { get; set; }

    public bool IsClosed { get; set; }
}

public sealed class SaveSchoolClassRequest
{
    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(20, ErrorMessage = "Code cannot exceed 20 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [Range(1, 100, ErrorMessage = "Order must be between 1 and 100.")]
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class SchoolClassView
{
    public long SchoolClassId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }
}

public sealed class SaveSectionRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose an academic year.")]
    public long AcademicYearId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose a class.")]
    public long SchoolClassId { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(20, ErrorMessage = "Name cannot exceed 20 characters.")]
    public string Name { get; set; } = null!;

    [Range(1, 1000, ErrorMessage = "Capacity must be between 1 and 1000.")]
    public int? Capacity { get; set; }

    public long? ClassTeacherEmployeeId { get; set; }

    public long? RoomSpaceId { get; set; }
}

public sealed class SectionView
{
    public long SectionId { get; set; }

    public long AcademicYearId { get; set; }

    public string AcademicYearCode { get; set; } = null!;

    public long SchoolClassId { get; set; }

    public string ClassName { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int? Capacity { get; set; }

    public int Enrolled { get; set; }

    public long? ClassTeacherEmployeeId { get; set; }

    public long? RoomSpaceId { get; set; }
}

public sealed class SaveSubjectRequest
{
    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(20, ErrorMessage = "Code cannot exceed 20 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public SubjectKind SubjectKind { get; set; } = SubjectKind.Core;

    public bool IsActive { get; set; } = true;
}

public sealed class SubjectView
{
    public long SubjectId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public SubjectKind SubjectKind { get; set; }

    public bool IsActive { get; set; }
}

public sealed class StudentGuardianModel
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose a guardian contact.")]
    public long ContactId { get; set; }

    public GuardianRelationship Relationship { get; set; } = GuardianRelationship.Guardian;

    public bool IsPrimary { get; set; }

    public bool HasPortalAccess { get; set; }

    /// <summary>On reads only: the contact's name, from Master.</summary>
    public string? DisplayName { get; set; }
}

public sealed class EnrolRequest
{
    /// <summary>Ignored when the enrolment comes with a new student.</summary>
    public long StudentId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose an academic year.")]
    public long AcademicYearId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose a section.")]
    public long SectionId { get; set; }

    [Range(1, 1000, ErrorMessage = "Roll number must be between 1 and 1000.")]
    public int? RollNo { get; set; }
}

public sealed class SaveStudentRequest
{
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

    [MaxLength(20, ErrorMessage = "National id cannot exceed 20 characters.")]
    public string? NationalId { get; set; }

    public List<StudentGuardianModel> Guardians { get; set; } = [];

    /// <summary>On create only: enrol the new student straight into a section.</summary>
    public EnrolRequest? Enrol { get; set; }
}

public sealed class StudentListItem
{
    public long StudentId { get; set; }

    public string AdmissionNo { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public Gender Gender { get; set; }

    public StudentStatus StudentStatus { get; set; }

    public string? ClassName { get; set; }

    public string? SectionName { get; set; }

    public int? RollNo { get; set; }

    /// <summary>Always masked on a list: the last four characters only.</summary>
    public string? NationalId { get; set; }
}

public sealed class EnrolmentView
{
    public long EnrolmentId { get; set; }

    public long AcademicYearId { get; set; }

    public string AcademicYearCode { get; set; } = null!;

    public long SectionId { get; set; }

    public string ClassName { get; set; } = null!;

    public string SectionName { get; set; } = null!;

    public int? RollNo { get; set; }

    public EnrolmentStatus EnrolmentStatus { get; set; }
}

public sealed class StudentView
{
    public long StudentId { get; set; }

    public string AdmissionNo { get; set; } = null!;

    public string FirstName { get; set; } = null!;

    public string? LastName { get; set; }

    public DateOnly DateOfBirth { get; set; }

    public Gender Gender { get; set; }

    public DateOnly AdmissionDate { get; set; }

    public StudentStatus StudentStatus { get; set; }

    public DateOnly? LeavingDate { get; set; }

    public string? BloodGroup { get; set; }

    public string? NationalId { get; set; }

    public long? SourceApplicationId { get; set; }

    public List<StudentGuardianModel> Guardians { get; set; } = [];

    public List<EnrolmentView> Enrolments { get; set; } = [];
}

public sealed class ExamSubjectModel
{
    public long ExamSubjectId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose a subject.")]
    public long SubjectId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose a class.")]
    public long SchoolClassId { get; set; }

    [Range(typeof(decimal), "0.01", "10000", ErrorMessage = "Maximum marks must be between 0.01 and 10000.")]
    public decimal MaxMarks { get; set; }

    [Range(typeof(decimal), "0", "10000", ErrorMessage = "Pass marks must be between 0 and 10000.")]
    public decimal PassMarks { get; set; }
}

public sealed class SaveExamRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose an academic year.")]
    public long AcademicYearId { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public List<ExamSubjectModel> Subjects { get; set; } = [];
}

public sealed class ExamView
{
    public long ExamId { get; set; }

    public long AcademicYearId { get; set; }

    public string Name { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public ExamStatus ExamStatus { get; set; }

    public List<ExamSubjectModel> Subjects { get; set; } = [];
}

public sealed class ExamStatusRequest
{
    public ExamStatus ExamStatus { get; set; }
}

public sealed class MarkRow
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose a student.")]
    public long EnrolmentId { get; set; }

    public int? RollNo { get; set; }

    public string? StudentName { get; set; }

    [Range(typeof(decimal), "0", "10000", ErrorMessage = "Marks must be between 0 and 10000.")]
    public decimal? Marks { get; set; }

    public bool IsAbsent { get; set; }
}

public sealed class SaveMarksRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose an exam subject.")]
    public long ExamSubjectId { get; set; }

    public List<MarkRow> Marks { get; set; } = [];
}

/// <summary>A row the roll and the fee screens read: who is in a section.</summary>
public sealed class RollEntry
{
    public long EnrolmentId { get; set; }

    public long StudentId { get; set; }

    public string AdmissionNo { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public int? RollNo { get; set; }
}

// ---- Parent portal (S9, TK-69) ------------------------------------------------

/// <summary>One of a guardian's children, as the parent portal shows them.</summary>
public sealed class PortalChildView
{
    public long StudentId { get; set; }

    public string StudentName { get; set; } = null!;

    public string AdmissionNo { get; set; } = null!;

    /// <summary>The latest enrolment's class and section, e.g. <c>VI</c> and <c>A</c>; null when never enrolled.</summary>
    public string? ClassName { get; set; }

    public string? SectionName { get; set; }

    public string? AcademicYearCode { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>A published exam's marks for one child.</summary>
public sealed class PortalExamView
{
    public long ExamId { get; set; }

    public string ExamName { get; set; } = null!;

    public string AcademicYearCode { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public List<PortalMarkView> Subjects { get; set; } = [];
}

public sealed class PortalMarkView
{
    public string SubjectName { get; set; } = null!;

    public decimal MaxMarks { get; set; }

    public decimal PassMarks { get; set; }

    /// <summary>Null when not entered or absent.</summary>
    public decimal? Marks { get; set; }

    public bool IsAbsent { get; set; }

    public bool Passed { get; set; }
}
