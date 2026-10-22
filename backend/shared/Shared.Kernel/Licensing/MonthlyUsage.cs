using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Licensing;

/// <summary>
/// How many counted documents one branch has created in one calendar month
/// (D-69, TK-210). A Professional or trial plan allows 1,000 Sales documents and,
/// counted apart, 1,000 Purchase documents a month, so Sales and Purchase each
/// keep their own table of these, <c>sal.MonthlyUsages</c> and
/// <c>pur.MonthlyUsages</c>, and each branch has one row a month.
///
/// Only <see cref="DocumentQuotaInterceptor"/> writes it, with a guarded update
/// inside the document's own transaction. Rolling the transaction back gives the
/// places back, and the row is never edited by hand.
///
/// Mapped by the Sales and Purchase contexts and by nothing else, which is why
/// it is not in <see cref="TenantDbContext"/> beside the error log and the edit
/// log: only two schemas count anything.
/// </summary>
public class MonthlyUsage : OrgScopedEntity
{
    public long MonthlyUsageId { get; set; }

    /// <summary>The first day of the month, in India Standard Time. See <see cref="QuotaPeriod"/>.</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>Counted documents created in the month: drafts and voided ones included (D-69).</summary>
    public int DocumentCount { get; set; }
}
