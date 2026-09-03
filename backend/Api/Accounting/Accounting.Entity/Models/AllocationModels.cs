using System.ComponentModel.DataAnnotations;

namespace Accounting.Entity.Models;

/// <summary>
/// Allocates one document (the source) against another (the target): a credit
/// note against the invoice it settles, a debit note against the bill it
/// corrects. The row records the claim so the target's outstanding balance is
/// not allocated twice.
///
/// <b>How the target's balance is read.</b> A document's receivable or payable
/// is its CONTROL legs in the ledger — the one leg type that is AR, AP, bank or
/// cash. The amount still available is that net, in base currency, minus what
/// has already been allocated. Nothing here invents a balance; it reads the
/// ledger and refuses when the claim exceeds what is left.
///
/// <b>Replace, never append.</b> The key is (source, target): posting the same
/// note against the same invoice again replaces the earlier row rather than
/// doubling it, which is what makes a retry after a dropped response safe and a
/// void-and-repost clean.
/// </summary>
public class AllocateTransactionRequest
{
    /// <summary>Which database. Carried in the body like <c>PostLedgerRequest</c>.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Which branch's books. The allocation row is stamped with it.</summary>
    public Guid OrgId { get; set; }

    [Required(ErrorMessage = "Source transaction type code is required.")]
    [MaxLength(3, ErrorMessage = "Source transaction type code must be a 3-letter code.")]
    public string SourceTransactionTypeCode { get; set; } = null!;

    public long SourceTransactionId { get; set; }

    [Required(ErrorMessage = "Target transaction type code is required.")]
    [MaxLength(3, ErrorMessage = "Target transaction type code must be a 3-letter code.")]
    public string TargetTransactionTypeCode { get; set; } = null!;

    public long TargetTransactionId { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "An allocation must be a positive amount.")]
    public decimal Amount { get; set; }

    /// <summary>
    /// The date the allocation is effective in the books. Unset means today — a
    /// caller that does not care about the period should not have to name one.
    /// </summary>
    public DateOnly? AllocationDate { get; set; }

    [MaxLength(300, ErrorMessage = "Notes cannot exceed 300 characters.")]
    public string? Notes { get; set; }
}

/// <summary>
/// What a void sends: take every allocation a source document made. Removing
/// by the source, because a voided credit note withdraws its claims from every
/// invoice it named.
/// </summary>
public class RemoveAllocationsRequest
{
    /// <summary>Which database. Carried in the body like <c>PostLedgerRequest</c>.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Which branch's books. The rows being removed are scoped to it.</summary>
    public Guid OrgId { get; set; }

    [Required(ErrorMessage = "Source transaction type code is required.")]
    [MaxLength(3, ErrorMessage = "Source transaction type code must be a 3-letter code.")]
    public string SourceTransactionTypeCode { get; set; } = null!;

    public long SourceTransactionId { get; set; }
}

/// <summary>What came back from an allocation attempt.</summary>
public enum AllocationOutcome
{
    Ok = 0,

    /// <summary>Transient — the write raced another allocation to the same target.</summary>
    Retry = 1,

    /// <summary>Refused. Retrying unchanged will be refused again.</summary>
    Refused = 2,
}

public sealed record AllocationResult(AllocationOutcome Outcome, string? Message = null);

/// <summary>
/// The user-facing payload to allocate one document against another.
/// Tenant context is drawn from the caller's token rather than the body.
/// </summary>
public class CreateAllocationDto
{
    [Required(ErrorMessage = "Source transaction type code is required.")]
    [MaxLength(3, ErrorMessage = "Source transaction type code must be a 3-letter code.")]
    public string SourceTransactionTypeCode { get; set; } = null!;

    public long SourceTransactionId { get; set; }

    [Required(ErrorMessage = "Target transaction type code is required.")]
    [MaxLength(3, ErrorMessage = "Target transaction type code must be a 3-letter code.")]
    public string TargetTransactionTypeCode { get; set; } = null!;

    public long TargetTransactionId { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "An allocation must be a positive amount.")]
    public decimal Amount { get; set; }

    /// <summary>The date the allocation is effective in the books. Unset means today.</summary>
    public DateOnly? AllocationDate { get; set; }

    [MaxLength(300, ErrorMessage = "Notes cannot exceed 300 characters.")]
    public string? Notes { get; set; }
}

