using System.Linq.Expressions;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;

namespace Reporting.Api.Services.Sources;

/// <summary>
/// The fixed-asset register read against the ledger, account by account: two
/// rows per account, one saying what the register says the account should hold
/// and one saying what the ledger says it does.
///
/// <b>The accounts are the ones a category names</b> — its asset account and its
/// accumulated depreciation account. Each account appears once per side however
/// many categories share it, because the ledger has one balance per account and
/// splitting it by category would count it once for every category that names it.
/// A row for an asset account fills the cost columns; a row for an accumulated
/// depreciation account fills the depreciation columns; book value is the one
/// less the other, so summing it over one side gives that side's net book value.
///
/// <b>Expect the two sides to disagree today, and read it as the report
/// working.</b> Capitalising and disposing of an asset post nothing (D-19, D-20),
/// and a bill posts its capital line to one shared Fixed Asset account rather than
/// the category's; showing exactly that gap is what a reconciliation is for.
///
/// <b>No column totals.</b> A total over both sides adds the register to the
/// ledger, which is a number that means nothing. Group by Source for each side's
/// figures, or read the pairs.
/// </summary>
public sealed class FixedAssetReconciliationSource : ReportSource<FixedAssetReconciliationRow>
{
    public override string ReportKey => "fixed-asset-reconciliation";

    public override string Title => "Fixed Asset Reconciliation";

    public override ReportModule Module => ReportModule.FixedAssets;

    public override string RequiredPermission => "accounting.view";

