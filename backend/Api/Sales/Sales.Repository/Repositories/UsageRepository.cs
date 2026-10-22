using Sales.Entity.TableEntities;
using Shared.Kernel.Licensing;

namespace Sales.Repository.Repositories;

/// <summary>
/// What Sales counts towards its plan's monthly limit, and what the branch has
/// used so far this month (D-69, TK-210).
///
/// The count itself is taken by <see cref="DocumentQuotaInterceptor"/> on every
/// insert of a document in <see cref="CountedDocuments"/>; this is the list it is
/// given, and the read behind <c>GET api/sales/usage</c>.
/// </summary>
public sealed class UsageRepository
{
    /// <summary>
    /// Quotes, sales orders, invoices (POS included, which is an invoice) and
    /// credit notes. A delivery challan is not counted, and a conversion counts as
    /// the new document it creates because it inserts one.
    /// </summary>
    public static readonly IReadOnlyList<Type> CountedDocuments =
        [typeof(Quote), typeof(SalesOrder), typeof(Invoice), typeof(CreditNote)];

    private readonly SalesDbContext _db;
    private readonly IPlanLimitProvider _limits;
    private readonly TimeProvider _clock;

    public UsageRepository(SalesDbContext db, IPlanLimitProvider limits, TimeProvider clock)
    {
        _db = db;
        _limits = limits;
        _clock = clock;
    }

    /// <summary>This month's count of Sales documents for the signed-in branch, and its plan's limit.</summary>
    public Task<MonthlyUsageResponse> ReadAsync(CancellationToken ct) =>
        MonthlyUsageReader.ReadAsync(_db, _limits, PlanLimitKind.SalesTransactions, _clock, ct);
}
