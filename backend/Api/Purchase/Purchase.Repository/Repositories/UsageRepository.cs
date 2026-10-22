using Purchase.Entity.TableEntities;
using Shared.Kernel.Licensing;

namespace Purchase.Repository.Repositories;

/// <summary>
/// What Purchase counts towards its plan's monthly limit, and what the branch has
/// used so far this month (D-69, TK-210). Counted apart from Sales: a branch that
/// has filled its Sales month can still raise its thousand purchase documents.
///
/// The count itself is taken by <see cref="DocumentQuotaInterceptor"/> on every
/// insert of a document in <see cref="CountedDocuments"/>; this is the list it is
/// given, and the read behind <c>GET api/purchase/usage</c>.
/// </summary>
public sealed class UsageRepository
{
    /// <summary>
    /// Purchase orders, goods receipts, bills and debit notes. A conversion
    /// counts as the new document it creates because it inserts one.
    /// </summary>
    public static readonly IReadOnlyList<Type> CountedDocuments =
        [typeof(PurchaseOrder), typeof(GoodsReceipt), typeof(Bill), typeof(DebitNote)];

    private readonly PurchaseDbContext _db;
    private readonly IPlanLimitProvider _limits;
    private readonly TimeProvider _clock;

    public UsageRepository(PurchaseDbContext db, IPlanLimitProvider limits, TimeProvider clock)
    {
        _db = db;
        _limits = limits;
        _clock = clock;
    }

    /// <summary>This month's count of Purchase documents for the signed-in branch, and its plan's limit.</summary>
    public Task<MonthlyUsageResponse> ReadAsync(CancellationToken ct) =>
        MonthlyUsageReader.ReadAsync(_db, _limits, PlanLimitKind.PurchaseTransactions, _clock, ct);
}