    public override IReadOnlyList<ReportParameter> Parameters => FixedAssetPeriod.Parameters;

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<FixedAssetReconciliationRow, string>(
            "accountCode", ColumnDataType.Text, r => r.AccountCode, groupable: true),
        ReportColumn.Of<FixedAssetReconciliationRow, string>(
            "account", ColumnDataType.Text, r => r.Account, groupable: true),
        ReportColumn.Of<FixedAssetReconciliationRow, string>(
            "source", ColumnDataType.Enum, r => r.Source, groupable: true),
        ReportColumn.Of<FixedAssetReconciliationRow, decimal>(
            "openingCost", ColumnDataType.Money, r => r.OpeningCost),
        ReportColumn.Of<FixedAssetReconciliationRow, decimal>(
            "costDebits", ColumnDataType.Money, r => r.CostDebits),
        ReportColumn.Of<FixedAssetReconciliationRow, decimal>(
            "costCredits", ColumnDataType.Money, r => r.CostCredits),
        ReportColumn.Of<FixedAssetReconciliationRow, decimal>(
            "closingCost", ColumnDataType.Money, r => r.ClosingCost),
        ReportColumn.Of<FixedAssetReconciliationRow, decimal>(
            "openingAccumDep", ColumnDataType.Money, r => r.OpeningAccumDep),
        ReportColumn.Of<FixedAssetReconciliationRow, decimal>(
            "accumDepDebits", ColumnDataType.Money, r => r.AccumDepDebits),
        ReportColumn.Of<FixedAssetReconciliationRow, decimal>(
            "accumDepCredits", ColumnDataType.Money, r => r.AccumDepCredits),
        ReportColumn.Of<FixedAssetReconciliationRow, decimal>(
            "closingAccumDep", ColumnDataType.Money, r => r.ClosingAccumDep),
        ReportColumn.Of<FixedAssetReconciliationRow, decimal>(
            "openingBookValue", ColumnDataType.Money, r => r.OpeningBookValue),
        ReportColumn.Of<FixedAssetReconciliationRow, decimal>(
            "closingBookValue", ColumnDataType.Money, r => r.ClosingBookValue),
        ReportColumn.Of<FixedAssetReconciliationRow, long>(
            "accountId", ColumnDataType.Number, r => r.AccountId, filterable: false),
    ];

    protected override IQueryable<FixedAssetReconciliationRow> Build(
        ReportParameters parameters, ReportingDbContext db) =>
        Rows(
            FixedAssetRegister.Rows(db, parameters.Date("from"), parameters.Date("to")),
            db.FixedAssetCategories,
            db.Accounts,
            db.Ledger,
            parameters.Date("from"),
            parameters.Date("to"));

    /// <summary>
    /// Both sides, over the sets rather than the context so the arithmetic can be
    /// tested over lists.
    ///
    /// <b>Built as movements, then totalled.</b> Every asset contributes a cost
    /// movement to its asset account and a depreciation movement to its
    /// accumulated depreciation account; every ledger line contributes to its own
    /// account; and every named account contributes a zero on each side, so an
    /// account with nothing posted still shows both rows. The union is then
    /// grouped by account and side. This shape has no outer join in it, which is
    /// what lets it translate: EF evaluates the null test on an outer-joined
    /// aggregate on the client, and a client projection cannot be unioned.
    ///
    /// <b>Ledger debits and credits keep their own sides</b>: an asset account is
    /// debit-normal, so its cost is debits less credits; accumulated depreciation
    /// is credit-normal, so its balance is credits less debits. On the register
    /// side an addition is the debit a purchase would post, a disposal the credit,
    /// a charge the credit to accumulated depreciation and a disposal's write-back
    /// the debit.
    /// </summary>
    public static IQueryable<FixedAssetReconciliationRow> Rows(
        IQueryable<FixedAssetPeriodRow> register,
        IQueryable<FixedAssetCategoryRead> categories,
        IQueryable<AccountRead> accounts,
        IQueryable<JournalLedgerRead> ledger,
        DateOnly? start,
        DateOnly? end)
    {
        const int registerSide = (int)ReconciliationSide.Register;
        const int ledgerSide = (int)ReconciliationSide.Ledger;

        IQueryable<AccountRead> named = accounts.Where(a => categories.Any(c =>
            c.AssetAccountId == a.AccountId
            || c.AccumulatedDepreciationAccountId == a.AccountId));

        IQueryable<ReconciliationMovement> zeros = named.Select(a => new ReconciliationMovement
        {
            AccountId = a.AccountId,
            Side = registerSide,
            OpeningCost = 0m,
            CostDebits = 0m,
            CostCredits = 0m,
            OpeningAccumDep = 0m,
            AccumDepDebits = 0m,
            AccumDepCredits = 0m,
        });

        IQueryable<ReconciliationMovement> registerCost = register.Select(r =>
            new ReconciliationMovement
            {
                AccountId = r.AssetAccountId,
                Side = registerSide,
                OpeningCost = r.OpeningCost,
                CostDebits = r.AdditionCost,
                CostCredits = r.DisposalCost,
                OpeningAccumDep = 0m,
                AccumDepDebits = 0m,
                AccumDepCredits = 0m,
            });

        IQueryable<ReconciliationMovement> registerAccumDep = register.Select(r =>
            new ReconciliationMovement
            {
                AccountId = r.AccumDepAccountId,
                Side = registerSide,
                OpeningCost = 0m,
                CostDebits = 0m,
                CostCredits = 0m,
                OpeningAccumDep = r.OpeningAccumDep,
                AccumDepDebits = r.DisposalAccumDep,
                AccumDepCredits = r.Depreciation,
            });

        IQueryable<JournalLedgerRead> lines = ledger.Where(l =>
            end == null || l.LedgerDate <= end);

        IQueryable<ReconciliationMovement> ledgerCost =
            from l in lines
            where categories.Any(c => c.AssetAccountId == l.AccountId)
            let before = start != null && l.LedgerDate < start
            select new ReconciliationMovement
            {
                AccountId = l.AccountId,
                Side = ledgerSide,
                OpeningCost = before ? l.DebitAmountBase - l.CreditAmountBase : 0m,
                CostDebits = before ? 0m : l.DebitAmountBase,
                CostCredits = before ? 0m : l.CreditAmountBase,
                OpeningAccumDep = 0m,
                AccumDepDebits = 0m,
                AccumDepCredits = 0m,
            };

        IQueryable<ReconciliationMovement> ledgerAccumDep =
            from l in lines
            where categories.Any(c => c.AccumulatedDepreciationAccountId == l.AccountId)
            let before = start != null && l.LedgerDate < start
            select new ReconciliationMovement
            {
                AccountId = l.AccountId,
                Side = ledgerSide,
                OpeningCost = 0m,
                CostDebits = 0m,
                CostCredits = 0m,
                OpeningAccumDep = before ? l.CreditAmountBase - l.DebitAmountBase : 0m,
                AccumDepDebits = before ? 0m : l.DebitAmountBase,
                AccumDepCredits = before ? 0m : l.CreditAmountBase,
            };

        IQueryable<ReconciliationMovement> ledgerZeros = named.Select(a => new ReconciliationMovement
        {
            AccountId = a.AccountId,
            Side = ledgerSide,
            OpeningCost = 0m,
            CostDebits = 0m,
            CostCredits = 0m,
            OpeningAccumDep = 0m,
            AccumDepDebits = 0m,
            AccumDepCredits = 0m,
        });

        var totals = zeros
            .Concat(ledgerZeros)
            .Concat(registerCost)
            .Concat(registerAccumDep)
            .Concat(ledgerCost)
            .Concat(ledgerAccumDep)
            .GroupBy(m => new { m.AccountId, m.Side })
            .Select(g => new
            {
                g.Key.AccountId,
                g.Key.Side,
                OpeningCost = g.Sum(m => m.OpeningCost),
                CostDebits = g.Sum(m => m.CostDebits),
                CostCredits = g.Sum(m => m.CostCredits),
                OpeningAccumDep = g.Sum(m => m.OpeningAccumDep),
                AccumDepDebits = g.Sum(m => m.AccumDepDebits),
                AccumDepCredits = g.Sum(m => m.AccumDepCredits),
            });

        return
            from t in totals
            join a in named on t.AccountId equals a.AccountId
            let closingCost = t.OpeningCost + t.CostDebits - t.CostCredits
            let closingAccumDep = t.OpeningAccumDep + t.AccumDepCredits - t.AccumDepDebits
            select new FixedAssetReconciliationRow
            {
                AccountId = a.AccountId,
                AccountCode = a.AccountCode,
                Account = a.AccountName,
                Source = t.Side == registerSide
                    ? nameof(ReconciliationSide.Register)
                    : nameof(ReconciliationSide.Ledger),
                SourceOrder = t.Side,
                OpeningCost = t.OpeningCost,
                CostDebits = t.CostDebits,
                CostCredits = t.CostCredits,
                ClosingCost = closingCost,
                OpeningAccumDep = t.OpeningAccumDep,
                AccumDepDebits = t.AccumDepDebits,
                AccumDepCredits = t.AccumDepCredits,
                ClosingAccumDep = closingAccumDep,
                OpeningBookValue = t.OpeningCost - t.OpeningAccumDep,
                ClosingBookValue = closingCost - closingAccumDep,
            };
    }

    /// <summary>Register before ledger within an account, which is what makes a row pair.</summary>
    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<FixedAssetReconciliationRow, long>>)(r => r.AccountId * 2 + r.SourceOrder);
}

