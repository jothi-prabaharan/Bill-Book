using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;

namespace Accounting.Api.Services;

/// <summary>
/// The fixed asset register: putting an asset on it, and taking one off.
/// Depreciation is <see cref="DepreciationService"/>'s.
///
/// Every refusal is a <see cref="FixedAssetOutcome"/> for the controller to map,
/// and every check that the database would also make — the unique asset code,
/// one schedule per type — is made here first, so a duplicate is a sentence the
/// user can act on rather than a constraint violation.
///
/// <b>Nothing here posts to the ledger yet.</b> Registering an asset bought on a
/// bill must not debit it a second time, and the reclassification to the
/// category's account and the four legs of a disposal are TK-12 (D-19, D-20).
/// </summary>
public sealed class FixedAssetService
{
    private readonly AccountingDbContext _db;

    public FixedAssetService(AccountingDbContext db) => _db = db;

    public async Task<IReadOnlyList<FixedAssetModel>> ListAsync(CancellationToken ct) =>
        await _db.FixedAssets
            .AsNoTracking()
            .OrderBy(a => a.AssetCode)
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
                a.Status))
            .ToListAsync(ct);

    /// <summary>Puts an asset on the register, with its Books and Tax schedules.</summary>
    public async Task<FixedAssetResult> RegisterAsync(CreateFixedAssetRequest request, CancellationToken ct)
    {
        FixedAssetOutcome invalid = await ValidateAsync(
            request.FixedAssetCategoryId, request.AssetCode, ct);

        if (invalid != FixedAssetOutcome.Ok)
        {
            return new FixedAssetResult(invalid);
        }

        if (!SchedulesAreValid(request.Schedules, request.PurchasePrice))
        {
            return new FixedAssetResult(FixedAssetOutcome.InvalidSchedule);
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        var asset = new FixedAsset
        {
            FixedAssetCategoryId = request.FixedAssetCategoryId,
            AssetCode = request.AssetCode.Trim(),
            AssetName = request.AssetName.Trim(),
            Description = request.Description,
            SerialNumber = request.SerialNumber,
            PurchaseDate = request.PurchaseDate,
            PurchasePrice = request.PurchasePrice,
            PurchaseBillId = request.PurchaseBillId,
            Status = request.Status,
        };

        _db.FixedAssets.Add(asset);
        await _db.SaveChangesAsync(ct);

        _db.DepreciationSchedules.AddRange(request.Schedules.Select(s => new DepreciationSchedule
        {
            FixedAssetId = asset.FixedAssetId,
            ScheduleType = s.ScheduleType,
            DepreciationMethod = s.DepreciationMethod,
            Rate = s.Rate,
            UsefulLifeYears = s.UsefulLifeYears,
            DepreciationStartDate = s.DepreciationStartDate,
            SalvageValue = s.SalvageValue,
        }));

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new FixedAssetResult(FixedAssetOutcome.Ok, asset.FixedAssetId);
    }

    /// <summary>
    /// Puts an asset bought on a bill on the register, in service.
    ///
    /// <b>It deliberately posts nothing, and that is not the gap it looks
    /// like.</b> <c>Purchase.BillService</c> already posted the bill's capital
    /// line to the shared <c>Fixed Asset</c> account, so debiting the asset again
    /// here would carry it twice on the balance sheet, balanced both times and
    /// contradicted by nothing. What is missing is the reclassification to the
    /// category's own asset account, decided by D-19 and built by TK-12.
    /// </summary>
    public async Task<FixedAssetResult> CapitalizeAsync(CapitalizeAssetRequest request, CancellationToken ct)
    {
        FixedAssetOutcome invalid = await ValidateAsync(
            request.FixedAssetCategoryId, request.AssetCode, ct);

        if (invalid != FixedAssetOutcome.Ok)
        {
            return new FixedAssetResult(invalid);
        }

        var asset = new FixedAsset
        {
            FixedAssetCategoryId = request.FixedAssetCategoryId,
            AssetCode = request.AssetCode.Trim(),
            AssetName = request.AssetName.Trim(),
            PurchaseDate = request.PurchaseDate,
            PurchasePrice = request.PurchasePrice,
            PurchaseBillId = request.PurchaseBillId,
            Status = FixedAssetStatus.Active,
        };

        _db.FixedAssets.Add(asset);
        await _db.SaveChangesAsync(ct);

        return new FixedAssetResult(FixedAssetOutcome.Ok, asset.FixedAssetId);
    }

    /// <summary>
    /// Retires an asset and records what it sold for.
    ///
    /// <b>The ledger side is TK-12.</b> A disposal is four legs — the accumulated
    /// depreciation written back, the asset removed at cost, the proceeds, and
    /// the gain or loss — and the proceeds need an account the user picked (D-20:
    /// a bank or cash account, or a sales invoice to the buyer). Until the
    /// request carries one, the disposal is a register event and the books still
    /// hold the asset at cost.
    ///
    /// <b>Another branch's asset is <see cref="FixedAssetOutcome.NotFound"/></b>,
    /// never a refusal that would confirm it exists: the query filter and RLS
    /// hide it from this service too (CLAUDE.md, "When asked to add an endpoint").
    /// </summary>
    public async Task<FixedAssetResult> DisposeAsync(long fixedAssetId, DisposeAssetRequest request, CancellationToken ct)
    {
        FixedAsset? asset = await _db.FixedAssets
            .FirstOrDefaultAsync(a => a.FixedAssetId == fixedAssetId, ct);

        if (asset is null)
        {
            return new FixedAssetResult(FixedAssetOutcome.NotFound);
        }

        if (asset.Status != FixedAssetStatus.Active)
        {
            return new FixedAssetResult(FixedAssetOutcome.NotActive, fixedAssetId);
        }

        if (request.DisposalDate < asset.PurchaseDate)
        {
            return new FixedAssetResult(FixedAssetOutcome.DisposalBeforePurchase, fixedAssetId);
        }

        asset.Status = FixedAssetStatus.Disposed;

        _db.AssetTransactions.Add(new AssetTransaction
        {
            FixedAssetId = fixedAssetId,
            TransactionType = AssetTransactionType.Disposal,
            TransactionDate = request.DisposalDate,
            Amount = request.SaleAmount,
            Notes = request.Notes,
        });

        await _db.SaveChangesAsync(ct);

        return new FixedAssetResult(FixedAssetOutcome.Ok, fixedAssetId);
    }

    private async Task<FixedAssetOutcome> ValidateAsync(long categoryId, string assetCode, CancellationToken ct)
    {
        if (!await _db.FixedAssetCategories.AnyAsync(c => c.FixedAssetCategoryId == categoryId, ct))
        {
            return FixedAssetOutcome.CategoryMissing;
        }

        string code = assetCode.Trim();

        if (await _db.FixedAssets.AnyAsync(a => a.AssetCode == code, ct))
        {
            return FixedAssetOutcome.DuplicateCode;
        }

        return FixedAssetOutcome.Ok;
    }

    /// <summary>
    /// Whether every schedule can charge something, and there is at most one of
    /// each type. Public for its tests, which need no database.
    /// </summary>
    public static bool SchedulesAreValid(IReadOnlyCollection<CreateDepreciationScheduleRequest> schedules, decimal cost)
    {
        if (schedules.Select(s => s.ScheduleType).Distinct().Count() != schedules.Count)
        {
            return false;
        }

        return schedules.All(s =>
            Enum.IsDefined(s.ScheduleType)
            && s.SalvageValue >= 0
            && s.SalvageValue <= cost
            && s.DepreciationMethod switch
            {
                DepreciationMethod.StraightLine => s.UsefulLifeYears > 0 || (s.Rate > 0 && s.Rate <= 100),
                DepreciationMethod.WrittenDownValue => s.Rate > 0 && s.Rate < 100,
                _ => false,
            });
    }
}
