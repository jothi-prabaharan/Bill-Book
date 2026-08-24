using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public class ArAgingSummaryRow
{
    public long ContactId { get; set; }
    public string ContactCode { get; set; } = null!;
    public string ContactName { get; set; } = null!;
    public decimal Current { get; set; }
    public decimal Days1_30 { get; set; }
    public decimal Days31_60 { get; set; }
    public decimal Days61_90 { get; set; }
    public decimal Days90Plus { get; set; }
    public decimal Total { get; set; }
}

public sealed class ArAgingSummarySource : ReportSource<ArAgingSummaryRow>
{
    public override string ReportKey => "ar-aging-summary";

    public override string Title => "Aged Receivables Summary";

    public override ReportModule Module => ReportModule.Sales;

    public override string RequiredPermission => "sales.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<ArAgingSummaryRow, string>("contactCode", ColumnDataType.Text, r => r.ContactCode),
        ReportColumn.Of<ArAgingSummaryRow, string>("contactName", ColumnDataType.Text, r => r.ContactName),
        ReportColumn.Of<ArAgingSummaryRow, decimal>("current", ColumnDataType.Money, r => r.Current, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ArAgingSummaryRow, decimal>("days1_30", ColumnDataType.Money, r => r.Days1_30, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ArAgingSummaryRow, decimal>("days31_60", ColumnDataType.Money, r => r.Days31_60, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ArAgingSummaryRow, decimal>("days61_90", ColumnDataType.Money, r => r.Days61_90, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ArAgingSummaryRow, decimal>("days90Plus", ColumnDataType.Money, r => r.Days90Plus, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ArAgingSummaryRow, decimal>("total", ColumnDataType.Money, r => r.Total, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ArAgingSummaryRow, long>("contactId", ColumnDataType.Number, r => r.ContactId, filterable: false),
    ];

    protected override IQueryable<ArAgingSummaryRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        
        return from l in db.Ledger
               where l.ContactId != null && l.LedgerDate <= asOf
               // Assuming Accounts Receivable has a specific AccountType or LedgerSource. We will join Accounts.
               join a in db.Accounts on l.AccountId equals a.AccountId
               where a.AccountSystemName == "Accounts Receivable" // Assuming ARR is Accounts Receivable
               join c in db.Contacts on l.ContactId equals c.ContactId
               where c.IsCustomer
               group l by new { c.ContactId, c.ContactCode, c.DisplayName } into g
               select new ArAgingSummaryRow
               {
                   ContactId = g.Key.ContactId,
                   ContactCode = g.Key.ContactCode,
                   ContactName = g.Key.DisplayName,
                   // Using LedgerDate as proxy for DueDate
                   Current = g.Sum(x => asOf.DayNumber - x.LedgerDate.DayNumber <= 0 ? (x.DebitAmountBase - x.CreditAmountBase) : 0),
                   Days1_30 = g.Sum(x => asOf.DayNumber - x.LedgerDate.DayNumber > 0 && asOf.DayNumber - x.LedgerDate.DayNumber <= 30 ? (x.DebitAmountBase - x.CreditAmountBase) : 0),
                   Days31_60 = g.Sum(x => asOf.DayNumber - x.LedgerDate.DayNumber > 30 && asOf.DayNumber - x.LedgerDate.DayNumber <= 60 ? (x.DebitAmountBase - x.CreditAmountBase) : 0),
                   Days61_90 = g.Sum(x => asOf.DayNumber - x.LedgerDate.DayNumber > 60 && asOf.DayNumber - x.LedgerDate.DayNumber <= 90 ? (x.DebitAmountBase - x.CreditAmountBase) : 0),
                   Days90Plus = g.Sum(x => asOf.DayNumber - x.LedgerDate.DayNumber > 90 ? (x.DebitAmountBase - x.CreditAmountBase) : 0),
                   Total = g.Sum(x => x.DebitAmountBase - x.CreditAmountBase)
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<ArAgingSummaryRow, string>>)(r => r.ContactName);
}
