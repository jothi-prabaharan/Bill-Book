using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    public StatementsController(ReportingDbContext db) => _db = db;

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
