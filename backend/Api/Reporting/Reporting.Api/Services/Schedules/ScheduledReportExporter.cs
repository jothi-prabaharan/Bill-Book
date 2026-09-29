using Reporting.Api.Controllers;
using Reporting.Entity.Models;

namespace Reporting.Api.Services.Schedules;

/// <summary>A scheduled report's workbook, or why there is none.</summary>
public sealed record ScheduledExport(byte[]? Workbook, long Rows, string? Problem);

/// <summary>Runs a report as its owner and writes the workbook (TK-114).</summary>
public interface IScheduledReportExporter
{
    Task<ScheduledExport> ExportAsync(string reportKey, ReportQueryRequest layout, IReadOnlySet<string> permissions, CancellationToken ct);
}

/// <summary>
/// The export route's own steps: the same runner, paging off, the same row
/// cap, the same writer. A scheduled workbook cannot differ from the one the
/// owner would download by hand.
/// </summary>
public sealed class ScheduledReportExporter : IScheduledReportExporter
{
    private readonly ReportRunner _runner;

    public ScheduledReportExporter(ReportRunner runner) => _runner = runner;

    public async Task<ScheduledExport> ExportAsync(
        string reportKey, ReportQueryRequest layout, IReadOnlySet<string> permissions, CancellationToken ct)
    {
        layout.Page = new ReportPageModel { Number = 1, Size = ReportsController.ExportRowCap + 1, IncludeCount = true };

        try
        {
            ReportResultView? result = await _runner.RunAsync(reportKey, layout, permissions, ct);

            if (result is null)
            {
                return new ScheduledExport(null, 0, "The owner can no longer run this report.");
            }

            long rows = result.Page.TotalRows ?? result.Rows.Count;

            if (result.Rows.Count > ReportsController.ExportRowCap)
            {
                return new ScheduledExport(null, rows,
                    $"The report is {rows:N0} rows, over the {ReportsController.ExportRowCap:N0}-row export limit. Add a filter to the schedule.");
            }

            return new ScheduledExport(ExcelReportWriter.Write(result), rows, null);
        }
        catch (ReportQueryException ex)
        {
            return new ScheduledExport(null, 0, ex.Message);
        }
    }
}
