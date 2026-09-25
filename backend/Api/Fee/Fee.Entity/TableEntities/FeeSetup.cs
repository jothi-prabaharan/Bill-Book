using System.ComponentModel.DataAnnotations;
using Fee.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Fee.Entity.TableEntities;

/// <summary>
/// What a fee is for: tuition, transport, exam. Posts to its own income
/// account, or to Fee Income when it names none; a refundable head (a caution
/// deposit) posts to Refundable Deposits instead, since it is owed back.
/// </summary>
public class FeeHead : OrgScopedEntity
{
    public long FeeHeadId { get; set; }

    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(20, ErrorMessage = "Code cannot exceed 20 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    /// <summary>Unenforced: <c>acc.Accounts</c>, an Income account (a Liability for a refundable head), checked through Accounting.</summary>
    public long? IncomeAccountId { get; set; }

    public bool IsRefundable { get; set; }

    /// <summary>SAC for the rare taxable head. School education is exempt, so this is stored and not yet taxed.</summary>
    [MaxLength(8, ErrorMessage = "SAC cannot exceed 8 characters.")]
    public string? HsnSacCode { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>What a class pays in a year: <c>Day scholar</c>, <c>Hosteller</c>.</summary>
public class FeeStructure : OrgScopedEntity
{
    public long FeeStructureId { get; set; }

    /// <summary>Unenforced: <c>sis.AcademicYears</c>.</summary>
    public long AcademicYearId { get; set; }

    /// <summary>Unenforced: <c>sis.SchoolClasses</c>.</summary>
    public long SchoolClassId { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    /// <summary>The month the school year starts, 1–12, from which frequencies are counted.</summary>
    [Range(1, 12, ErrorMessage = "The first month must be between 1 and 12.")]
    public int FirstMonth { get; set; } = 6;

    public bool IsActive { get; set; } = true;

    public ICollection<FeeStructureLine> Lines { get; set; } = [];
}

public class FeeStructureLine : OrgScopedEntity
{
    public long FeeStructureLineId { get; set; }

    public long FeeStructureId { get; set; }

    public long FeeHeadId { get; set; }

    public decimal Amount { get; set; }

    public FeeFrequency Frequency { get; set; } = FeeFrequency.Monthly;

    /// <summary>The day of the period the fee falls due, 1–28.</summary>
    [Range(1, 28, ErrorMessage = "The due day must be between 1 and 28.")]
    public int DueDay { get; set; } = 10;
}

/// <summary>A reduction for one student on one head, for a while. Applied only once approved.</summary>
public class FeeConcession : OrgScopedEntity
{
    public long FeeConcessionId { get; set; }

    /// <summary>Unenforced: <c>sis.Students</c>.</summary>
    public long StudentId { get; set; }

    public long FeeHeadId { get; set; }

    public ConcessionKind ConcessionKind { get; set; } = ConcessionKind.Percent;

    public decimal Value { get; set; }

    [Required(ErrorMessage = "Reason is required.")]
    [MaxLength(200, ErrorMessage = "Reason cannot exceed 200 characters.")]
    public string Reason { get; set; } = null!;

    public DateOnly ValidFrom { get; set; }

    public DateOnly ValidTo { get; set; }

    public bool IsApproved { get; set; }
}
