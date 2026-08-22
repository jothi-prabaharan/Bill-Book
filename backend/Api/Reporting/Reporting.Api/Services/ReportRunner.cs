using Reporting.Entity.Models;
using Reporting.Repository;
using Shared.Kernel.Tenancy;

namespace Reporting.Api.Services;

/// <summary>
/// Runs a report: finds the source, checks the caller may have it, executes it,
/// and puts the presentation back on the result.
///
/// The split from <see cref="ReportSource{TRow}"/> is deliberate. A source knows
/// its query and its columns and nothing about who is asking; this knows about the
/// caller, the branch's currency and the seeded presentation, and nothing about
/// any particular report. Neither has to change when the other does.
/// </summary>
public sealed class ReportRunner
{
    private readonly ReportCatalogService _catalog;
    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public ReportRunner(
        ReportCatalogService catalog, ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _catalog = catalog;
        _db = db;
        _currency = currency;
    }

    /// <summary>
    /// Null when the report does not exist <b>or</b> the caller may not run it. One
    /// answer for both, so a caller cannot learn which reports exist by asking.
    /// </summary>
    public async Task<ReportResultView?> RunAsync(
        string reportKey,
        ReportQueryRequest request,
        IReadOnlySet<string> permissions,
        CancellationToken ct)
    {
        IReportSource? source = _catalog.Source(reportKey, permissions);

        if (source is null)
        {
            return null;
        }

        ReportCurrencyView currency = await CurrencyAsync(ct);

        ReportResultView result = await source.ExecuteAsync(request, _db, currency, ct);

        // The source produced rows and totals; presentation comes from the seeded
        // catalog, including the %CurCode% substitution. Done here rather than in
        // the source so a report never has to know what a header looks like.
        result.Columns = await _catalog.ColumnsAsync(source, currency, ct);

        // Only the columns actually asked for, in the order asked for. The source
        // returns every column it can produce, because the totals are over all of
        // them; the grid is shown what it selected.
        if (request.Columns.Count > 0)
        {
            Dictionary<string, ReportColumnView> byKey =
                result.Columns.ToDictionary(c => c.Key, StringComparer.Ordinal);

            result.Columns =
            [
                .. request.Columns
                    .Where(byKey.ContainsKey)
                    .Select(key => byKey[key]),
            ];
        }

        return result;
    }

    /// <summary>
    /// The branch's base currency, which every money column on every report is
    /// denominated in.
    ///
    /// <b>The provider answers null rather than guessing, and a report must not
    /// undo that.</b> Its callers elsewhere postpone a posting instead of booking
    /// one in an assumed currency; a report cannot postpone, because it is a read
    /// and refusing it on a brief outage helps nobody. So an unknown currency
    /// yields an empty code, and the header substitution drops the parenthetical
    /// entirely — "Debit(INR)" becomes "Debit" rather than "Debit()" or, far
    /// worse, "Debit(INR)" on a branch that keeps its books in dirhams. The
    /// figures are unaffected: they were always the base-currency columns.
    /// </summary>
    private async Task<ReportCurrencyView> CurrencyAsync(CancellationToken ct) =>
        new()
        {
            Code = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty,

            // Two everywhere the product currently trades. When a zero-decimal
            // currency appears this reads the currency master instead — a display
            // concern only, since amounts are stored as decimals throughout.
            Decimals = 2,
        };
}
