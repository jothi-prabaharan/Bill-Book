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
/// <b>The postings (TK-12).</b> Each is one system journal through
/// <see cref="JournalService.PostSystemAsync"/>, posted in the same scope as the
/// register change it belongs to, so the two never disagree:
/// <list type="bullet">
/// <item><b>Bought on a bill</b> (D-19): the bill already debited the shared
/// <c>Fixed Asset</c> account, so the register only <i>reclassifies</i> —
/// Dr the category's asset account, Cr <c>Fixed Asset</c>. Debiting the asset
/// again would carry it twice.</item>
/// <item><b>Migrated</b> — no bill behind it (D-19): Dr the category's asset
/// account, Cr <c>Opening Balance Equity</c>. It arrives at the value entered
/// and skips historical depreciation.</item>
/// <item><b>Disposed of</b> (D-20): the accumulated depreciation written back,
/// the asset removed at cost, the proceeds received — into a bank or cash
/// account, or already receivable on a sales invoice to the buyer — and the
/// difference to <c>Gain/Loss on Asset Disposal</c>.</item>
/// </list>
/// </summary>
public sealed class FixedAssetService
{
    /// <summary><c>mst.LedgerSources</c> 12 — Journal. The register raises JRN entries (D-09: no new transaction codes).</summary>
    private const int JournalLedgerSource = 12;

    /// <summary><c>mst.LedgerTypes</c> 1 — ITEM: an invoice's sale legs, the ones a disposal invoice's proceeds are read from.</summary>
    private const int ItemLedgerType = 1;

    private readonly AccountingDbContext _db;
    private readonly JournalService _journals;

    public FixedAssetService(AccountingDbContext db, JournalService journals)
    {
        _db = db;
        _journals = journals;
    }

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

    /// <summary>
    /// Puts an asset on the register by hand, with its Books and Tax schedules,
    /// and posts its acquisition: a reclassification from <c>Fixed Asset</c> when
    /// it names the bill that bought it, an opening entry against Opening Balance
    /// Equity when it names none.
    /// </summary>
    public async Task<FixedAssetResult> RegisterAsync(CreateFixedAssetRequest request, CancellationToken ct)
    {
        FixedAssetOutcome invalid = await ValidateAsync(
            request.FixedAssetCategoryId, request.AssetCode, request.PurchaseBillId, ct);

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

        _db.DepreciationSchedules.AddRange(ToSchedules(asset.FixedAssetId, request.Schedules));
        await _db.SaveChangesAsync(ct);

        FixedAssetResult posted = await PostAcquisitionAsync([asset], request.PurchaseDate, ct);
        if (posted.Outcome != FixedAssetOutcome.Ok)
        {
            return posted;
        }

        await tx.CommitAsync(ct);
        return new FixedAssetResult(FixedAssetOutcome.Ok, asset.FixedAssetId, JournalId: posted.JournalId);
    }

