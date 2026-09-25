using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;
using Sis.Entity.Enums;

namespace Sis.Entity.TableEntities;

/// <summary>A school year, <c>2026-27</c>. One is current per branch; a closed one takes no new enrolments or marks.</summary>
public class AcademicYear : OrgScopedEntity
{
    public long AcademicYearId { get; set; }

    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(20, ErrorMessage = "Code cannot exceed 20 characters.")]
    public string Code { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsCurrent { get; set; }

    public bool IsClosed { get; set; }
}

/// <summary>A grade, LKG to XII. <c>Class</c> is a C# keyword, hence the name. Promotion goes to the next SortOrder.</summary>
public class SchoolClass : OrgScopedEntity
{
    public long SchoolClassId { get; set; }

    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(20, ErrorMessage = "Code cannot exceed 20 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>A class in one year: <c>VI-A</c> in 2026-27.</summary>
public class Section : OrgScopedEntity
{
    public long SectionId { get; set; }

    public long AcademicYearId { get; set; }

    public long SchoolClassId { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(20, ErrorMessage = "Name cannot exceed 20 characters.")]
    public string Name { get; set; } = null!;

    /// <summary>Enrolment beyond it is refused, not warned.</summary>
    [Range(1, 1000, ErrorMessage = "Capacity must be between 1 and 1000.")]
    public int? Capacity { get; set; }

    /// <summary>Unenforced: <c>hrm.Employees</c>.</summary>
    public long? ClassTeacherEmployeeId { get; set; }

    /// <summary>Unenforced: <c>fac.Spaces</c>.</summary>
    public long? RoomSpaceId { get; set; }
}

public class Subject : OrgScopedEntity
{
    public long SubjectId { get; set; }

    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(20, ErrorMessage = "Code cannot exceed 20 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public SubjectKind SubjectKind { get; set; } = SubjectKind.Core;

    public bool IsActive { get; set; } = true;
}
