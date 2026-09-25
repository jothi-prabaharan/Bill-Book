using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;
using Sis.Entity.Enums;

namespace Sis.Entity.TableEntities;

public class Exam : OrgScopedEntity
{
    public long ExamId { get; set; }

    public long AcademicYearId { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public ExamStatus ExamStatus { get; set; } = ExamStatus.Planned;

    public ICollection<ExamSubject> Subjects { get; set; } = [];
}

/// <summary>One subject of an exam for one class, with its maximum and pass marks.</summary>
public class ExamSubject : OrgScopedEntity
{
    public long ExamSubjectId { get; set; }

    public long ExamId { get; set; }

    public long SubjectId { get; set; }

    public long SchoolClassId { get; set; }

    public decimal MaxMarks { get; set; }

    public decimal PassMarks { get; set; }
}

/// <summary>A student's marks in one exam subject. Null marks with IsAbsent.</summary>
public class ExamMark : OrgScopedEntity
{
    public long ExamMarkId { get; set; }

    public long ExamSubjectId { get; set; }

    public long EnrolmentId { get; set; }

    public decimal? Marks { get; set; }

    public bool IsAbsent { get; set; }
}
