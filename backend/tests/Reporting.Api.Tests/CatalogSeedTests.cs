using Reporting.Api.Services;
using Reporting.Repository.SeedData;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The source and the seed must agree about which columns a report has.
///
/// <b>They already do agree at run time, and that is the problem.</b>
/// <c>ReportCatalogService.Validate</c> refuses a report whose two lists differ —
/// but it only runs when somebody opens that report, on a branch with data. A
/// column added to a source and forgotten here is invisible until a customer hits
/// it; a report seeded with no source row never appears in the list at all and
/// nothing says why. Both of those have happened in this codebase.
///
/// These tests move that check to the build, where it costs nothing.
/// </summary>
public class CatalogSeedTests
{
    [Theory]
    [MemberData(nameof(ReportSourceTests.Sources), MemberType = typeof(ReportSourceTests))]
    public void Every_declared_column_has_a_seed_row(IReportSource source)
    {
        Assert.True(
            ReportCatalogSeeder.SeededColumnKeys.TryGetValue(
                source.ReportKey, out IReadOnlyList<string>? seeded),
            $"{source.ReportKey} declares columns but has no catalog entry, so it "
            + "never appears in the report list.");

        string[] missing =
        [
            .. source.Columns
                .Select(c => c.Key)
                .Where(key => !seeded!.Contains(key)),
        ];

        Assert.True(
            missing.Length == 0,
            $"{source.ReportKey} declares {string.Join(", ", missing)} with no seed "
            + "row, which refuses the whole report rather than omitting the column.");
    }

    [Theory]
    [MemberData(nameof(ReportSourceTests.Sources), MemberType = typeof(ReportSourceTests))]
    public void Every_seeded_column_is_declared_by_the_source(IReportSource source)
    {
        if (!ReportCatalogSeeder.SeededColumnKeys.TryGetValue(
                source.ReportKey, out IReadOnlyList<string>? seeded))
        {
            return;
        }

        HashSet<string> declared = [.. source.Columns.Select(c => c.Key)];

        string[] orphaned = [.. seeded.Where(key => !declared.Contains(key))];

        Assert.True(
            orphaned.Length == 0,
            $"{source.ReportKey} seeds {string.Join(", ", orphaned)} with nothing to "
            + "read them — a header over a column that can never hold a value.");
    }
}
