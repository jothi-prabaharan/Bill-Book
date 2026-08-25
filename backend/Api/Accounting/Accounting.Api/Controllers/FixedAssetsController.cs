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

