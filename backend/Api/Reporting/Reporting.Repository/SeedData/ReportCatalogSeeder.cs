using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.TableEntities;

namespace Reporting.Repository.SeedData;

/// <summary>
/// Writes the report catalog for a branch — the <c>rpt.Reports</c> row for each
/// report and the <c>rpt.ReportDetails</c> row for each of its columns.
///
/// <b>Re-runnable, and meant to be.</b> It adds only what is missing, so calling
/// it against a branch set up months ago backfills every report added since. That
/// is not a nicety: the catalog grows with each stage, and forty-five reports will
/// not all arrive on the same day. A branch seeded today and left alone would
/// otherwise never see R2's inventory reports.
///
/// <b>What it does not do is update.</b> A header a branch has renamed into its own
/// language stays renamed, and a column somebody turned off stays off — re-running
/// the seeder must never overwrite a branch's own choices, or the backfill becomes
/// a reset that quietly undoes somebody's configuration.
/// </summary>
public sealed class ReportCatalogSeeder
{
    private readonly ReportingDbContext _db;

    public ReportCatalogSeeder(ReportingDbContext db) => _db = db;

    /// <summary>Rows written, by report key. Empty on a branch already complete.</summary>
    public async Task<Dictionary<string, int>> SeedAsync(CancellationToken ct)
    {
        Dictionary<string, int> written = [];

        foreach (ReportSeed seed in Catalog)
        {
            int rows = await SeedOneAsync(seed, ct);

            if (rows > 0)
            {
                written[seed.ReportKey] = rows;
            }
        }

        await _db.SaveChangesAsync(ct);

        return written;
    }

    private async Task<int> SeedOneAsync(ReportSeed seed, CancellationToken ct)
    {
        Report? report = await _db.Reports
            .FirstOrDefaultAsync(r => r.ReportKey == seed.ReportKey, ct);

        int written = 0;

        if (report is null)
        {
            report = new Report
            {
                ReportKey = seed.ReportKey,
                Title = seed.Title,
                Module = seed.Module,
                Description = seed.Description,
                RequiredPermission = seed.RequiredPermission,
                IsActive = true,
                SortOrder = seed.SortOrder,
            };

            _db.Reports.Add(report);

            // Needed before the detail rows can reference it. The report and its
            // columns are one unit — a report row with no columns renders an empty
            // grid rather than failing, which is worse than not appearing at all.
            await _db.SaveChangesAsync(ct);
            written++;
        }

        HashSet<string> existing =
        [
            .. await _db.ReportDetails
                .Where(d => d.ReportId == report.ReportId)
                .Select(d => d.ColumnKey)
                .ToListAsync(ct),
        ];

        int order = 0;

        foreach (ColumnSeed column in seed.Columns)
        {
            order++;

            if (existing.Contains(column.Key))
            {
                continue;
            }

            _db.ReportDetails.Add(new ReportDetail
            {
                ReportId = report.ReportId,
                ColumnKey = column.Key,
                Header = column.Header,
                DataType = column.DataType,
                IsDefault = column.IsDefault,
                IsFilterable = column.IsFilterable,
                IsSortable = true,
                IsGroupable = column.IsGroupable,
                IsPivotable = column.IsPivotable,
                DefaultAggregate = column.Aggregate,
                Alignment = column.Alignment,
                SortOrder = order,
                IsPrimary = column.IsPrimary,
                IsHidden = column.IsHidden,
            });

            written++;
        }

        return written;
    }

