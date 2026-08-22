using System.Text.Json;
using Shared.Kernel.Documents;
using Shared.Kernel.Tax;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// The C# half of the shared tax fixture.
///
/// <b>The same file drives <c>line-math.spec.ts</c>.</b> Two implementations of
/// one sum diverge by a paisa without either looking wrong, and a GST return is
/// where that surfaces months later — so the expected answers live in one place
/// and both sides are held to them. Two copies of the expectations would be two
/// answers, which is the problem rather than the check.
///
/// Amounts in the fixture are rupees with two decimals, which is how C# holds
/// them; the TypeScript multiplies by a hundred on the way in because
/// JavaScript has no decimal. That conversion is the only thing the two sides
/// are allowed to do differently.
/// </summary>
public class GstCalculatorFixtureTests
{
    private static readonly TaxFixture Fixture = Load();

    public static TheoryData<string> LineCaseNames()
    {
        var data = new TheoryData<string>();
        foreach (LineCase c in Fixture.LineCases)
        {
            data.Add(c.Name);
        }

        return data;
    }

    public static TheoryData<string> DocumentCaseNames()
    {
        var data = new TheoryData<string>();
        foreach (DocumentCase c in Fixture.DocumentCases)
        {
            data.Add(c.Name);
        }

        return data;
    }

    public static TheoryData<string> PlaceOfSupplyCaseNames()
    {
        var data = new TheoryData<string>();
        foreach (PlaceCase c in Fixture.PlaceOfSupplyCases)
        {
            data.Add(c.Name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(LineCaseNames))]
    public void Line_matches_the_fixture(string name)
    {
        LineCase testCase = Fixture.LineCases.Single(c => c.Name == name);

        TaxLineResult got = GstCalculator.Compute(
            ToInput(testCase.Line), ToContext(testCase.Context));

        LineExpectation want = testCase.Expected;

        Assert.Equal(want.GrossAmount, got.GrossAmount);
        Assert.Equal(want.DiscountAmount, got.DiscountAmount);
        Assert.Equal(want.TaxableAmount, got.TaxableAmount);
        Assert.Equal(want.TaxAmount, got.TaxAmount);
        Assert.Equal(want.LineTotal, got.LineTotal);

        Assert.Equal(want.Components.Count, got.Components.Count);

        for (int i = 0; i < want.Components.Count; i++)
        {
            Assert.Equal(
                Enum.Parse<TaxComponent>(want.Components[i].Component),
                got.Components[i].Component);
            Assert.Equal(want.Components[i].Rate, got.Components[i].Rate);
            Assert.Equal(want.Components[i].Amount, got.Components[i].Amount);
        }
    }

    [Theory]
    [MemberData(nameof(DocumentCaseNames))]
    public void Document_totals_match_the_fixture(string name)
    {
        DocumentCase testCase = Fixture.DocumentCases.Single(c => c.Name == name);
        TaxContext context = ToContext(testCase.Context);

        List<TaxLineResult> lines =
            [.. testCase.Lines.Select(l => GstCalculator.Compute(ToInput(l), context))];

        TaxDocumentTotals got = GstCalculator.Totals(lines);
        DocumentExpectation want = testCase.Expected;

        Assert.Equal(want.SubTotal, got.SubTotal);
        Assert.Equal(want.DiscountAmount, got.DiscountAmount);
        Assert.Equal(want.TaxableAmount, got.TaxableAmount);
        Assert.Equal(want.CgstAmount, got.CgstAmount);
        Assert.Equal(want.SgstAmount, got.SgstAmount);
        Assert.Equal(want.IgstAmount, got.IgstAmount);
        Assert.Equal(want.CessAmount, got.CessAmount);
        Assert.Equal(want.TotalAmount, got.TotalAmount);
    }

    /// <summary>
    /// The header must equal the sum of the lines printed above it. Checked
    /// separately from the expected figures because it is the property that has
    /// to hold for <i>every</i> document, not only the ones written down here.
    /// </summary>
    [Theory]
    [MemberData(nameof(DocumentCaseNames))]
    public void Document_total_is_the_sum_of_its_lines(string name)
    {
        DocumentCase testCase = Fixture.DocumentCases.Single(c => c.Name == name);
        TaxContext context = ToContext(testCase.Context);

        List<TaxLineResult> lines =
            [.. testCase.Lines.Select(l => GstCalculator.Compute(ToInput(l), context))];

        TaxDocumentTotals totals = GstCalculator.Totals(lines);

        Assert.Equal(lines.Sum(l => l.TaxAmount),
            totals.CgstAmount + totals.SgstAmount + totals.IgstAmount + totals.CessAmount);

        Assert.Equal(lines.Sum(l => l.LineTotal), totals.TotalAmount);
    }

    [Theory]
    [MemberData(nameof(PlaceOfSupplyCaseNames))]
    public void Place_of_supply_matches_the_fixture(string name)
    {
        PlaceCase testCase = Fixture.PlaceOfSupplyCases.Single(c => c.Name == name);

        PlaceOfSupplyResult got = PlaceOfSupply.Resolve(
            testCase.BranchStateCode, testCase.PlaceOfSupplyStateCode, testCase.ContactGstin);

        Assert.Equal(
            Enum.Parse<PlaceOfSupplyOutcome>(testCase.Expected.Outcome), got.Outcome);

        if (got.Outcome != PlaceOfSupplyOutcome.Ok)
        {
            // Every refusal says something a user can act on. A refusal with no
            // reason is the one somebody has to guess at.
            Assert.False(string.IsNullOrWhiteSpace(got.Detail));
            return;
        }

        Assert.Equal(testCase.Expected.StateCode, got.StateCode);
        Assert.Equal(testCase.Expected.IsInterState, got.IsInterState);
    }

    private static TaxLineInput ToInput(FixtureLine line) => new()
    {
        Quantity = line.Quantity,
        UnitPrice = line.UnitPrice,
        DiscountPercent = line.DiscountPercent,
        DiscountAmount = line.DiscountAmount,
        IsPriceInclusive = line.IsPriceInclusive,
        TaxTreatment = Enum.Parse<TaxTreatment>(line.TaxTreatment),
        Rate = line.Rate is null ? null : Fixture.Rates[line.Rate],
    };

    private static TaxContext ToContext(FixtureContext context) =>
        new(context.IsInterState, context.DiscountBeforeTax, context.IsUnionTerritory);

    private static TaxFixture Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "tax-fixture.json");

