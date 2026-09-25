using System.ComponentModel.DataAnnotations;
using Fee.Entity.Enums;

namespace Fee.Entity.Models;

// Requests and views for the fee API (S4, TK-64).

public sealed record FeeMessage(string Message);

public sealed class SaveFeeHeadRequest
{
    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(20, ErrorMessage = "Code cannot exceed 20 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public long? IncomeAccountId { get; set; }

    public bool IsRefundable { get; set; }

    [MaxLength(8, ErrorMessage = "SAC cannot exceed 8 characters.")]
    public string? HsnSacCode { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class FeeHeadView
{
    public long FeeHeadId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public long? IncomeAccountId { get; set; }

    public bool IsRefundable { get; set; }

    public string? HsnSacCode { get; set; }

    public bool IsActive { get; set; }
}

public sealed class FeeStructureLineModel
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose a fee head.")]
    public long FeeHeadId { get; set; }

    [Range(typeof(decimal), "0.01", "100000000", ErrorMessage = "The amount must be more than zero.")]
    public decimal Amount { get; set; }

    public FeeFrequency Frequency { get; set; } = FeeFrequency.Monthly;

    [Range(1, 28, ErrorMessage = "The due day must be between 1 and 28.")]
    public int DueDay { get; set; } = 10;
}

public sealed class SaveFeeStructureRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose a school year.")]
    public long AcademicYearId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose a class.")]
    public long SchoolClassId { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [Range(1, 12, ErrorMessage = "The first month must be between 1 and 12.")]
    public int FirstMonth { get; set; } = 6;

    public bool IsActive { get; set; } = true;

    public List<FeeStructureLineModel> Lines { get; set; } = [];
}

public sealed class FeeStructureView
{
    public long FeeStructureId { get; set; }

    public long AcademicYearId { get; set; }

    public long SchoolClassId { get; set; }

    public string Name { get; set; } = null!;

    public int FirstMonth { get; set; }

    public bool IsActive { get; set; }

    public List<FeeStructureLineModel> Lines { get; set; } = [];
}

public sealed class SaveConcessionRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose a student.")]
    public long StudentId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose a fee head.")]
    public long FeeHeadId { get; set; }

    public ConcessionKind ConcessionKind { get; set; } = ConcessionKind.Percent;

    [Range(typeof(decimal), "0.01", "100000000", ErrorMessage = "The concession must be more than zero.")]
    public decimal Value { get; set; }

    [Required(ErrorMessage = "Reason is required.")]
    [MaxLength(200, ErrorMessage = "Reason cannot exceed 200 characters.")]
    public string Reason { get; set; } = null!;

    public DateOnly ValidFrom { get; set; }

    public DateOnly ValidTo { get; set; }

    public bool IsApproved { get; set; }
}

public sealed class ConcessionView
{
    public long FeeConcessionId { get; set; }

    public long StudentId { get; set; }

    public long FeeHeadId { get; set; }

    public ConcessionKind ConcessionKind { get; set; }

    public decimal Value { get; set; }

    public string Reason { get; set; } = null!;

    public DateOnly ValidFrom { get; set; }

    public DateOnly ValidTo { get; set; }

    public bool IsApproved { get; set; }
}

public sealed class GenerateDemandsRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose a fee structure.")]
    public long FeeStructureId { get; set; }

    /// <summary>The period billed, <c>2026-06</c>.</summary>
    [Required(ErrorMessage = "Period is required.")]
    [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$", ErrorMessage = "The period must be a year and month, like 2026-06.")]
    public string PeriodKey { get; set; } = null!;

    public DateOnly DemandDate { get; set; }
}

public sealed class GenerateDemandsResponse
{
    public int Created { get; set; }

    /// <summary>Students already billed for the period: found, not raised again.</summary>
    public int AlreadyRaised { get; set; }

    /// <summary>Students with nothing due in the period, or with no primary guardian.</summary>
    public int Skipped { get; set; }

    public List<string> Notes { get; set; } = [];
}

public sealed class FeeDemandLineView
{
    public long FeeHeadId { get; set; }

    public decimal Amount { get; set; }

    public decimal ConcessionAmount { get; set; }
}

public sealed class FeeDemandView
{
    public long FeeDemandId { get; set; }

