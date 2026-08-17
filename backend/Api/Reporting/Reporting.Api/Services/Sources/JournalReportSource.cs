using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public sealed class JournalReportSource : ReportSource<JournalReportRow>
{
    private readonly BatchedNameResolver _resolver;

    public JournalReportSource(BatchedNameResolver resolver)
    {
        _resolver = resolver;
    }

    public override string ReportKey => "journal-report";

    public override string Title => "Journal Report";

    public override ReportModule Module => ReportModule.Accounting;

    public override string RequiredPermission => "accounting.view";

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<JournalReportRow, DateOnly>(
            "date", ColumnDataType.Date, r => r.Date),

        ReportColumn.Of<JournalReportRow, string?>(
            "transactionNo", ColumnDataType.Text, r => r.TransactionNo),

        ReportColumn.Of<JournalReportRow, string?>(
            "reference", ColumnDataType.Text, r => r.Reference),

        ReportColumn.Of<JournalReportRow, string?>(
            "narration", ColumnDataType.Text, r => r.Narration),

        ReportColumn.Of<JournalReportRow, string?>(
            "description", ColumnDataType.Text, r => r.Description),

        ReportColumn.Of<JournalReportRow, string>(
            "transactions", ColumnDataType.Text, r => r.Transactions, groupable: true),

        ReportColumn.Of<JournalReportRow, string>(
            "accountCode", ColumnDataType.Text, r => r.AccountCode, groupable: true),

        ReportColumn.Of<JournalReportRow, string>(
            "accountName", ColumnDataType.Text, r => r.AccountName, groupable: true),

        ReportColumn.Of<JournalReportRow, string?>(
            "contactName", ColumnDataType.Text, r => r.ContactName),

        ReportColumn.Of<JournalReportRow, string>(
            "currency", ColumnDataType.Text, r => r.Currency, groupable: true),

        ReportColumn.Of<JournalReportRow, decimal>(
            "exchangeRate", ColumnDataType.Rate, r => r.ExchangeRate),

        ReportColumn.Of<JournalReportRow, decimal>(
            "debitSource", ColumnDataType.Money, r => r.DebitSource, AggregateFunction.Sum),

        ReportColumn.Of<JournalReportRow, decimal>(
            "creditSource", ColumnDataType.Money, r => r.CreditSource, AggregateFunction.Sum),

        ReportColumn.Of<JournalReportRow, decimal>(
            "debit", ColumnDataType.Money, r => r.Debit, AggregateFunction.Sum),

        ReportColumn.Of<JournalReportRow, decimal>(
            "credit", ColumnDataType.Money, r => r.Credit, AggregateFunction.Sum),

        ReportColumn.Of<JournalReportRow, string?>(
            "createdByName", ColumnDataType.Text, r => r.CreatedByName),

        ReportColumn.Of<JournalReportRow, DateTimeOffset?>(
            "createdAt", ColumnDataType.DateTime, r => r.CreatedAt),

        ReportColumn.Of<JournalReportRow, string?>(
            "postedByName", ColumnDataType.Text, r => r.PostedByName),

        ReportColumn.Of<JournalReportRow, DateTimeOffset?>(
            "postedAt", ColumnDataType.DateTime, r => r.PostedAt),

        ReportColumn.Of<JournalReportRow, string?>(
            "modifiedByName", ColumnDataType.Text, r => r.ModifiedByName),

        ReportColumn.Of<JournalReportRow, DateTimeOffset?>(
            "modifiedAt", ColumnDataType.DateTime, r => r.ModifiedAt),

        ReportColumn.Of<JournalReportRow, string>(
            "status", ColumnDataType.Enum, r => r.Status.ToString()),

        ReportColumn.Of<JournalReportRow, long>(
            "journalDetailId", ColumnDataType.Number, r => r.JournalDetailId, filterable: false),
    ];

    protected override IQueryable<JournalReportRow> Build(
        ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly? start = parameters.Date("from");
        DateOnly? end = parameters.Date("to");

        IQueryable<Repository.ReadModels.JournalRead> journals = db.Journals;

        if (start is DateOnly from)
        {
            journals = journals.Where(j => j.JournalDate >= from);
        }

        if (end is DateOnly to)
        {
            journals = journals.Where(j => j.JournalDate <= to);
        }

        return from j in journals
               join l in db.JournalDetails on j.JournalId equals l.JournalId
               join a in db.Accounts on l.AccountId equals a.AccountId
               join c in db.Contacts on l.SubAccountId equals c.ContactId into contacts
               from c in contacts.DefaultIfEmpty()
               select new JournalReportRow
               {
                   JournalDetailId = l.JournalDetailId,
                   Date = j.JournalDate,
                   TransactionNo = j.JournalNo,
                   Reference = j.Reference,
                   Narration = j.Memo,
                   Description = l.LineMemo,
                   Transactions = j.TransactionTypeCode,
                   AccountCode = a.AccountCode,
                   AccountName = a.AccountName,
                   ContactName = c != null ? c.DisplayName : null,
                   Currency = j.CurrencyCode,
                   ExchangeRate = j.ExchangeRate,
                   DebitSource = l.DebitAmount,
                   CreditSource = l.CreditAmount,
                   Debit = l.DebitAmountBase,
                   Credit = l.CreditAmountBase,
                   CreatedBy = j.CreatedBy,
                   CreatedAt = j.CreatedAt,
                   PostedBy = j.PostedBy,
                   PostedAt = j.PostedAt,
                   ModifiedBy = j.ModifiedBy,
                   ModifiedAt = j.ModifiedAt,
                   Status = (JournalStatus)j.Status,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<JournalReportRow, long>>)(r => r.JournalDetailId);

    protected override async Task FormatRowsAsync(IReadOnlyList<JournalReportRow> page, CancellationToken ct)
    {
        var userIds = page.Select(r => r.CreatedBy)
            .Concat(page.Select(r => r.ModifiedBy))
            .Concat(page.Select(r => r.PostedBy))
            .Distinct();

        Dictionary<Guid, string> names = await _resolver.GetUserNamesAsync(userIds, ct);

        foreach (JournalReportRow row in page)
        {
            if (row.CreatedBy.HasValue && names.TryGetValue(row.CreatedBy.Value, out string? createdBy))
            {
                row.CreatedByName = createdBy;
            }

            if (row.ModifiedBy.HasValue && names.TryGetValue(row.ModifiedBy.Value, out string? modifiedBy))
            {
                row.ModifiedByName = modifiedBy;
            }

            if (row.PostedBy.HasValue && names.TryGetValue(row.PostedBy.Value, out string? postedBy))
            {
                row.PostedByName = postedBy;
            }
        }
    }
}

public sealed class JournalReportRow
{
    public long JournalDetailId { get; set; }
    public DateOnly Date { get; set; }
    public string? TransactionNo { get; set; }
    public string? Reference { get; set; }
    public string? Narration { get; set; }
    public string? Description { get; set; }
    public string Transactions { get; set; } = null!;
    public string AccountCode { get; set; } = null!;
    public string AccountName { get; set; } = null!;
    public string? ContactName { get; set; }
    public string Currency { get; set; } = null!;
    public decimal ExchangeRate { get; set; }
    public decimal DebitSource { get; set; }
    public decimal CreditSource { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }

    public Guid? CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }

    public Guid? PostedBy { get; set; }
    public string? PostedByName { get; set; }
    public DateTimeOffset? PostedAt { get; set; }

    public Guid? ModifiedBy { get; set; }
    public string? ModifiedByName { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }

    public JournalStatus Status { get; set; }
}