/// <summary>
/// One contribution to one account on one side, before totalling. Every branch
/// of the union assigns every field, zero where it moves nothing — EF will not
/// union projections that assign different members.
/// </summary>
public sealed class ReconciliationMovement
{
    public long AccountId { get; set; }

    public int Side { get; set; }

    public decimal OpeningCost { get; set; }

    public decimal CostDebits { get; set; }

    public decimal CostCredits { get; set; }

    public decimal OpeningAccumDep { get; set; }

    public decimal AccumDepDebits { get; set; }

    public decimal AccumDepCredits { get; set; }
}

/// <summary>One account, as one side of the reconciliation states it.</summary>
public sealed class FixedAssetReconciliationRow
{
    public long AccountId { get; set; }

    public string AccountCode { get; set; } = null!;

    public string Account { get; set; } = null!;

    /// <summary>The <see cref="ReconciliationSide"/> name.</summary>
    public string Source { get; set; } = null!;

    /// <summary>The source's value, carried only to order the pair.</summary>
    public int SourceOrder { get; set; }

    public decimal OpeningCost { get; set; }

    public decimal CostDebits { get; set; }

    public decimal CostCredits { get; set; }

    public decimal ClosingCost { get; set; }

    public decimal OpeningAccumDep { get; set; }

    public decimal AccumDepDebits { get; set; }

    public decimal AccumDepCredits { get; set; }

    public decimal ClosingAccumDep { get; set; }

    public decimal OpeningBookValue { get; set; }

    public decimal ClosingBookValue { get; set; }
}
