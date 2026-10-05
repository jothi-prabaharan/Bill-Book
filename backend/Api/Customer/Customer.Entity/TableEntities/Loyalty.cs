using System.ComponentModel.DataAnnotations;
using Customer.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Customer.Entity.TableEntities;

/// <summary>
/// A contact's loyalty points in one branch (TK-166). One row per contact;
/// <see cref="PointsBalance"/> is the sum of its transactions, kept so a till
/// reads a balance without adding up history.
/// </summary>
public class LoyaltyAccount : OrgScopedEntity
{
    public long LoyaltyAccountId { get; set; }

    /// <summary>The contact, in Master's <c>con</c>. No foreign key: another service's table.</summary>
    public long ContactId { get; set; }

    public int PointsBalance { get; set; }
}

/// <summary>
/// One movement of a contact's points (TK-166), signed: earned is positive,
/// reversed and redeemed negative. A document earns once and is reversed
/// once, which a unique index on (kind, source, source id) holds.
/// </summary>
public class LoyaltyTransaction : OrgScopedEntity
{
    public long LoyaltyTransactionId { get; set; }

    public long LoyaltyAccountId { get; set; }

    public LoyaltyTransactionKind Kind { get; set; }

    public int Points { get; set; }

    public LoyaltySource? Source { get; set; }

    public long? SourceId { get; set; }

    [MaxLength(30, ErrorMessage = "A document number can be at most 30 characters.")]
    public string? DocumentNo { get; set; }

    /// <summary>When it happened, in branch local time.</summary>
    public DateTime OccurredAt { get; set; }

    [MaxLength(250, ErrorMessage = "A note can be at most 250 characters.")]
    public string? Note { get; set; }
}
