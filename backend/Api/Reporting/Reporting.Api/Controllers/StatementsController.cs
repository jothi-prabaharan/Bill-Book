using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Reporting.Api.Services;
using Reporting.Repository.Repositories;
using Reporting.Repository;
using Shared.Kernel.Internal;
using Shared.Kernel.Apps;

namespace Reporting.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("reports")]
[Route("api/statements")]
[RequireBusinessType]
public sealed class StatementsController : ControllerBase
{
    private readonly ReportingDbContext _db;
    private readonly BatchedNameResolverRepository _resolver;

    public StatementsController(ReportingDbContext db, BatchedNameResolverRepository resolver)
    {
        _db = db;
        _resolver = resolver;
    }

    [HttpGet("profit-and-loss")]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> ProfitAndLoss(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken ct)
    {
        var types = await _resolver.GetAccountTypesFullAsync(ct);
        var plTypes = types.Where(t => t.ReportSection == "ProfitAndLoss").ToList();
        var plTypeIds = plTypes.Select(t => t.AccountTypeId).ToHashSet();

        var query = from l in _db.Ledger
                    join a in _db.Accounts on l.AccountId equals a.AccountId
                    where plTypeIds.Contains(a.AccountTypeId)
                    select new { l, a };

        if (fromDate.HasValue) query = query.Where(x => x.l.LedgerDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.l.LedgerDate <= toDate.Value);

        var ledgerRows = await query.ToListAsync(ct);

        var grouped = ledgerRows
            .GroupBy(x => x.a.AccountTypeId)
            .ToDictionary(g => g.Key, g => g.GroupBy(x => new { x.a.AccountId, x.a.AccountName, x.a.AccountCode })
                                          .Select(ag => new
                                          {
                                              ag.Key.AccountId,
                                              ag.Key.AccountName,
                                              ag.Key.AccountCode,
                                              Balance = ag.Sum(x => x.l.CreditAmountBase - x.l.DebitAmountBase)
                                          }).ToList());

        var sections = new List<object>();
        decimal netProfit = 0;
        decimal grossProfit = 0;

        foreach (var type in plTypes.OrderBy(t => t.SortOrder))
        {
            if (!grouped.TryGetValue(type.AccountTypeId, out var accounts))
                continue;

            decimal totalBalance = 0;
            var accountList = new List<object>();

            foreach (var acc in accounts)
            {
                decimal bal = type.NormalBalance == "Credit" ? acc.Balance : -acc.Balance;
                if (bal != 0)
                {
                    totalBalance += bal;
                    accountList.Add(new { acc.AccountId, acc.AccountName, acc.AccountCode, Balance = bal });
                }
            }

            if (type.SystemName == "Income")
            {
                grossProfit += totalBalance;
                netProfit += totalBalance;
            }
            else if (type.SystemName == "Expense")
            {
                grossProfit -= totalBalance; // Technically COGS affects GP, but we simplify for now
                netProfit -= totalBalance;
            }

            sections.Add(new
            {
                type.AccountTypeId,
                type.DisplayName,
                type.SystemName,
                TotalBalance = totalBalance,
                Accounts = accountList.OrderBy(a => ((dynamic)a).AccountCode)
            });
        }

        return Ok(new
        {
            ReportSection = "ProfitAndLoss",
            Sections = sections,
            GrossProfit = grossProfit,
            NetProfit = netProfit
        });
    }

    [HttpGet("inventory-valuation")]
    [RequirePermission("inventory.view")]
    public async Task<IActionResult> InventoryValuation(CancellationToken ct)
    {
        var query = from s in _db.ItemStocks
                    join i in _db.Items on s.ItemId equals i.ItemId
                    where s.QuantityOnHand > 0
                    select new
                    {
                        s.ItemId,
                        i.ItemCode,
                        i.ItemName,
                        s.QuantityOnHand,
                        s.WeightedAverageCost,
                        TotalValue = s.QuantityOnHand * s.WeightedAverageCost
                    };

        var rows = await query.ToListAsync(ct);
        var grandTotal = rows.Sum(r => r.TotalValue);

        return Ok(new
        {
            Rows = rows.OrderBy(r => r.ItemCode),
            GrandTotal = grandTotal
        });
    }
}
