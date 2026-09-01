using System.Linq.Expressions;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public sealed class ProfitAndLossSource : ReportSource<ProfitAndLossRow>
{
    private readonly BatchedNameResolver _resolver;

    public ProfitAndLossSource(BatchedNameResolver resolver)
    {
        _resolver = resolver;
    }

    public override string ReportKey => "profit-and-loss";

    public override string Title => "Profit & Loss";

    public override ReportModule Module => ReportModule.Accounting;

    public override string RequiredPermission => "accounting.view";

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<ProfitAndLossRow, string?>(
            "accountType", ColumnDataType.Text, r => r.AccountType, groupable: true),

        ReportColumn.Of<ProfitAndLossRow, string>(
            "accountName", ColumnDataType.Text, r => r.AccountName, groupable: true),

        ReportColumn.Of<ProfitAndLossRow, decimal>(
            "netAmount", ColumnDataType.Money, r => r.NetAmount, AggregateFunction.Sum),

        ReportColumn.Of<ProfitAndLossRow, long>(
            "accountId", ColumnDataType.Number, r => r.AccountId, filterable: false),
    ];

    protected override IQueryable<ProfitAndLossRow> Build(
        ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly? start = parameters.Date("from");
        DateOnly? end = parameters.Date("to");

        return from a in db.Accounts
               where a.IsActive && (a.AccountTypeId == 4 || a.AccountTypeId == 5)
               let legs = db.Ledger.Where(l =>
                   l.AccountId == a.AccountId
                   && (start == null || l.LedgerDate >= start)
                   && (end == null || l.LedgerDate <= end))
               select new ProfitAndLossRow
               {
                   AccountId = a.AccountId,
                   AccountTypeId = a.AccountTypeId,
                   AccountName = a.AccountName,
                   NetAmount = a.AccountTypeId == 4 
                       ? legs.Sum(l => l.CreditAmountBase) - legs.Sum(l => l.DebitAmountBase)
                       : legs.Sum(l => l.DebitAmountBase) - legs.Sum(l => l.CreditAmountBase),
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<ProfitAndLossRow, long>>)(r => r.AccountId);

    protected override async Task FormatRowsAsync(
        IReadOnlyList<ProfitAndLossRow> page, CancellationToken ct)
    {
        Dictionary<int, string> types = await _resolver.GetAccountTypeNamesAsync(ct);

        foreach (ProfitAndLossRow row in page)
        {
            if (types.TryGetValue(row.AccountTypeId, out string? typeName))
            {
                row.AccountType = typeName;
            }
        }
    }
}

public sealed class ProfitAndLossRow
{
    public long AccountId { get; set; }

    public int AccountTypeId { get; set; }

    public string? AccountType { get; set; }

    public string AccountName { get; set; } = null!;

    public decimal NetAmount { get; set; }
}
