using Purchase.Repository.Clients;
using Microsoft.EntityFrameworkCore;
using Purchase.Repository;
using Shared.Kernel.Budgets;
using Shared.Kernel.Tenancy;

namespace Purchase.Repository.Repositories;

/// <summary>
/// After a bill is saved, asks Accounting whether its lines pass the branch's
/// approved budget (TK-109). Each line with an account is sent at its taxable
/// value in base currency; Accounting keeps only expense accounts. The answer is
/// warnings for the form, never a refusal.
/// </summary>
public sealed class BillBudgetCheckRepository
{
    private readonly PurchaseDbContext _db;
    private readonly IBudgetCheckClient _client;
    private readonly ITenantContext _tenant;

    public BillBudgetCheckRepository(PurchaseDbContext db, IBudgetCheckClient client, ITenantContext tenant)
    {
        _db = db;
        _client = client;
        _tenant = tenant;
    }

    public async Task<List<string>> WarningsAsync(long billId, CancellationToken ct)
    {
        if (_tenant.CustomerId is not Guid customerId || _tenant.OrgId is not Guid orgId)
        {
            return [];
        }

        if (!_tenant.AddOns.HasFlag(Shared.Kernel.Apps.AddOn.Budget))
        {
            return [];
        }

        var bill = await _db.Bills.AsNoTracking()
            .Where(b => b.BillId == billId)
            .Select(b => new { b.DocumentDate, b.ExchangeRate })
            .FirstOrDefaultAsync(ct);
        if (bill is null)
        {
            return [];
        }

        List<BudgetSpendLine> lines = await _db.BillDetails.AsNoTracking()
            .Where(l => l.BillId == billId && l.AccountId != null && l.FixedAssetCategoryId == null)
            .Select(l => new BudgetSpendLine
            {
                AccountId = l.AccountId!.Value,
                ProjectId = l.ProjectId,
                Amount = Math.Round(l.TaxableAmount * bill.ExchangeRate, 2),
            })
            .ToListAsync(ct);

        return await _client.WarningsAsync(customerId, orgId, bill.DocumentDate, lines, ct);
    }
}