    public string? DemandNo { get; set; }

    public long StudentId { get; set; }

    public long EnrolmentId { get; set; }

    public long FeeStructureId { get; set; }

    public string PeriodKey { get; set; } = null!;

    public long ContactId { get; set; }

    public DateOnly DemandDate { get; set; }

    public DateOnly DueDate { get; set; }

    public FeeDocumentStatus DocumentStatus { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public decimal TotalAmount { get; set; }

    public decimal ConcessionAmount { get; set; }

    public decimal NetAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal OpenAmount { get; set; }

    public string? StudentName { get; set; }

    public List<FeeDemandLineView> Lines { get; set; } = [];
}

public sealed class PostDemandsRequest
{
    /// <summary>These drafts, or every draft of the structure and period when empty.</summary>
    public List<long> FeeDemandIds { get; set; } = [];

    public long? FeeStructureId { get; set; }

    [MaxLength(7, ErrorMessage = "Period cannot exceed 7 characters.")]
    public string? PeriodKey { get; set; }
}

public sealed class VoidRequest
{
    [Required(ErrorMessage = "A reason is required to void.")]
    [MaxLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
    public string Reason { get; set; } = null!;
}

public sealed class AllocationModel
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose a demand.")]
    public long FeeDemandId { get; set; }

    [Range(typeof(decimal), "0.01", "100000000", ErrorMessage = "An allocation must be more than zero.")]
    public decimal Amount { get; set; }
}

public sealed class SaveReceiptRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose the guardian.")]
    public long ContactId { get; set; }

    public DateOnly ReceiptDate { get; set; }

    public PaymentMode PaymentMode { get; set; } = PaymentMode.Cash;

    [Range(1, long.MaxValue, ErrorMessage = "Choose the bank or cash account.")]
    public long BankAccountId { get; set; }

    [Range(typeof(decimal), "0.01", "100000000", ErrorMessage = "The amount must be more than zero.")]
    public decimal Amount { get; set; }

    [MaxLength(50, ErrorMessage = "Reference cannot exceed 50 characters.")]
    public string? Reference { get; set; }

    /// <summary>Which demands it settles. Empty settles the oldest open demands first.</summary>
    public List<AllocationModel> Allocations { get; set; } = [];
}

public sealed class FeeReceiptView
{
    public long FeeReceiptId { get; set; }

    public string ReceiptNo { get; set; } = null!;

    public long ContactId { get; set; }

    public DateOnly ReceiptDate { get; set; }

    public PaymentMode PaymentMode { get; set; }

    public long BankAccountId { get; set; }

    public decimal Amount { get; set; }

    public decimal UnallocatedAmount { get; set; }

    public string? Reference { get; set; }

    public FeeDocumentStatus DocumentStatus { get; set; }

    public List<AllocationModel> Allocations { get; set; } = [];
}

// ---- Parent portal (S9, TK-69) ------------------------------------------------

/// <summary>A posted demand as the guardian it is addressed to sees it.</summary>
public sealed class PortalDemandView
{
    public long FeeDemandId { get; set; }

    public string DemandNo { get; set; } = null!;

    public long StudentId { get; set; }

    public string PeriodKey { get; set; } = null!;

    public DateOnly DemandDate { get; set; }

    public DateOnly DueDate { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal ConcessionAmount { get; set; }

    public decimal NetAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal Balance { get; set; }

    public List<PortalDemandLineView> Lines { get; set; } = [];
}

public sealed class PortalDemandLineView
{
    public string FeeHeadName { get; set; } = null!;

    public decimal Amount { get; set; }

    public decimal ConcessionAmount { get; set; }
}

/// <summary>A posted receipt from the guardian, with the demands it settled.</summary>
public sealed class PortalReceiptView
{
    public long FeeReceiptId { get; set; }

    public string ReceiptNo { get; set; } = null!;

    public DateOnly ReceiptDate { get; set; }

    public PaymentMode PaymentMode { get; set; }

    public decimal Amount { get; set; }

    /// <summary>What is kept as the guardian's advance.</summary>
    public decimal UnallocatedAmount { get; set; }

    public List<string> DemandNos { get; set; } = [];
}