    /// <summary>
    /// The catalog. <b>Every column an <c>IReportSource</c> declares needs a row
    /// here and nothing else does</b> — the two lists are compared when a report's
    /// columns are built, and a mismatch refuses the report by name. Adding a
    /// column to a source without adding it here breaks that report rather than
    /// quietly omitting the column, which is the intended trade.
    /// </summary>
    private static IReadOnlyList<ReportSeed> Catalog =>
    [
        new()
        {
            ReportKey = "account-movement",
            Title = "Account Movement",
            Module = ReportModule.Accounting,
            RequiredPermission = "accounting.view",
            Description = "Every posting against every account, in date order.",
            SortOrder = 10,
            Columns =
            [
                new("date", "Date", ColumnDataType.Date, IsDefault: true, IsPrimary: true),
                new("accountCode", "Account Code", ColumnDataType.Text, IsDefault: true,
                    IsGroupable: true),
                new("account", "Account", ColumnDataType.Text, IsDefault: true,
                    IsGroupable: true, IsPrimary: true),
                new("debit", "Debit", ColumnDataType.Money, IsDefault: true,
                    Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("credit", "Credit", ColumnDataType.Money, IsDefault: true,
                    Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("description", "Description", ColumnDataType.Text, IsDefault: true),
                new("reference", "Reference", ColumnDataType.Text, IsDefault: true),
                new("source", "Source", ColumnDataType.Text, IsDefault: true,
                    IsGroupable: true, IsPivotable: true),
                new("accountId", "Account", ColumnDataType.Number, IsHidden: true),
            ],
        },
        new()
        {
            ReportKey = "account-transaction",
            Title = "Account Transaction",
            Module = ReportModule.Accounting,
            RequiredPermission = "accounting.view",
            Description =
                "Every posting with its contact, its currency and a running balance.",
            SortOrder = 15,
            Columns =
            [
                new("date", "Date", ColumnDataType.Date, IsDefault: true, IsPrimary: true),
                new("transactionNo", "Transaction No", ColumnDataType.Text, IsDefault: true,
                    IsPrimary: true),
                new("source", "Source", ColumnDataType.Text, IsDefault: true,
                    IsGroupable: true, IsPivotable: true),
                new("accountCode", "Account Code", ColumnDataType.Text, IsDefault: true,
                    IsGroupable: true),
                new("account", "Account", ColumnDataType.Text, IsDefault: true,
                    IsGroupable: true),
                new("contactCode", "Contact Code", ColumnDataType.Text),
                new("contactName", "Contact Name", ColumnDataType.Text, IsDefault: true),
                new("description", "Description", ColumnDataType.Text, IsDefault: true),
                new("reference", "Reference", ColumnDataType.Text),
                new("currency", "Currency", ColumnDataType.Text, IsGroupable: true),
                new("exchangeRate", "ExchangeRate", ColumnDataType.Rate,
                    Alignment: ColumnAlignment.Right),
                new("debitSource", "Debit(Source)", ColumnDataType.Money,
                    Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("creditSource", "Credit(Source)", ColumnDataType.Money,
                    Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("debit", "Debit(%CurCode%)", ColumnDataType.Money, IsDefault: true,
                    Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("credit", "Credit(%CurCode%)", ColumnDataType.Money, IsDefault: true,
                    Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("runningBalance", "Running Balance", ColumnDataType.Money,
                    IsDefault: true, IsFilterable: false, Alignment: ColumnAlignment.Right),
                new("ledgerId", "Ledger", ColumnDataType.Number, IsHidden: true),
            ],
        },
        new()
        {
            ReportKey = "trial-balance",
            Title = "Trial Balance",
            Module = ReportModule.Accounting,
            RequiredPermission = "accounting.view",
            Description = "Every account with a balance, and the two totals that must agree.",
            SortOrder = 20,
            Columns =
            [
                new("accountCode", "Account Code", ColumnDataType.Text, IsDefault: true,
                    IsPrimary: true),
                new("accountName", "Account Name", ColumnDataType.Text, IsDefault: true,
                    IsGroupable: true, IsPrimary: true),
                new("debit", "Debit", ColumnDataType.Money, IsDefault: true,
                    Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("credit", "Credit", ColumnDataType.Money, IsDefault: true,
                    Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                new("currentBalance", "Current Balance", ColumnDataType.Money, IsDefault: true,
                    Aggregate: AggregateFunction.Sum, Alignment: ColumnAlignment.Right),
                // CAAccountID in the source list. An internal key, never offered.
                new("accountId", "Account", ColumnDataType.Number, IsHidden: true),
            ],
        },
    ];

    private sealed class ReportSeed
    {
        public required string ReportKey { get; init; }

        public required string Title { get; init; }

        public required ReportModule Module { get; init; }

        public required string RequiredPermission { get; init; }

        public string? Description { get; init; }

        public int SortOrder { get; init; }

        public required IReadOnlyList<ColumnSeed> Columns { get; init; }
    }

    private sealed record ColumnSeed(
        string Key,
        string Header,
        ColumnDataType DataType,
        bool IsDefault = false,
        bool IsFilterable = true,
        bool IsGroupable = false,
        bool IsPivotable = false,
        bool IsPrimary = false,
        bool IsHidden = false,
        AggregateFunction Aggregate = AggregateFunction.None,
        ColumnAlignment Alignment = ColumnAlignment.Left);
}
