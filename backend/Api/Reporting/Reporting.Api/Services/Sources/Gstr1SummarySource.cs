using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public class Gstr1SummaryRow
{
    public long Id { get; set; }
    public string SummaryType { get; set; } = null!; // B2B, B2C, etc.
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string? CustomerGstin { get; set; }
    public decimal GstRate { get; set; }
    public string GstRateText => $"{GstRate:0.##}%";
    public decimal TaxableAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal CessAmount { get; set; }
}

public sealed class Gstr1SummarySource : ReportSource<Gstr1SummaryRow>
{
    public override string ReportKey => "gstr1-summary";

    public override string Title => "GSTR-1 Summary";

    public override ReportModule Module => ReportModule.Sales;

    public override string RequiredPermission => "sales.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<Gstr1SummaryRow, string>("summaryType", ColumnDataType.Text, r => r.SummaryType, groupable: true),
        ReportColumn.Of<Gstr1SummaryRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<Gstr1SummaryRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<Gstr1SummaryRow, string?>("customerGstin", ColumnDataType.Text, r => r.CustomerGstin),
        ReportColumn.Of<Gstr1SummaryRow, string>("gstRate", ColumnDataType.Text, r => r.GstRateText, groupable: true),
        ReportColumn.Of<Gstr1SummaryRow, decimal>("taxableAmount", ColumnDataType.Money, r => r.TaxableAmount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<Gstr1SummaryRow, decimal>("cgstAmount", ColumnDataType.Money, r => r.CgstAmount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<Gstr1SummaryRow, decimal>("sgstAmount", ColumnDataType.Money, r => r.SgstAmount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<Gstr1SummaryRow, decimal>("igstAmount", ColumnDataType.Money, r => r.IgstAmount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<Gstr1SummaryRow, decimal>("cessAmount", ColumnDataType.Money, r => r.CessAmount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<Gstr1SummaryRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<Gstr1SummaryRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from s in db.SalesRegisters
               select new Gstr1SummaryRow
               {
                   Id = s.SalesRegisterId,
                   SummaryType = string.IsNullOrEmpty(s.ContactGstin) ? "B2C" : "B2B",
                   DocumentNo = s.DocumentNo,
                   DocumentDate = s.DocumentDate,
                   CustomerGstin = s.ContactGstin,
                   GstRate = s.GstRate,
                   TaxableAmount = s.TaxableAmount,
                   CgstAmount = s.CgstAmount,
                   SgstAmount = s.SgstAmount,
                   IgstAmount = s.IgstAmount,
                   CessAmount = s.CessAmount
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<Gstr1SummaryRow, DateOnly>>)(r => r.DocumentDate);
}