    /// <summary>
    /// Puts an asset bought on a bill on the register, in service, by hand — for
    /// a bill posted before bills did this themselves. Reclassifies its cost out
    /// of <c>Fixed Asset</c>. Refused when the bill already registered its
    /// assets, which would reclassify the same cost twice.
    /// </summary>
    public async Task<FixedAssetResult> CapitalizeAsync(CapitalizeAssetRequest request, CancellationToken ct)
    {
        FixedAssetOutcome invalid = await ValidateAsync(
            request.FixedAssetCategoryId, request.AssetCode, request.PurchaseBillId, ct);

        if (invalid != FixedAssetOutcome.Ok)
        {
            return new FixedAssetResult(invalid);
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

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

        FixedAssetResult posted = await PostAcquisitionAsync([asset], request.PurchaseDate, ct);
        if (posted.Outcome != FixedAssetOutcome.Ok)
        {
            return posted;
        }

        await tx.CommitAsync(ct);
        return new FixedAssetResult(FixedAssetOutcome.Ok, asset.FixedAssetId, JournalId: posted.JournalId);
    }

    /// <summary>
    /// A posted bill's capital lines, each put on the register in service, with
    /// one journal reclassifying them all out of <c>Fixed Asset</c> (D-19).
    ///
    /// <b>Idempotent per bill line.</b> Purchase calls this after its ledger
    /// posting, and a retried bill post calls it again; a line already on the
    /// register is returned as it is and posted nothing, so each asset is
    /// created, and reclassified, once.
    ///
    /// The asset arrives with no depreciation schedule — a bill line says nothing
    /// about useful life — so it charges nothing until
    /// <see cref="SetSchedulesAsync"/> gives it one.
    /// </summary>
    public async Task<(FixedAssetResult Result, CapitaliseBillResponse? Response)> CapitaliseBillAsync(
        CapitaliseBillRequest request, CancellationToken ct)
    {
        List<long> lineIds = [.. request.Lines.Select(l => l.BillDetailId)];

        Dictionary<long, long> existing = await _db.FixedAssets
            .Where(a => a.PurchaseBillDetailId != null && lineIds.Contains(a.PurchaseBillDetailId.Value))
            .ToDictionaryAsync(a => a.PurchaseBillDetailId!.Value, a => a.FixedAssetId, ct);

        List<CapitaliseBillLine> fresh = [.. request.Lines.Where(l => !existing.ContainsKey(l.BillDetailId))];

        if (fresh.Count == 0)
        {
            return (new FixedAssetResult(FixedAssetOutcome.Ok),
                new CapitaliseBillResponse([.. request.Lines.Select(l => existing[l.BillDetailId])], null));
        }

        List<long> categoryIds = [.. fresh.Select(l => l.FixedAssetCategoryId).Distinct()];
        int known = await _db.FixedAssetCategories.CountAsync(c => categoryIds.Contains(c.FixedAssetCategoryId), ct);

        if (known != categoryIds.Count)
        {
            return (new FixedAssetResult(
                FixedAssetOutcome.CategoryMissing,
                Detail: "A capital line names a fixed asset category this branch does not have."), null);
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        List<FixedAsset> assets = [.. fresh.Select(l => new FixedAsset
        {
            FixedAssetCategoryId = l.FixedAssetCategoryId,
            AssetCode = BillAssetCode(request.DocumentNo, l.LineNumber),
            AssetName = l.Description.Length > 200 ? l.Description[..200] : l.Description,
            PurchaseDate = request.DocumentDate,
            PurchasePrice = l.Amount,
            PurchaseBillId = request.PurchaseBillId,
            PurchaseBillDetailId = l.BillDetailId,
            Status = FixedAssetStatus.Active,
        })];

        _db.FixedAssets.AddRange(assets);
        await _db.SaveChangesAsync(ct);

        FixedAssetResult posted = await PostAcquisitionAsync(assets, request.DocumentDate, ct);
        if (posted.Outcome != FixedAssetOutcome.Ok)
        {
            return (posted, null);
        }

        await tx.CommitAsync(ct);

        foreach (FixedAsset asset in assets)
        {
            existing[asset.PurchaseBillDetailId!.Value] = asset.FixedAssetId;
        }

        return (new FixedAssetResult(FixedAssetOutcome.Ok, JournalId: posted.JournalId),
            new CapitaliseBillResponse([.. request.Lines.Select(l => existing[l.BillDetailId])], posted.JournalId));
    }

    /// <summary>
    /// Replaces an asset's depreciation schedules. Refused once depreciation has
    /// been charged on them: the charges already posted were worked out from
    /// those schedules, and changing them underneath would leave the ledger
    /// explained by nothing on the register.
    /// </summary>
    public async Task<FixedAssetResult> SetSchedulesAsync(
        long fixedAssetId, SetDepreciationSchedulesRequest request, CancellationToken ct)
    {
        FixedAsset? asset = await _db.FixedAssets
            .FirstOrDefaultAsync(a => a.FixedAssetId == fixedAssetId, ct);

        if (asset is null)
        {
            return new FixedAssetResult(FixedAssetOutcome.NotFound);
        }

        if (!SchedulesAreValid(request.Schedules, asset.PurchasePrice))
        {
            return new FixedAssetResult(FixedAssetOutcome.InvalidSchedule, fixedAssetId);
        }

        bool charged = await _db.AssetTransactions.AnyAsync(
            t => t.FixedAssetId == fixedAssetId && t.TransactionType == AssetTransactionType.Depreciation, ct);

        if (charged)
        {
            return new FixedAssetResult(FixedAssetOutcome.SchedulesInUse, fixedAssetId);
        }

        List<DepreciationSchedule> old = await _db.DepreciationSchedules
            .Where(s => s.FixedAssetId == fixedAssetId)
            .ToListAsync(ct);

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        // Two saves: the unique (asset, type) index would otherwise see the new
        // Books schedule beside the old one it replaces.
        _db.DepreciationSchedules.RemoveRange(old);
        await _db.SaveChangesAsync(ct);

        _db.DepreciationSchedules.AddRange(ToSchedules(fixedAssetId, request.Schedules));
        await _db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return new FixedAssetResult(FixedAssetOutcome.Ok, fixedAssetId);
    }

    /// <summary>
    /// Takes an asset off the books, with the four-leg disposal entry (D-20).
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

        bool toBank = request.ProceedsBankAccountId is not null;
        bool toInvoice = request.SalesInvoiceId is not null;

        if ((toBank && toInvoice) || (!toBank && !toInvoice && request.SaleAmount > 0))
        {
            return new FixedAssetResult(FixedAssetOutcome.ProceedsDestinationRequired, fixedAssetId);
        }

        FixedAssetCategory category = await _db.FixedAssetCategories
            .FirstAsync(c => c.FixedAssetCategoryId == asset.FixedAssetCategoryId, ct);

        long? gainLossAccountId = await SystemAccountIdAsync(SystemAccount.AssetDisposalGainLoss, ct);
        if (gainLossAccountId is null)
        {
            return new FixedAssetResult(
                FixedAssetOutcome.SystemAccountMissing, fixedAssetId,
                $"The chart has no '{SystemAccountNames.Of(SystemAccount.AssetDisposalGainLoss)}' account. "
                    + "Re-run the branch's setup to add it.");
        }

        // What the proceeds debit, and how much they come to.
        List<SaveJournalLineRequest> proceeds = [];
        string memo = $"Disposal of {asset.AssetName}";

        if (toBank)
        {
            long? bankLedger = await _db.BankAccounts
                .Where(b => b.BankAccountId == request.ProceedsBankAccountId)
                .Select(b => b.LedgerAccountId)
                .FirstOrDefaultAsync(ct);

            if (bankLedger is null)
            {
                return new FixedAssetResult(FixedAssetOutcome.ProceedsAccountMissing, fixedAssetId);
            }

            if (request.SaleAmount > 0)
            {
                proceeds.Add(new SaveJournalLineRequest
                {
                    AccountId = bankLedger.Value,
                    DebitAmount = request.SaleAmount,
                    LineMemo = memo,
                });
            }
        }
        else if (toInvoice)
        {
            // The invoice already debited the buyer's receivable and credited its
            // lines before GST. Those credits are the proceeds, and they are not
            // trading income — so the disposal takes them back off the accounts
            // they landed on and carries them into the gain or loss instead.
            proceeds = await _db.JournalLedger
                .Where(l => (l.TransactionTypeCode == "INV" || l.TransactionTypeCode == "POS")
                    && l.TransactionId == request.SalesInvoiceId
                    && l.LedgerTypeId == ItemLedgerType
                    && l.CreditAmountBase > 0)
                .GroupBy(l => new { l.AccountId, l.SubAccountId })
                .Select(g => new SaveJournalLineRequest
                {
                    AccountId = g.Key.AccountId,
                    SubAccountId = g.Key.SubAccountId,
                    DebitAmount = g.Sum(l => l.CreditAmountBase),
                    LineMemo = memo,
                })
                .ToListAsync(ct);

            if (proceeds.Count == 0)
            {
                return new FixedAssetResult(FixedAssetOutcome.InvoiceNotPosted, fixedAssetId);
            }
        }

        decimal fetched = proceeds.Sum(l => l.DebitAmount);

        decimal accumulated = await _db.AssetTransactions
            .Where(t => t.FixedAssetId == fixedAssetId
                && t.TransactionType == AssetTransactionType.Depreciation
                && t.JournalId != null)
            .SumAsync(t => t.Amount, ct);

        // Where the cost sits now. An asset put on the register before TK-12 was
        // never reclassified — its cost is still in the shared Fixed Asset
        // account, where every capitalised purchase collected — so that is what
        // the disposal takes it out of.
        bool acquisitionPosted = await _db.AssetTransactions.AnyAsync(
            t => t.FixedAssetId == fixedAssetId
                && t.TransactionType == AssetTransactionType.Acquisition, ct);

        long costAccountId = category.AssetAccountId;

        if (!acquisitionPosted)
        {
            if (await SystemAccountIdAsync(SystemAccount.FixedAsset, ct) is not long holding)
            {
                return new FixedAssetResult(
                    FixedAssetOutcome.SystemAccountMissing, fixedAssetId,
                    $"The chart has no '{SystemAccountNames.Of(SystemAccount.FixedAsset)}' account.");
            }

            costAccountId = holding;
        }

        List<SaveJournalLineRequest> lines = [.. DisposalLines(
            asset.PurchasePrice, accumulated, fetched,
            costAccountId, category.AccumulatedDepreciationAccountId, gainLossAccountId.Value, memo)];

        lines.InsertRange(0, proceeds);

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        SaveJournalResult journal = await _journals.PostSystemAsync(
            new SaveJournalRequest
            {
                JournalDate = request.DisposalDate,
                Reference = $"FA-DISPOSAL {asset.AssetCode}",
                Memo = memo,
                Lines = lines,
            },
            JournalLedgerSource,
            ct);

        if (journal.Outcome != SaveJournalOutcome.Ok)
        {
            return Refused(journal, fixedAssetId);
        }

        asset.Status = FixedAssetStatus.Disposed;

        _db.AssetTransactions.Add(new AssetTransaction
        {
            FixedAssetId = fixedAssetId,
            TransactionType = AssetTransactionType.Disposal,
            TransactionDate = request.DisposalDate,
            Amount = fetched,
            JournalId = journal.JournalId,
            Notes = request.Notes,
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new FixedAssetResult(FixedAssetOutcome.Ok, fixedAssetId, JournalId: journal.JournalId);
    }

    /// <summary>
    /// A disposal's legs other than the proceeds: the accumulated depreciation
    /// written back, the asset removed at cost, and the difference between what
    /// it fetched and what it stood at to gain or loss. Public and pure so the
    /// arithmetic is tested without a database.
    /// </summary>
    public static IEnumerable<SaveJournalLineRequest> DisposalLines(
        decimal cost,
        decimal accumulatedDepreciation,
        decimal proceeds,
        long assetAccountId,
        long accumulatedDepreciationAccountId,
        long gainLossAccountId,
        string memo)
    {
        if (accumulatedDepreciation > 0)
        {
            yield return new SaveJournalLineRequest
            {
                AccountId = accumulatedDepreciationAccountId,
                DebitAmount = accumulatedDepreciation,
                LineMemo = memo,
            };
        }

        yield return new SaveJournalLineRequest
        {
            AccountId = assetAccountId,
            CreditAmount = cost,
            LineMemo = memo,
        };

        decimal gain = proceeds - (cost - accumulatedDepreciation);

        if (gain != 0)
        {
            yield return new SaveJournalLineRequest
            {
                AccountId = gainLossAccountId,
                CreditAmount = gain > 0 ? gain : 0,
                DebitAmount = gain < 0 ? -gain : 0,
                LineMemo = gain > 0 ? $"Gain on {memo.ToLowerInvariant()}" : $"Loss on {memo.ToLowerInvariant()}",
            };
        }
    }

    /// <summary>
    /// One journal for the assets' acquisition: per asset, Dr the category's
    /// asset account and Cr <c>Fixed Asset</c> when a bill bought it, or Cr
    /// Opening Balance Equity when none did. An asset whose category already
    /// points at <c>Fixed Asset</c> has nothing to reclassify and adds no lines.
    /// Each asset gets an <c>Acquisition</c> transaction naming the journal.
    /// </summary>
    private async Task<FixedAssetResult> PostAcquisitionAsync(
        IReadOnlyList<FixedAsset> assets, DateOnly date, CancellationToken ct)
    {
        List<long> categoryIds = [.. assets.Select(a => a.FixedAssetCategoryId).Distinct()];
        Dictionary<long, long> assetAccounts = await _db.FixedAssetCategories
            .Where(c => categoryIds.Contains(c.FixedAssetCategoryId))
            .ToDictionaryAsync(c => c.FixedAssetCategoryId, c => c.AssetAccountId, ct);

        bool anyBought = assets.Any(a => a.PurchaseBillId is not null);
        bool anyMigrated = assets.Any(a => a.PurchaseBillId is null);

        long? holding = anyBought ? await SystemAccountIdAsync(SystemAccount.FixedAsset, ct) : null;
        long? openingEquity = anyMigrated ? await SystemAccountIdAsync(SystemAccount.OpeningBalanceEquity, ct) : null;

        if ((anyBought && holding is null) || (anyMigrated && openingEquity is null))
        {
            SystemAccount missing = anyBought && holding is null
                ? SystemAccount.FixedAsset
                : SystemAccount.OpeningBalanceEquity;

            return new FixedAssetResult(
                FixedAssetOutcome.SystemAccountMissing,
                Detail: $"The chart has no '{SystemAccountNames.Of(missing)}' account.");
        }

        List<SaveJournalLineRequest> lines = [];

        foreach (FixedAsset asset in assets)
        {
            long credit = asset.PurchaseBillId is null ? openingEquity!.Value : holding!.Value;
            long debit = assetAccounts[asset.FixedAssetCategoryId];

            if (debit == credit || asset.PurchasePrice <= 0)
            {
                continue;
            }

            string memo = asset.PurchaseBillId is null
                ? $"Opening value of {asset.AssetName}"
                : $"{asset.AssetName} capitalised";

            lines.Add(new SaveJournalLineRequest { AccountId = debit, DebitAmount = asset.PurchasePrice, LineMemo = memo });
            lines.Add(new SaveJournalLineRequest { AccountId = credit, CreditAmount = asset.PurchasePrice, LineMemo = memo });
        }

        long? journalId = null;

        if (lines.Count > 0)
        {
            SaveJournalResult journal = await _journals.PostSystemAsync(
                new SaveJournalRequest
                {
                    JournalDate = date,
                    Reference = assets.Count == 1
                        ? $"FA-ACQ {assets[0].AssetCode}"
                        : $"FA-ACQ bill {assets[0].PurchaseBillId}",
                    Memo = "Fixed assets put on the register",
                    Lines = lines,
                },
                JournalLedgerSource,
                ct);

            if (journal.Outcome != SaveJournalOutcome.Ok)
            {
                return Refused(journal, null);
            }

            journalId = journal.JournalId;
        }

        _db.AssetTransactions.AddRange(assets.Select(a => new AssetTransaction
        {
            FixedAssetId = a.FixedAssetId,
            TransactionType = AssetTransactionType.Acquisition,
            TransactionDate = date,
            Amount = a.PurchasePrice,
            JournalId = journalId,
        }));

        await _db.SaveChangesAsync(ct);

        return new FixedAssetResult(FixedAssetOutcome.Ok, JournalId: journalId);
    }

    private async Task<FixedAssetOutcome> ValidateAsync(
        long categoryId, string assetCode, long? purchaseBillId, CancellationToken ct)
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

        // A bill that registered its own assets has already reclassified them.
        if (purchaseBillId is long billId
            && await _db.FixedAssets.AnyAsync(
                a => a.PurchaseBillId == billId && a.PurchaseBillDetailId != null, ct))
        {
            return FixedAssetOutcome.AlreadyCapitalised;
        }

        return FixedAssetOutcome.Ok;
    }

    private async Task<long?> SystemAccountIdAsync(SystemAccount account, CancellationToken ct)
    {
        string name = SystemAccountNames.Of(account);

        return await _db.Accounts
            .Where(a => a.AccountSystemName == name)
            .Select(a => (long?)a.AccountId)
            .FirstOrDefaultAsync(ct);
    }

    private static FixedAssetResult Refused(SaveJournalResult journal, long? fixedAssetId) =>
        new(journal.Outcome == SaveJournalOutcome.PeriodClosed
                ? FixedAssetOutcome.PeriodClosed
                : FixedAssetOutcome.PostingRefused,
            fixedAssetId,
            journal.Detail ?? journal.Outcome.ToString());

    /// <summary>A bill-made asset's code: the bill's number and its line, which is unique because the bill number is.</summary>
    public static string BillAssetCode(string documentNo, int lineNumber)
    {
        string code = $"{documentNo}-{lineNumber}";
        return code.Length > 50 ? code[^50..] : code;
    }

    private static IEnumerable<DepreciationSchedule> ToSchedules(
        long fixedAssetId, IEnumerable<CreateDepreciationScheduleRequest> schedules) =>
        schedules.Select(s => new DepreciationSchedule
        {
            FixedAssetId = fixedAssetId,
            ScheduleType = s.ScheduleType,
            DepreciationMethod = s.DepreciationMethod,
            Rate = s.Rate,
            UsefulLifeYears = s.UsefulLifeYears,
            DepreciationStartDate = s.DepreciationStartDate,
            SalvageValue = s.SalvageValue,
        });

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
