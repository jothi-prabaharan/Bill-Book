using Sales.Api.Services.EInvoicing;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>Each line's GST unit, copied onto it when a document posts (TK-91).</summary>
public sealed class LineUqcTests
{
    private sealed class Line(long? uomId, string? uqc = null)
    {
        public long? UomId { get; } = uomId;

        public string? UqcCode { get; set; } = uqc;
    }

    private static Task Stamp(StubUqcLookup lookup, params Line[] lines) =>
        LineUqc.StampAsync(lookup, lines, l => l.UomId, l => l.UqcCode, (l, code) => l.UqcCode = code, default);

    [Fact]
    public async Task Each_line_takes_its_units_uqc()
    {
        Line kg = new(2), nos = new(1);

        await Stamp(new StubUqcLookup(), kg, nos);

        Assert.Equal("KGS", kg.UqcCode);
        Assert.Equal("NOS", nos.UqcCode);
    }

    [Fact]
    public async Task A_line_with_no_unit_or_an_unknown_unit_reports_oth()
    {
        Line none = new(null), unknown = new(99);

        await Stamp(new StubUqcLookup(), none, unknown);

        Assert.Equal("OTH", none.UqcCode);
        Assert.Equal("OTH", unknown.UqcCode);
    }

    [Fact]
    public async Task A_line_that_already_has_a_uqc_keeps_it()
    {
        Line kept = new(1, "BOX");

        await Stamp(new StubUqcLookup(), kept);

        Assert.Equal("BOX", kept.UqcCode);
    }

    [Fact]
    public async Task Inventory_unreachable_leaves_the_lines_empty_rather_than_guessed()
    {
        Line line = new(1);

        await Stamp(new StubUqcLookup { Fail = true }, line);

        Assert.Null(line.UqcCode);
    }
}
