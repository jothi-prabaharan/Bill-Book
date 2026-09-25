using System.ComponentModel.DataAnnotations;
using Fee.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Fee.Entity.TableEntities;

/// <summary>
/// The fee invoice: one student, one period of one structure, billed to the
/// primary guardian as they were when it was raised. Numbered from FDM,
/// gaplessly. Posted, it is never edited; it is voided while nothing has been
/// paid against it.
/// </summary>
public class FeeDemand : OrgScopedEntity
{
    public long FeeDemandId { get; set; }

    [MaxLength(30, ErrorMessage = "Demand number cannot exceed 30 characters.")]
    public string? DemandNo { get; set; }

    /// <summary>Unenforced: <c>sis.Students</c>.</summary>
    public long StudentId { get; set; }

    /// <summary>Unenforced: <c>sis.Enrolments</c>.</summary>
    public long EnrolmentId { get; set; }

    public long FeeStructureId { get; set; }

    /// <summary>The period billed, <c>2026-06</c>. With the enrolment and structure, the key that makes a run idempotent.</summary>
    [Required(ErrorMessage = "Period is required.")]
    [MaxLength(7, ErrorMessage = "Period cannot exceed 7 characters.")]
    public string PeriodKey { get; set; } = null!;

    /// <summary>Unenforced: the primary guardian in <c>con.Contacts</c>, snapshotted when raised.</summary>
    public long ContactId { get; set; }

    public DateOnly DemandDate { get; set; }

    public DateOnly DueDate { get; set; }

    public FeeDocumentStatus DocumentStatus { get; set; } = FeeDocumentStatus.Draft;

    [Required(ErrorMessage = "Currency is required.")]
    [MaxLength(3, ErrorMessage = "Currency cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = "INR";

    /// <summary>Snapshot at the demand date; never looked up again.</summary>
    public decimal ExchangeRate { get; set; } = 1m;

    public decimal TotalAmount { get; set; }

    public decimal ConcessionAmount { get; set; }

    public decimal NetAmount { get; set; }

    /// <summary>What receipts have allocated to it. Moved only by a guarded update, never past NetAmount.</summary>
    public decimal PaidAmount { get; set; }

    [MaxLength(500, ErrorMessage = "Void reason cannot exceed 500 characters.")]
    public string? VoidReason { get; set; }

    public ICollection<FeeDemandLine> Lines { get; set; } = [];
}

public class FeeDemandLine : OrgScopedEntity
{
    public long FeeDemandLineId { get; set; }

    public long FeeDemandId { get; set; }

    public long FeeHeadId { get; set; }

    public decimal Amount { get; set; }

    public decimal ConcessionAmount { get; set; }
}

/// <summary>Money received from a guardian, numbered from FRC. What it does not settle is held as an advance.</summary>
public class FeeReceipt : OrgScopedEntity
{
    public long FeeReceiptId { get; set; }

    [Required(ErrorMessage = "Receipt number is required.")]
    [MaxLength(30, ErrorMessage = "Receipt number cannot exceed 30 characters.")]
    public string ReceiptNo { get; set; } = null!;

    /// <summary>Unenforced: <c>con.Contacts</c>.</summary>
    public long ContactId { get; set; }

    public DateOnly ReceiptDate { get; set; }

    public PaymentMode PaymentMode { get; set; } = PaymentMode.Cash;

    /// <summary>Unenforced: <c>acc.BankAccounts</c>, where the money landed.</summary>
    public long BankAccountId { get; set; }

    public decimal Amount { get; set; }

    /// <summary>What was not allocated to a demand: held as the guardian's advance.</summary>
    public decimal UnallocatedAmount { get; set; }

    [MaxLength(50, ErrorMessage = "Reference cannot exceed 50 characters.")]
    public string? Reference { get; set; }

    [Required(ErrorMessage = "Currency is required.")]
    [MaxLength(3, ErrorMessage = "Currency cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = "INR";

    public FeeDocumentStatus DocumentStatus { get; set; } = FeeDocumentStatus.Posted;

    [MaxLength(500, ErrorMessage = "Void reason cannot exceed 500 characters.")]
    public string? VoidReason { get; set; }

    public ICollection<FeeReceiptAllocation> Allocations { get; set; } = [];
}

public class FeeReceiptAllocation : OrgScopedEntity
{
    public long FeeReceiptAllocationId { get; set; }

    public long FeeReceiptId { get; set; }

    public long FeeDemandId { get; set; }

    public decimal Amount { get; set; }
}
