using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Repository.Repositories;

public class MsmeAgingRow
{
    public long BillId { get; set; }
    public string DocumentNo { get; set; } = null!;
    public string? VendorBillNo { get; set; }
    public DateOnly DocumentDate { get; set; }
    public DateOnly DueDate { get; set; }
    public string ContactCode { get; set; } = null!;
    public string ContactName { get; set; } = null!;
    public string? Pan { get; set; }
    public string? UdyamNumber { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountDue { get; set; }
    public int DaysFromAcceptance { get; set; }
    public int DaysPastDue { get; set; }
    public string Bucket { get; set; } = null!;
    public bool IsOverdue45Days { get; set; }
    public decimal DisallowedAmount { get; set; }
}

/// <summary>
/// MSME Ageing report & Section 43B(h) year-end disallowance tracker (TK-119).
/// Shows unpaid MSME bills categorized into 1-15 days, 16-45 days, and overdue > 45 days.
/// </summary>
public sealed class MsmeAgingSourceRepository : ReportSource<MsmeAgingRow>
{
    public override string ReportKey => "msme-aging";
    public override string Title => "MSME Ageing & Sec 43B(h) Disallowance Report";
    public override ReportModule Module => ReportModule.Purchase;
    public override string RequiredPermission => "purchase.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "asOf", Label = "As Of", DataType = ColumnDataType.Date },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<MsmeAgingRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<MsmeAgingRow, string?>("vendorBillNo", ColumnDataType.Text, r => r.VendorBillNo),
        ReportColumn.Of<MsmeAgingRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<MsmeAgingRow, DateOnly>("dueDate", ColumnDataType.Date, r => r.DueDate),
        ReportColumn.Of<MsmeAgingRow, string>("contactCode", ColumnDataType.Text, r => r.ContactCode),
        ReportColumn.Of<MsmeAgingRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<MsmeAgingRow, string?>("pan", ColumnDataType.Text, r => r.Pan),
        ReportColumn.Of<MsmeAgingRow, string?>("udyamNumber", ColumnDataType.Text, r => r.UdyamNumber),
        ReportColumn.Of<MsmeAgingRow, int>("daysFromAcceptance", ColumnDataType.Number, r => r.DaysFromAcceptance),
        ReportColumn.Of<MsmeAgingRow, int>("daysPastDue", ColumnDataType.Number, r => r.DaysPastDue),
        ReportColumn.Of<MsmeAgingRow, string>("bucket", ColumnDataType.Text, r => r.Bucket, groupable: true),
        ReportColumn.Of<MsmeAgingRow, bool>("isOverdue45Days", ColumnDataType.Boolean, r => r.IsOverdue45Days),
        ReportColumn.Of<MsmeAgingRow, decimal>("totalAmount", ColumnDataType.Money, r => r.TotalAmount, AggregateFunction.Sum),
        ReportColumn.Of<MsmeAgingRow, decimal>("amountDue", ColumnDataType.Money, r => r.AmountDue, AggregateFunction.Sum),
        ReportColumn.Of<MsmeAgingRow, decimal>("disallowedAmount", ColumnDataType.Money, r => r.DisallowedAmount, AggregateFunction.Sum),
        ReportColumn.Of<MsmeAgingRow, long>("billId", ColumnDataType.Number, r => r.BillId, filterable: false),
    ];

    protected override IQueryable<MsmeAgingRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly asOf = parameters.Date("asOf") ?? DateOnly.FromDateTime(DateTime.UtcNow);

        return from b in db.Bills
               where b.Status == Shared.Kernel.Documents.DocumentStatus.Posted && b.DocumentDate <= asOf
               join c in db.Contacts on b.ContactId equals c.ContactId
               where c.IsMsme
               let daysAge = asOf.DayNumber - b.DocumentDate.DayNumber
               let daysPastDue = asOf.DayNumber - b.DueDate.DayNumber
               select new MsmeAgingRow
               {
                   BillId = b.BillId,
                   DocumentNo = b.DocumentNo,
                   VendorBillNo = b.VendorBillNo,
                   DocumentDate = b.DocumentDate,
                   DueDate = b.DueDate,
                   ContactCode = c.ContactCode,
                   ContactName = c.DisplayName,
                   Pan = c.Pan,
                   UdyamNumber = c.UdyamNumber,
                   TotalAmount = b.TotalAmount,
                   AmountDue = b.TotalAmount,
                   DaysFromAcceptance = daysAge,
                   DaysPastDue = daysPastDue > 0 ? daysPastDue : 0,
                   Bucket = daysAge <= 15 ? "Within 15 days"
                          : daysAge <= 45 ? "16 - 45 days"
                          : "Overdue > 45 days",
                   IsOverdue45Days = daysAge > 45,
                   DisallowedAmount = daysAge > 45 ? b.TotalAmount : 0m,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<MsmeAgingRow, DateOnly>>)(r => r.DocumentDate);
}
