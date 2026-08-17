using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>acc.JournalDetails</c>, read-only. The lines of a manual journal, in both
/// the document's currency and the base one.
/// </summary>
public class JournalDetailRead : OrgScopedEntity
{
    public long JournalDetailId { get; set; }

    public long JournalId { get; set; }

    public int LineNumber { get; set; }

    public long AccountId { get; set; }

    public long? SubAccountId { get; set; }

    public decimal DebitAmount { get; set; }

    public decimal CreditAmount { get; set; }

    public decimal DebitAmountBase { get; set; }

    public decimal CreditAmountBase { get; set; }

    public string? LineMemo { get; set; }
}
