using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Entity.TableEntities;
using Reporting.Repository;

namespace Reporting.Api.Services;

/// <summary>
/// Which reports a caller may run, and what each one looks like.
///
/// <b>Two authorities meet here.</b> An <see cref="IReportSource"/> is the
/// authority on what a column <i>means</i> — which expression reads it, whether it
/// can be filtered at all. A <c>rpt.ReportDetails</c> row is the authority on how
/// it <i>appears</i> — its heading, its width, whether it is on by default. This
/// service merges the two, and refuses the report when they disagree about which
/// columns exist.
/// </summary>
public sealed class ReportCatalogService
{
    private readonly IReadOnlyDictionary<string, IReportSource> _sources;
    private readonly ReportingDbContext _db;

    public ReportCatalogService(IEnumerable<IReportSource> sources, ReportingDbContext db)
    {
        _sources = sources.ToDictionary(s => s.ReportKey, StringComparer.Ordinal);
        _db = db;
    }

    /// <summary>
    /// The catalog, as this caller sees it.
    ///
    /// <b>A report the caller lacks the permission for is absent, not refused.</b>
    /// Whether a report exists is itself information — a listing of things you may
    /// not have tells an attacker what to go after, and tells everybody else about
    /// a screen they cannot use.
    /// </summary>
    public async Task<List<ReportCatalogItemView>> ListAsync(
        IReadOnlySet<string> permissions, CancellationToken ct)
    {
        List<Report> rows = await _db.Reports
            .Where(r => r.IsActive)
            .OrderBy(r => r.Module)
            .ThenBy(r => r.SortOrder)
            .ThenBy(r => r.Title)
            .ToListAsync(ct);

        return
        [
            .. rows
                .Where(r => _sources.ContainsKey(r.ReportKey))
                .Where(r => permissions.Contains(r.RequiredPermission))
                .Select(r => new ReportCatalogItemView
                {
                    ReportKey = r.ReportKey,
                    Title = r.Title,
                    Module = r.Module,
                    Description = r.Description,
                    SortOrder = r.SortOrder,
                }),
        ];
    }

    /// <summary>
    /// One report's parameters and columns, ready for the grid.
    ///
    /// Returns null when the report does not exist <i>or</i> the caller may not run
    /// it — the caller turns both into the same answer, so the two are
    /// indistinguishable from outside.
    /// </summary>
    public async Task<ReportMetadataView?> MetadataAsync(
        string reportKey,
        IReadOnlySet<string> permissions,
        ReportCurrencyView currency,
        CancellationToken ct)
    {
        if (!_sources.TryGetValue(reportKey, out IReportSource? source)
            || !permissions.Contains(source.RequiredPermission))
        {
            return null;
        }

        List<ReportColumnView> columns = await ColumnsAsync(source, currency, ct);

        return new ReportMetadataView
        {
            ReportKey = source.ReportKey,
            Title = source.Title,
            Module = source.Module,
            Parameters =
            [
                .. source.Parameters.Select(p => new ReportParameterView
                {
                    Name = p.Name,
                    Label = p.Label,
                    DataType = p.DataType,
                    IsRequired = p.IsRequired,
                    DefaultValue = p.DefaultValue,
                }),
            ],
            Columns = columns,
            DefaultColumns = [.. await DefaultKeysAsync(source, ct)],
            ForcesSortOrder = source.ForcesSortOrder,
        };
    }

    public IReportSource? Source(string reportKey, IReadOnlySet<string> permissions) =>
        _sources.TryGetValue(reportKey, out IReportSource? source)
        && permissions.Contains(source.RequiredPermission)
            ? source
            : null;