/// <summary>Why an allocation is being released. A void always says why.</summary>
public class VoidAllocationDto
{
    [Required(ErrorMessage = "A reason is required to void an allocation.")]
    [MaxLength(300, ErrorMessage = "Void reason cannot exceed 300 characters.")]
    public string Reason { get; set; } = null!;
}

/// <summary>One row of the allocation list. Deliberately lightweight — the list screen shows no balances.</summary>
public class AllocationListItemDto
{
    public long TransactionRatioId { get; set; }

    public string SourceTransactionTypeCode { get; set; } = null!;

    public long SourceTransactionId { get; set; }

    public string TargetTransactionTypeCode { get; set; } = null!;

    public long TargetTransactionId { get; set; }

    public decimal Amount { get; set; }

    public DateOnly AllocationDate { get; set; }

    public bool IsVoided { get; set; }

    public string? Notes { get; set; }
}

/// <summary>A page of allocations, and how many there were in total.</summary>
public class AllocationPageDto
{
    public List<AllocationListItemDto> Items { get; set; } = [];

    /// <summary>What matched, not what fitted on the page.</summary>
    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}

/// <summary>
/// One allocation in full, with the live balance either end of it still carries.
/// The balances are read at request time rather than stored, so a figure here is
/// never a stale copy of the ledger.
/// </summary>
public class AllocationDetailDto : AllocationListItemDto
{
    public DateTime AllocatedAt { get; set; }

    public DateTimeOffset? VoidedAt { get; set; }

    public string? VoidReason { get; set; }

    /// <summary>What the target document was posted for, from its CONTROL legs.</summary>
    public decimal TargetPostedAmount { get; set; }

    /// <summary>What every live allocation against the target claims, this one included.</summary>
    public decimal TargetAllocatedAmount { get; set; }

    /// <summary>What is still free to allocate against the target.</summary>
    public decimal TargetAvailableAmount { get; set; }
}

/// <summary>
/// Settlement state, derived rather than stored. A document's status comes from
/// what its ledger says it was posted for against what has been allocated to it,
/// so nothing has to be written back to Sales or Purchase to keep it true.
/// </summary>
public enum SettlementStatus
{
    /// <summary>Nothing allocated against it yet.</summary>
    Unallocated = 0,

    /// <summary>Some, but not all, of the balance is claimed.</summary>
    PartiallyPaid = 1,

    /// <summary>Fully claimed. Nothing left to allocate.</summary>
    Paid = 2,
}

/// <summary>
/// One document a contact has open, on either side of an allocation: a credit
/// waiting to be applied, or a balance waiting to be settled.
/// </summary>
public class OpenDocumentDto
{
    public string TransactionTypeCode { get; set; } = null!;

    public long TransactionId { get; set; }

    public string DocumentNo { get; set; } = null!;

    public DateOnly DocumentDate { get; set; }

    /// <summary>What the document was posted for, from its CONTROL legs, unsigned.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>What live allocations have already claimed against it.</summary>
    public decimal AllocatedAmount { get; set; }

    /// <summary>What is still free — the most this document can take or give.</summary>
    public decimal UnallocatedAmount { get; set; }

    public SettlementStatus SettlementStatus { get; set; }
}

/// <summary>
/// Both halves of a contact's allocation workspace: what they have to give, and
/// what they owe. Split by the direction the CONTROL balance runs rather than by
/// document type, so a source is anything carrying a credit and a target is
/// anything carrying a debit — which is what makes this work for a customer and
/// a vendor without two endpoints.
/// </summary>
public class OpenDocumentsDto
{
    public long ContactId { get; set; }

    /// <summary>Credits available to apply — advances, overpayments, credit and debit notes.</summary>
    public List<OpenDocumentDto> Sources { get; set; } = [];

    /// <summary>Balances waiting to be settled — invoices, bills.</summary>
    public List<OpenDocumentDto> Targets { get; set; } = [];

    /// <summary>The sum of what the targets still owe.</summary>
    public decimal TotalOutstanding { get; set; }

    /// <summary>The sum of the credit still free to apply.</summary>
    public decimal TotalAvailableCredit { get; set; }
}