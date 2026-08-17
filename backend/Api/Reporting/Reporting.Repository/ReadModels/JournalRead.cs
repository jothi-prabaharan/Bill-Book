#pragma warning disable CS8618
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>acc.Journals</c>, read-only. The manual journal header.
///
/// The Journal Report reads this rather than <c>JournalLedger</c> because it
/// reports on the <b>document</b> — including drafts, which have no ledger rows
/// at all. A journal reaches the ledger only when it is posted.
///
/// The audit columns come from <c>AuditableEntity</c> on the base class:
/// <c>CreatedBy</c>, <c>CreatedAt</c>, <c>ModifiedBy</c>, <c>ModifiedAt</c>. The
/// user ids resolve against <c>mst.Users</c> in another database, so they are
/// resolved in C# and <b>batched</b> — a 200-row page must not be 200 lookups.
/// </summary>
public class JournalRead : OrgScopedEntity
{
    public long JournalId { get; set; }

    public string? JournalNo { get; set; }

    public DateOnly JournalDate { get; set; }

    public string CurrencyCode { get; set; }
    public decimal ExchangeRate { get; set; }

    public string? Reference { get; set; }

    public string? Memo { get; set; }

    public string TransactionTypeCode { get; set; }
    /// <summary>Matches <c>JournalStatus</c> in Accounting: draft, posted, reversed.</summary>
    public int Status { get; set; }

    public DateTimeOffset? PostedAt { get; set; }

    public Guid? PostedBy { get; set; }

    public long? ReversesJournalId { get; set; }

    public long? ReversedByJournalId { get; set; }
}


