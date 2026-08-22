using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Services;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// Reports — the catalog, one report's metadata, and running it.
///
/// <b>Every route needs two permissions, not one.</b> <c>reports.view</c> gets you
/// as far as this controller; the report itself then requires its own module's
/// permission — <c>accounting.view</c> for the ledger reports, <c>inventory.view</c>
/// for the stock ones. Without that second check the report engine would be a way
/// round the permissions on the screens it reports from, which is the most obvious
/// way a reporting module leaks a business's books.
///
/// The module is <c>reports</c>, matching what is seeded in <c>mst.Permissions</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("reports")]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly ReportCatalogService _catalog;
    private readonly ReportRunner _runner;

    public ReportsController(ReportCatalogService catalog, ReportRunner runner)
    {
        _catalog = catalog;
        _runner = runner;
    }

    /// <summary>The reports this caller may run. Ones they may not are absent, not refused.</summary>
    [HttpGet]
    public async Task<IActionResult> Catalog(CancellationToken ct) =>
        Ok(await _catalog.ListAsync(Permissions(), ct));

    /// <summary>One report's parameters and columns, for building its screen.</summary>
    [HttpGet("{reportKey}")]
    public async Task<IActionResult> Metadata(string reportKey, CancellationToken ct)
    {
        ReportMetadataView? metadata = await _catalog.MetadataAsync(
            reportKey, Permissions(), new ReportCurrencyView(), ct);

        // NotFound for "no such report" and for "not yours" alike. Which one it is
        // is itself information, and Forbid() here would tell a caller exactly
        // which reports exist for them to go after.
        return metadata is null ? NotFound() : Ok(metadata);
    }

    /// <summary>
    /// Runs the report.
    ///
    /// <b>POST for a read, deliberately.</b> A filter with an <c>in</c> list of two
    /// hundred item ids does not fit a query string, and a URL that long is refused
    /// by proxies before it reaches the gateway. The action is idempotent and is
    /// permissioned as a read.
    /// </summary>
    [HttpPost("{reportKey}/query")]
    // A read wearing a POST. Without this the derived action would be `.edit`, so
    // running a report would demand permission to change things — and a Viewer,
    // the role that exists precisely to read reports, could not run one.
    [PermissionAction("view")]
    public async Task<IActionResult> Query(
        string reportKey, [FromBody] ReportQueryRequest request, CancellationToken ct)
    {
        try
        {
            ReportResultView? result =
                await _runner.RunAsync(reportKey, request, Permissions(), ct);

            return result is null ? NotFound() : Ok(result);
        }
        catch (ReportQueryException ex)
        {
            // Everything this exception carries is something the caller can fix: a
            // column that does not exist, a value that will not parse, a filter on
            // a column that does not accept one. It is a 400 with the message
            // intact rather than a 500 with it in a log.
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// The report as a file, over the <b>whole</b> result rather than a page.
    ///
    /// <b>The cap refuses rather than truncates.</b> A 100,000-row ceiling exists
    /// because a bigger export is a background job this product has no broker for
    /// yet — but a file silently missing its last two hundred thousand rows is
    /// worse than no file, because it looks complete. The message says the row
    /// count so the person can narrow the range knowing by how much.
    /// </summary>
    [HttpPost("{reportKey}/export")]
    [PermissionAction("export")]
    public async Task<IActionResult> Export(
        string reportKey,
        [FromQuery] ExportFormat format,
        [FromBody] ReportQueryRequest request,
        CancellationToken ct)
    {
        if (format == ExportFormat.Pdf)
        {
            // Declared in the contract so adding it later is not a route change,
            // and refused here because nothing in the pinned package list writes a
            // PDF that renders Tamil or Chinese. Reporting.md §5.8.
            return BadRequest(new
            {
                message = "PDF export is not built yet. Export to Excel instead.",
            });
        }

        try
        {
            // Paging off: an export is the same query without a page.
            request.Page = new ReportPageModel
            {
                Number = 1,
                Size = ExportRowCap + 1,
                IncludeCount = true,
            };

            ReportResultView? result =
                await _runner.RunAsync(reportKey, request, Permissions(), ct);

            if (result is null)
            {
                return NotFound();
            }

            if (result.Rows.Count > ExportRowCap)
            {
                return BadRequest(new
                {
                    message =
                        $"This export would be {result.Page.TotalRows:N0} rows, and the limit is "
                        + $"{ExportRowCap:N0}. Narrow the date range or add a filter.",
                });
            }

            return File(
                ExcelReportWriter.Write(result),
                ExcelReportWriter.ContentType,
                $"{reportKey}-{DateTime.UtcNow:yyyy-MM-dd}.xlsx");
        }
        catch (ReportQueryException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Reporting.md §5.6. When a broker exists this becomes the threshold at which an
    /// export is queued rather than refused.
    /// </summary>
    private const int ExportRowCap = 100_000;

    /// <summary>
    /// The permissions on the token.
    ///
    /// Read from claims rather than fetched, so a report cannot be run with
    /// yesterday's rights — the token is the authority, and it expires in fifteen
    /// minutes.
    /// </summary>
    private IReadOnlySet<string> Permissions() =>
        User.FindAll("permission").Select(c => c.Value).ToHashSet(StringComparer.Ordinal);
}
