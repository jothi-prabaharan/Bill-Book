using System.Linq.Expressions;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public sealed class BalanceSheetSource : ReportSource<BalanceSheetRow>
{
    private readonly BatchedNameResolver _resolver;

    public BalanceSheetSource(BatchedNameResolver resolver)
    {
        _resolver = resolver;
    }

    public override string ReportKey => "balance-sheet";

    public override string Title => "Balance Sheet";

    public override ReportModule Module => ReportModule.Accounting;

    public override string RequiredPermission => "accounting.view";

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new() { Name = "asOf", Label = "As Of", DataType = ColumnDataType.Date },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<BalanceSheetRow, string?>(
            "accountType", ColumnDataType.Text, r => r.AccountType, groupable: true),

        ReportColumn.Of<BalanceSheetRow, string>(
            "accountName", ColumnDataType.Text, r => r.AccountName, groupable: true),

        ReportColumn.Of<BalanceSheetRow, decimal>(
            "balance", ColumnDataType.Money, r => r.Balance, AggregateFunction.Sum),

        ReportColumn.Of<BalanceSheetRow, long>(
            "accountId", ColumnDataType.Number, r => r.AccountId, filterable: false),
    ];

    protected override IQueryable<BalanceSheetRow> Build(
        ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly? asOf = parameters.Date("asOf");

        return from a in db.Accounts
               where a.IsActive && (a.AccountTypeId == 1 || a.AccountTypeId == 2 || a.AccountTypeId == 3)
               let legs = db.Ledger.Where(l =>
                   l.AccountId == a.AccountId
                   && (asOf == null || l.LedgerDate <= asOf))
               select new BalanceSheetRow
               {
                   AccountId = a.AccountId,
                   AccountTypeId = a.AccountTypeId,
                   AccountName = a.AccountName,
                   Balance = legs.Sum(l => l.DebitAmountBase) - legs.Sum(l => l.CreditAmountBase),
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<BalanceSheetRow, long>>)(r => r.AccountId);

    protected override async Task FormatRowsAsync(
        IReadOnlyList<BalanceSheetRow> page, CancellationToken ct)
    {
        Dictionary<int, string> types = await _resolver.GetAccountTypeNamesAsync(ct);

        foreach (BalanceSheetRow row in page)
        {
            if (types.TryGetValue(row.AccountTypeId, out string? typeName))
            {
                row.AccountType = typeName;
            }
        }
    }
}

public sealed class BalanceSheetRow
{
    public long AccountId { get; set; }

    public int AccountTypeId { get; set; }

    public string? AccountType { get; set; }

    public string AccountName { get; set; } = null!;

    public decimal Balance { get; set; }
}
