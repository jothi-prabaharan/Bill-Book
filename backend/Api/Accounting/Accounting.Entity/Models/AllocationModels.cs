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
}