    /// <summary>
    /// Column presentation, merged over the source's declarations.
    ///
    /// <b>Where the two disagree about which columns exist, this throws.</b> A
    /// seeded column with no source silently never renders; a source column with no
    /// seed row silently cannot be chosen. Both are invisible in testing and
    /// obvious to whoever needed that column at year end.
    /// </summary>
    public async Task<List<ReportColumnView>> ColumnsAsync(
        IReportSource source, ReportCurrencyView currency, CancellationToken ct)
    {
        Dictionary<string, ReportDetail> details = await DetailsAsync(source.ReportKey, ct);

        Validate(source, details);

        return
        [
            .. source.Columns
                .Select(column =>
                {
                    ReportDetail detail = details[column.Key];

                    return new ReportColumnView
                    {
                        Key = column.Key,
                        Header = Substitute(detail.Header, currency.Code),
                        DataType = column.DataType,
                        Alignment = detail.Alignment,
                        Width = detail.Width,
                        IsFilterable = column.IsFilterable && detail.IsFilterable,
                        IsSortable = column.IsSortable && detail.IsSortable,
                        IsGroupable = column.IsGroupable && detail.IsGroupable,
                        IsPivotable = detail.IsPivotable,
                        Aggregate = column.DefaultAggregate,
                        IsPrimary = detail.IsPrimary,
                    };
                })
                .Where(c => !details[c.Key].IsHidden),
        ];
    }

    /// <summary>
    /// Replaces <c>%CurCode%</c> in a seeded header with the branch's currency, so
    /// one seed row reads "Debit(INR)" in one branch and "Debit(AED)" in another.
    ///
    /// <b>When the currency is unknown the parenthetical goes with it</b> —
    /// "Debit(%CurCode%)" becomes "Debit", not "Debit()". A header that renders
    /// empty brackets looks like a bug on a printed report, and one that guessed
    /// a code would be worse: the figures are correct base-currency amounts either
    /// way, so the honest thing is to stop naming the currency rather than to name
    /// it wrongly.
    /// </summary>
    internal static string Substitute(string header, string currencyCode) =>
        currencyCode.Length > 0
            ? header.Replace("%CurCode%", currencyCode, StringComparison.Ordinal)
            : header
                .Replace("(%CurCode%)", string.Empty, StringComparison.Ordinal)
                .Replace("%CurCode%", string.Empty, StringComparison.Ordinal)
                .Trim();

    private async Task<List<string>> DefaultKeysAsync(IReportSource source, CancellationToken ct)
    {
        Dictionary<string, ReportDetail> details = await DetailsAsync(source.ReportKey, ct);

        return
        [
            .. details.Values
                .Where(d => d.IsDefault && !d.IsHidden)
                .OrderBy(d => d.SortOrder)
                .Select(d => d.ColumnKey),
        ];
    }

    private async Task<Dictionary<string, ReportDetail>> DetailsAsync(
        string reportKey, CancellationToken ct)
    {
        List<ReportDetail> rows = await _db.ReportDetails
            .Where(d => _db.Reports.Any(r => r.ReportId == d.ReportId && r.ReportKey == reportKey))
            .OrderBy(d => d.SortOrder)
            .ToListAsync(ct);

        return rows.ToDictionary(d => d.ColumnKey, StringComparer.Ordinal);
    }

    /// <summary>
    /// The check R0.4 promised as a startup validator.
    ///
    /// It runs when a report's columns are built rather than at startup, and that
    /// is not a compromise — the catalog is per-branch data in a per-customer
    /// database, so at startup there is no tenant to read it for and nothing to
    /// validate against. Here there is exactly one branch's catalog in hand.
    /// </summary>
    private static void Validate(IReportSource source, Dictionary<string, ReportDetail> details)
    {
        List<string> missing =
        [
            .. source.Columns.Select(c => c.Key).Where(key => !details.ContainsKey(key)),
        ];

        List<string> orphaned =
        [
            .. details.Keys.Where(key => source.Columns.All(c => c.Key != key)),
        ];

        if (missing.Count == 0 && orphaned.Count == 0)
        {
            return;
        }

        List<string> problems = [];

        if (missing.Count > 0)
        {
            problems.Add(
                $"declared by the report but not seeded: {string.Join(", ", missing)}");
        }

        if (orphaned.Count > 0)
        {
            problems.Add(
                $"seeded but not declared by the report: {string.Join(", ", orphaned)}");
        }

        throw new ReportQueryException(
            $"The '{source.ReportKey}' report and its seeded columns disagree — "
            + string.Join("; ", problems)
            + ". Reseed the catalog, or correct the report's column list.");
    }
}
