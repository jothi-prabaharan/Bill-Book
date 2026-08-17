using System.Linq.Expressions;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public sealed class AccountMovementSource : ReportSource<AccountMovementRow>
{
    private readonly BatchedNameResolver _resolver;

    public AccountMovementSource(BatchedNameResolver resolver)
    {
        _resolver = resolver;
    }

    public override string ReportKey => "account-movement";

    public override string Title => "Account Movement";

    public override ReportModule Module => ReportModule.Accounting;

    public override string RequiredPermission => "accounting.view";

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new()
        {
            Name = "from",
            Label = "From",
            DataType = ColumnDataType.Date,
        },
        new()
        {
            Name = "to",
            Label = "To",
            DataType = ColumnDataType.Date,
        },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<AccountMovementRow, DateOnly>(
            "date", ColumnDataType.Date, r => r.Date),

        ReportColumn.Of<AccountMovementRow, string>(
            "accountType", ColumnDataType.Text, r => r.AccountType, groupable: true),

        ReportColumn.Of<AccountMovementRow, string>(
            "accountCode", ColumnDataType.Text, r => r.AccountCode, groupable: true),

        ReportColumn.Of<AccountMovementRow, string>(
            "account", ColumnDataType.Text, r => r.Account, groupable: true),

        ReportColumn.Of<AccountMovementRow, decimal>(
            "debit", ColumnDataType.Money, r => r.Debit, AggregateFunction.Sum),

        ReportColumn.Of<AccountMovementRow, decimal>(
            "credit", ColumnDataType.Money, r => r.Credit, AggregateFunction.Sum),

        ReportColumn.Of<AccountMovementRow, string?>(
            "description", ColumnDataType.Text, r => r.Description),

        ReportColumn.Of<AccountMovementRow, string?>(
            "reference", ColumnDataType.Text, r => r.Reference),

        ReportColumn.Of<AccountMovementRow, string>(
            "source", ColumnDataType.Text, r => r.Source, groupable: true),

        ReportColumn.Of<AccountMovementRow, long>(
            "accountId", ColumnDataType.Number, r => r.AccountId),
    ];

    protected override IQueryable<AccountMovementRow> Build(
        ReportParameters parameters, ReportingDbContext db)
    {
        IQueryable<Repository.ReadModels.JournalLedgerRead> ledger = db.Ledger;

        if (parameters.Date("from") is DateOnly from)
        {
            ledger = ledger.Where(l => l.LedgerDate >= from);
        }

        if (parameters.Date("to") is DateOnly to)
        {
            ledger = ledger.Where(l => l.LedgerDate <= to);
        }

        return from l in ledger
               join a in db.Accounts on l.AccountId equals a.AccountId
               select new AccountMovementRow
               {
                   LedgerId = l.LedgerId,
                   Date = l.LedgerDate,
                   AccountTypeId = a.AccountTypeId,
                   AccountId = a.AccountId,
                   AccountCode = a.AccountCode,
                   Account = a.AccountName,
                   Debit = l.DebitAmountBase,
                   Credit = l.CreditAmountBase,
                   Description = l.TransactionDesc,
                   Reference = l.TransactionTypeCode + "-" + l.TransactionId,
                   Source = l.TransactionTypeCode,
               };
    }

    protected override async Task FormatRowsAsync(IReadOnlyList<AccountMovementRow> page, CancellationToken ct)
    {
        Dictionary<int, string> types = await _resolver.GetAccountTypeNamesAsync(ct);
        foreach (var row in page)
        {
            if (types.TryGetValue(row.AccountTypeId, out string? typeName))
            {
                row.AccountType = typeName;
            }
        }
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<AccountMovementRow, long>>)(r => r.LedgerId);
}

public sealed class AccountMovementRow
{
    public long LedgerId { get; set; }

    public DateOnly Date { get; set; }

    public int AccountTypeId { get; set; }

    public string AccountType { get; set; } = string.Empty;

    public long AccountId { get; set; }

    public string AccountCode { get; set; } = null!;

    public string Account { get; set; } = null!;

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }

    public string? Description { get; set; }

    public string? Reference { get; set; }

    public string Source { get; set; } = null!;
}
