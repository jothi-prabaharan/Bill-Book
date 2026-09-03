using System;
using System.Linq;
using System.Threading.Tasks;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Controllers;

[ApiController]
[Route("api/accounting/fixed-assets")]
[Authorize]
[RequireModulePermission("accounting")]
public class FixedAssetsController : ControllerBase
{
    private readonly AccountingDbContext _db;
    private readonly ITenantContext _tenant;

    public FixedAssetsController(AccountingDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> GetFixedAssets()
    {
        var assets = await _db.FixedAssets
            .AsNoTracking()
            .Select(a => new FixedAssetModel(
                a.FixedAssetId,
                a.FixedAssetCategoryId,
                a.AssetCode,
                a.AssetName,
                a.Description,
                a.SerialNumber,
                a.PurchaseDate,
                a.PurchasePrice,
                a.PurchaseBillId,
                a.Status
            ))
            .ToListAsync();

        return Ok(assets);
    }

    [HttpPost]
    public async Task<IActionResult> RegisterAsset(CreateFixedAssetRequest request)
    {
        var asset = new FixedAsset
        {
            FixedAssetCategoryId = request.FixedAssetCategoryId,
            AssetCode = request.AssetCode,
            AssetName = request.AssetName,
            Description = request.Description,
            SerialNumber = request.SerialNumber,
            PurchaseDate = request.PurchaseDate,
            PurchasePrice = request.PurchasePrice,
            PurchaseBillId = request.PurchaseBillId,
            Status = request.Status
        };

        _db.FixedAssets.Add(asset);
        await _db.SaveChangesAsync();

        if (request.Schedules != null && request.Schedules.Any())
        {
            var schedules = request.Schedules.Select(s => new DepreciationSchedule
            {
                FixedAssetId = asset.FixedAssetId,
                ScheduleType = s.ScheduleType,
                DepreciationMethod = s.DepreciationMethod,
                Rate = s.Rate,
                UsefulLifeYears = s.UsefulLifeYears,
                DepreciationStartDate = s.DepreciationStartDate,
                SalvageValue = s.SalvageValue
            }).ToList();

            _db.DepreciationSchedules.AddRange(schedules);
            await _db.SaveChangesAsync();
        }

        return Ok(new { asset.FixedAssetId });
    }

    /// <summary>
    /// Puts an asset on the register.
    ///
    /// <b>It deliberately posts nothing, and that is not the gap it looks
    /// like.</b> The asset was bought on a bill, and <c>Purchase.BillService</c>
    /// already posted its capital line to the <c>Fixed Asset</c> account when
    /// that bill was posted — so debiting the asset again here would carry it
    /// twice on the balance sheet, balanced both times and contradicted by
    /// nothing.
    ///
    /// What is genuinely missing is the reclassification the category implies:
    /// the bill posts to one shared <c>Fixed Asset</c> account (its own comment
    /// says the category will own that mapping once this register exists), so
    /// capitalizing should move the cost to <see cref="FixedAssetCategory"/>'s
    /// own asset account. That is a posting decision — which account, and
    /// whether an asset with no <c>PurchaseBillId</c> (a migrated one) instead
    /// debits the asset against Opening Balance Equity — and it is open rather
    /// than merely unwritten. Guessing it wrong doubles an asset, so it is left
    /// for the decision rather than inferred here.
    /// </summary>
    [HttpPost("capitalize")]
    public async Task<IActionResult> CapitalizeAsset(CapitalizeAssetRequest request)
    {
        var asset = new FixedAsset
        {
            FixedAssetCategoryId = request.FixedAssetCategoryId,
            AssetCode = request.AssetCode,
            AssetName = request.AssetName,
            PurchaseDate = request.PurchaseDate,
            PurchasePrice = request.PurchasePrice,
            PurchaseBillId = request.PurchaseBillId,
            Status = FixedAssetStatus.Active
        };

        _db.FixedAssets.Add(asset);
        
        await _db.SaveChangesAsync();

        return Ok(new { asset.FixedAssetId });
    }

    /// <summary>
    /// Retires an asset and records what it sold for.
    ///
    /// <b>The ledger side is not written, and needs a decision before it can
    /// be.</b> A disposal is four legs — the accumulated depreciation written
    /// back, the asset removed at cost, the proceeds received, and whatever is
    /// left recognised as gain or loss — and three of the four are determined
    /// by the category and the asset. The fourth is not: nothing on
    /// <see cref="DisposeAssetRequest"/> says <i>where</i> the proceeds landed,
    /// and choosing a bank account here would put money into an account nobody
    /// picked. Until the request carries that, the disposal is a register
    /// event: the asset is retired and the sale amount recorded, and the books
    /// still hold the asset at cost.
    /// </summary>
    [HttpPost("{id}/dispose")]
    public async Task<IActionResult> DisposeAsset(long id, DisposeAssetRequest request)
    {
        var asset = await _db.FixedAssets.FindAsync(id);
        if (asset == null)
            return NotFound();

        asset.Status = FixedAssetStatus.Disposed;
        
        var transaction = new AssetTransaction
        {
            FixedAssetId = id,
            TransactionType = AssetTransactionType.Disposal,
            TransactionDate = request.DisposalDate,
            Amount = request.SaleAmount,
            Notes = request.Notes
        };
        
        _db.AssetTransactions.Add(transaction);

        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("depreciation-run")]
    public async Task<IActionResult> RunDepreciation([FromQuery] DateOnly runDate, [FromServices] Services.DepreciationService depreciationService)
    {
        await depreciationService.RunDepreciationAsync(runDate, HttpContext.RequestAborted);
        return Ok();
    }
}

