using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public class SalesRegisterRow
{
    public long Id { get; set; }
    public string TransactionType { get; set; } = null!;
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string CustomerName { get; set; } = null!;
    public string? CustomerGstin { get; set; }
    public string SupplyType { get; set; } = null!;
    public decimal TaxableAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal CessAmount { get; set; }
    public decimal TotalAmount { get; set; }
}

public sealed class SalesRegisterSource : ReportSource<SalesRegisterRow>
{
    public override string ReportKey => "sales-register";

    public override string Title => "Sales Register";

    public override ReportModule Module => ReportModule.Sales;

    public override string RequiredPermission => "sales.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<SalesRegisterRow, string>("transactionType", ColumnDataType.Text, r => r.TransactionType, groupable: true),
        ReportColumn.Of<SalesRegisterRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<SalesRegisterRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<SalesRegisterRow, string>("customerName", ColumnDataType.Text, r => r.CustomerName, groupable: true),
        ReportColumn.Of<SalesRegisterRow, string?>("customerGstin", ColumnDataType.Text, r => r.CustomerGstin),
        ReportColumn.Of<SalesRegisterRow, string>("supplyType", ColumnDataType.Text, r => r.SupplyType, groupable: true),
        ReportColumn.Of<SalesRegisterRow, decimal>("taxableAmount", ColumnDataType.Money, r => r.TaxableAmount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SalesRegisterRow, decimal>("cgstAmount", ColumnDataType.Money, r => r.CgstAmount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SalesRegisterRow, decimal>("sgstAmount", ColumnDataType.Money, r => r.SgstAmount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SalesRegisterRow, decimal>("igstAmount", ColumnDataType.Money, r => r.IgstAmount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SalesRegisterRow, decimal>("cessAmount", ColumnDataType.Money, r => r.CessAmount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SalesRegisterRow, decimal>("totalAmount", ColumnDataType.Money, r => r.TotalAmount, aggregate: AggregateFunction.Sum),

        ReportColumn.Of<SalesRegisterRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<SalesRegisterRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from s in db.SalesRegisters
               join c in db.Contacts on s.ContactId equals c.ContactId into gc
               from c in gc.DefaultIfEmpty()
               select new SalesRegisterRow
               {
                   Id = s.SalesRegisterId,
                   TransactionType = s.TransactionTypeCode,
                   DocumentNo = s.DocumentNo,
                   DocumentDate = s.DocumentDate,
                   CustomerName = c != null ? c.DisplayName : "Unknown",
                   CustomerGstin = s.ContactGstin,
                   SupplyType = s.SupplyType,
                   TaxableAmount = s.TaxableAmount,
                   CgstAmount = s.CgstAmount,
                   SgstAmount = s.SgstAmount,
                   IgstAmount = s.IgstAmount,
                   CessAmount = s.CessAmount,
                   TotalAmount = s.TotalAmount
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<SalesRegisterRow, DateOnly>>)(r => r.DocumentDate);
}

