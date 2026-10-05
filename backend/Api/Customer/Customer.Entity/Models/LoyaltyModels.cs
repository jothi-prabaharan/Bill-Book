using System.ComponentModel.DataAnnotations;
using Customer.Entity.Enums;

namespace Customer.Entity.Models;

/// <summary>Sales asking Customer to record what a posted bill earned (TK-166). The tenant travels in the body.</summary>
public sealed class EarnLoyaltyRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "A contact is required.")]
    public long ContactId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "The invoice is required.")]
    public long InvoiceId { get; set; }

    [MaxLength(30, ErrorMessage = "A document number can be at most 30 characters.")]
    public string? DocumentNo { get; set; }

    [Range(1, 10_000_000, ErrorMessage = "Points earned must be between 1 and 10,000,000.")]
    public int Points { get; set; }
}

/// <summary>Sales telling Customer a bill was voided, so what it earned is taken back (TK-166).</summary>
public sealed class ReverseLoyaltyRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "The invoice is required.")]
    public long InvoiceId { get; set; }
}

/// <summary>What recording an earn or a reversal did. <c>AlreadyRecorded</c> on a retry that changed nothing.</summary>
public sealed class LoyaltyPostingResult
{
    public long ContactId { get; set; }

    public int Points { get; set; }

    public int Balance { get; set; }

    public bool AlreadyRecorded { get; set; }
}

public sealed class LoyaltyTransactionView
{
    public long LoyaltyTransactionId { get; set; }

    public LoyaltyTransactionKind Kind { get; set; }

    public int Points { get; set; }

    public LoyaltySource? Source { get; set; }

    public long? SourceId { get; set; }

    public string? DocumentNo { get; set; }

    public DateTime OccurredAt { get; set; }

    public string? Note { get; set; }
}

/// <summary>A contact's balance and recent history (TK-166). A contact who never earned has a zero balance and no history.</summary>
public sealed class LoyaltyAccountView
{
    public long ContactId { get; set; }

    public int PointsBalance { get; set; }

    public List<LoyaltyTransactionView> Transactions { get; set; } = [];
}
