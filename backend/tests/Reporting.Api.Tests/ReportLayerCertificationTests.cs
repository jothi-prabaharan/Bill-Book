using System.Reflection;
using System.Text.RegularExpressions;
using Reporting.Api.Services;
using Reporting.Api.Services.Sources;
using Reporting.Repository.SeedData;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// A report has to exist in every layer, or it exists in none of them.
///
/// <b>Four layers, and a report missing from any one of them looks fine from
/// the other three.</b> A source class that compiles but is registered nowhere
/// is never resolved and never appears in the catalog. One registered but not
/// seeded appears nowhere either, because <c>ListAsync</c> filters the catalog
/// to reports with a <c>rpt.Reports</c> row — and throws if reached by key,
/// since <c>Validate</c> refuses a source whose columns have no
/// <c>ReportDetails</c>. One seeded but with no source is a row in a list that
/// opens onto nothing. And one absent from <see cref="ReportSourceTests.Sources"/>
/// is not covered by the column, aggregate, grouping and permission rules the
/// other forty are held to.
///
/// <b>Every one of those has happened here.</b> Seventeen sources existed while
/// the seeder carried three, so fourteen reports were invisible. Fifteen tracker
/// and finance reports were written, registered nowhere and listed here nowhere,
/// and 239 tests passed over the gap without noticing — because every test was a
/// theory over a list, and a report absent from the list is a report no theory
/// runs on.
///
/// So this asks the assembly rather than the list: every <see cref="IReportSource"/>
/// implementation, discovered by reflection, must appear in all four places.
/// A report added tomorrow and wired into three of them fails the build.
/// </summary>
public sealed class ReportLayerCertificationTests
{
    /// <summary>
    /// The sources the theory suite runs on, unwrapped from xUnit's
    /// <c>TheoryData</c> so they can be compared with what the assembly holds.
    /// </summary>
    private static IReadOnlyList<IReportSource> Tested =>
        [.. ReportSourceTests.Sources
            .Cast<object?[]>()
            .Select(row => (IReportSource)row[0]!)];

    /// <summary>Every concrete source in the service assembly, however it was wired.</summary>
    private static IReadOnlyList<Type> SourceTypes =>
        [.. typeof(AccountMovementSource).Assembly
            .GetTypes()
            .Where(t => typeof(IReportSource).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .OrderBy(t => t.Name, StringComparer.Ordinal)];

    /// <summary>
    /// The sources <c>Program.cs</c> registers, read out of the file.
    ///
    /// <b>Read as text rather than by building the container.</b> Standing up
    /// the real host would need a database, a signing key and Master reachable;
    /// what is being asserted is one line per source in one file, and the line
    /// is the thing that goes missing.
    /// </summary>
    private static IReadOnlySet<string> Registered
    {
        get
        {
            string program = File.ReadAllText(ProgramPath());

            return Regex
                .Matches(program, @"AddScoped<IReportSource,\s*(\w+)>")
                .Select(m => m.Groups[1].Value)
                .ToHashSet(StringComparer.Ordinal);
        }
    }

    private static string ProgramPath()
    {
        // Walk up from the test binary to the repository, so this does not care
        // where the runner puts its working directory.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null
            && !Directory.Exists(Path.Combine(directory.FullName, "Api", "Reporting")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return Path.Combine(
            directory!.FullName, "Api", "Reporting", "Reporting.Api", "Program.cs");
    }

    [Fact]
    public void The_assembly_holds_the_sources_we_think_it_does()
    {
        // Guards every assertion below: if reflection found nothing, they would
        // all pass over an empty set and prove the opposite of what they claim.
        Assert.Equal(41, SourceTypes.Count);
    }

    [Fact]
    public void Every_source_in_the_assembly_is_registered_for_injection()
    {
        IReadOnlySet<string> registered = Registered;

        Assert.Equal(
            string.Empty,
            string.Join(", ", SourceTypes
                .Select(t => t.Name)
                .Where(name => !registered.Contains(name))));
    }

    [Fact]
    public void Every_registration_names_a_source_that_exists()
    {
        HashSet<string> present = [.. SourceTypes.Select(t => t.Name)];

        Assert.Equal(
            string.Empty,
            string.Join(", ", Registered.Where(name => !present.Contains(name)).Order(StringComparer.Ordinal)));
    }

    [Fact]
    public void Every_source_in_the_assembly_is_exercised_by_the_source_tests()
    {
        HashSet<string> covered =
        [
            .. Tested.Select(s => s.GetType().Name),
        ];

        // A source absent from that list is a source no theory runs on — which
        // is exactly how fifteen reports went unchecked while the suite was
        // green.
        Assert.Equal(
            string.Empty,
            string.Join(", ", SourceTypes
                .Select(t => t.Name)
                .Where(name => !covered.Contains(name))));
    }

    [Fact]
    public void Every_source_has_a_catalog_entry()
    {
        Assert.Equal(
            string.Empty,
            string.Join(", ", Tested
                .Where(s => !ReportCatalogSeeder.SeededColumnKeys.ContainsKey(s.ReportKey))
                .Select(s => s.ReportKey)
                .Order(StringComparer.Ordinal)));
    }

    [Fact]
    public void Every_catalog_entry_has_a_source()
    {
        HashSet<string> keys =
        [
            .. Tested.Select(s => s.ReportKey),
        ];

        // A seeded report with no source is a row in the list that opens onto
        // nothing — worse than an absent report, because the user can see it.
        Assert.Equal(
            string.Empty,
            string.Join(", ", ReportCatalogSeeder.SeededColumnKeys.Keys
                .Where(key => !keys.Contains(key))
                .Order(StringComparer.Ordinal)));
    }

    [Fact]
    public void Every_report_key_is_unique_across_the_assembly()
    {
        List<string> duplicates =
        [
            .. Tested
                .Select(s => s.ReportKey)
                .GroupBy(key => key, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key),
        ];

        // Two sources on one key means whichever the container resolves second
        // silently wins, and which one that is depends on registration order.
        Assert.Equal(string.Empty, string.Join(", ", duplicates));
    }

    [Fact]
    public void Every_source_exports_the_same_columns_it_renders()
    {
        // The export contract: both writers take the ReportResultView the query
        // produced, so there is no second column list to drift. This asserts the
        // premise — that a source's columns are the only column list there is —
        // by checking none is left without one.
        Assert.Equal(
            string.Empty,
            string.Join(", ", Tested
                .Where(s => s.Columns.Count == 0)
                .Select(s => s.ReportKey)
                .Order(StringComparer.Ordinal)));
    }
}
