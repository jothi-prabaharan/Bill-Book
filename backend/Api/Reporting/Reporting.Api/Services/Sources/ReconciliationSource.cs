using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public sealed class ReconciliationSource : ReportSource<ReconciliationRow>
{
    public override string ReportKey => "reconciliation";

    public override string Title => "Reconciliation Report";

    public override ReportModule Module => ReportModule.Accounting;

    public override string RequiredPermission => "accounting.view";

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date },
        new() { Name = "groupBy", Label = "Group By", DataType = ColumnDataType.Text },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<ReconciliationRow, DateOnly>(
            "transactionDate", ColumnDataType.Date, r => r.TransactionDate),

        ReportColumn.Of<ReconciliationRow, string?>(
            "transactionNo", ColumnDataType.Text, r => r.TransactionNo),

        ReportColumn.Of<ReconciliationRow, string?>(
            "reference", ColumnDataType.Text, r => r.Reference),

        ReportColumn.Of<ReconciliationRow, string?>(
            "description", ColumnDataType.Text, r => r.Description),

        ReportColumn.Of<ReconciliationRow, decimal>(
            "amountIn", ColumnDataType.Money, r => r.AmountIn, AggregateFunction.Sum),

        ReportColumn.Of<ReconciliationRow, decimal>(
            "amountOut", ColumnDataType.Money, r => r.AmountOut, AggregateFunction.Sum),

        ReportColumn.Of<ReconciliationRow, string>(
            "status", ColumnDataType.Enum, r => r.Status.ToString(), groupable: true),

        ReportColumn.Of<ReconciliationRow, string>(
            "statement", ColumnDataType.Text, r => r.Statement, groupable: true),

        ReportColumn.Of<ReconciliationRow, long>(
            "bankStatementLineId", ColumnDataType.Number, r => r.BankStatementLineId, filterable: false),
    ];

    protected override IQueryable<ReconciliationRow> Build(
        ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly? start = parameters.Date("from");
        DateOnly? end = parameters.Date("to");

        IQueryable<Repository.ReadModels.BankStatementLineRead> lines = db.BankStatementLines;

        if (start is DateOnly from)
        {
            lines = lines.Where(l => l.TransactionDate >= from);
        }

        if (end is DateOnly to)
        {
            lines = lines.Where(l => l.TransactionDate <= to);
        }

        return from l in lines
               join s in db.BankStatements on l.BankStatementId equals s.BankStatementId
               select new ReconciliationRow
               {
                   BankStatementLineId = l.BankStatementLineId,
                   TransactionDate = l.TransactionDate,
                   TransactionNo = l.MatchedTransactionId != null ? l.MatchedTransactionTypeCode + "-" + l.MatchedTransactionId : null,
                   Reference = l.ReferenceNo,
                   Description = l.Description,
                   AmountIn = l.DepositAmount,
                   AmountOut = l.WithdrawalAmount,
                   Status = (StatementLineStatus)l.Status,
                   Statement = s.StatementReference ?? s.FileName ?? "Unknown",
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<ReconciliationRow, long>>)(r => r.BankStatementLineId);
}

public sealed class ReconciliationRow
{
    public long BankStatementLineId { get; set; }
    public DateOnly TransactionDate { get; set; }
    public string? TransactionNo { get; set; }
    public string? Reference { get; set; }
    public string? Description { get; set; }
    public decimal AmountIn { get; set; }
    public decimal AmountOut { get; set; }
    public StatementLineStatus Status { get; set; }
    public string Statement { get; set; } = null!;
}