        return JsonSerializer.Deserialize<TaxFixture>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException($"Could not read the tax fixture at {path}.");
    }

    // The fixture's shape. Records rather than the production models, so that a
    // change to a production model that the fixture does not follow is a
    // compile error here rather than a silently skipped field.

    private sealed record TaxFixture(
        Dictionary<string, TaxRate> Rates,
        List<LineCase> LineCases,
        List<DocumentCase> DocumentCases,
        List<PlaceCase> PlaceOfSupplyCases);

    /// <summary>
    /// <c>IsUnionTerritory</c> is defaulted, so the cases written before UTGST
    /// existed keep parsing without the key. It has to be carried into the
    /// <see cref="TaxContext"/> above, though — a reader that dropped it would
    /// run the UT cases as ordinary intra-state ones and pass them for the
    /// wrong reason, which is exactly the divergence this fixture exists to
    /// catch.
    /// </summary>
    private sealed record FixtureContext(
        bool IsInterState, bool DiscountBeforeTax, bool IsUnionTerritory = false);

    private sealed record FixtureLine(
        decimal Quantity,
        decimal UnitPrice,
        decimal? DiscountPercent,
        decimal DiscountAmount,
        bool IsPriceInclusive,
        string TaxTreatment,
        string? Rate);

    private sealed record ComponentExpectation(string Component, decimal Rate, decimal Amount);

    private sealed record LineExpectation(
        decimal GrossAmount,
        decimal DiscountAmount,
        decimal TaxableAmount,
        decimal TaxAmount,
        decimal LineTotal,
        List<ComponentExpectation> Components);

    private sealed record LineCase(
        string Name, FixtureContext Context, FixtureLine Line, LineExpectation Expected);

    private sealed record DocumentExpectation(
        decimal SubTotal,
        decimal DiscountAmount,
        decimal TaxableAmount,
        decimal CgstAmount,
        decimal SgstAmount,
        decimal IgstAmount,
        decimal CessAmount,
        decimal TotalAmount);

    private sealed record DocumentCase(
        string Name,
        FixtureContext Context,
        List<FixtureLine> Lines,
        DocumentExpectation Expected);

    private sealed record PlaceExpectation(string Outcome, string? StateCode, bool IsInterState);

    private sealed record PlaceCase(
        string Name,
        string? BranchStateCode,
        string? PlaceOfSupplyStateCode,
        string? ContactGstin,
        PlaceExpectation Expected);
}
