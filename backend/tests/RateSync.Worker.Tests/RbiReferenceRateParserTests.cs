using RateSync.Worker.Rbi;
using Xunit;

namespace RateSync.Worker.Tests;

/// <summary>
/// The RBI reference-rate parser (TK-26). The page copy is <b>synthetic</b>,
/// because the page could not be fetched when this was written; see the comment
/// at the top of the fixture.
/// </summary>
public sealed class RbiReferenceRateParserTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void Reads_the_date_and_every_rate_off_the_page()
    {
        ReferenceRatePage page = RbiReferenceRateParser.Parse(Fixture("rbi-reference-rate.synthetic.html"));

        Assert.Equal(new DateOnly(2026, 9, 24), page.RateDate);
        Assert.Equal(83.1245m, Rate(page, "USD"));
        Assert.Equal(110.8820m, Rate(page, "GBP"));
        Assert.Equal(92.4501m, Rate(page, "EUR"));
    }

    [Fact]
    public void A_rate_quoted_per_hundred_is_stored_per_one()
    {
        ReferenceRatePage page = RbiReferenceRateParser.Parse(Fixture("rbi-reference-rate.synthetic.html"));

        Assert.Equal(0.5761m, Rate(page, "JPY"));
    }

    [Fact]
    public void The_first_figure_for_a_currency_wins_and_scripts_are_ignored()
    {
        ReferenceRatePage page = RbiReferenceRateParser.Parse(Fixture("rbi-reference-rate.synthetic.html"));

        // The history table below repeats USD at an older figure, and a script
        // holds a decoy; neither is the day's rate.
        Assert.Single(page.Rates, r => r.CurrencyCode == "USD");
        Assert.Equal(83.1245m, Rate(page, "USD"));
    }

    [Theory]
    [InlineData("Reference Rate for 24-09-2026: INR / 1 USD 83.12")]
    [InlineData("Reference Rate for 24/09/2026 INR/1 USD 83.12")]
    [InlineData("Reference rate Sep 24, 2026 INR / 1 USD 83.12")]
    [InlineData("REFERENCE RATE 24th Sept 2026 INR / 1 USD : 83.12")]
    public void Reads_the_date_in_the_ways_an_indian_page_writes_it(string text)
    {
        ReferenceRatePage page = RbiReferenceRateParser.Parse($"<p>{text}</p>");

        Assert.Equal(new DateOnly(2026, 9, 24), page.RateDate);
        Assert.Equal(83.12m, Rate(page, "USD"));
    }

    [Theory]
    [InlineData("<p>INR / 1 USD 83.12</p>")]                                   // no date
    [InlineData("<p>Reference Rate as on 24 September 2026</p>")]               // no rate
    [InlineData("<p>Reference Rate as on 31 February 2026 INR / 1 USD 83.12</p>")] // no such day
    [InlineData("<p>Reference Rate as on 24 September 2026 INR / 1 USD 0.00</p>")] // not positive
    [InlineData("<p>Service unavailable</p>")]
    public void A_page_that_cannot_be_read_is_refused_rather_than_guessed(string html)
    {
        Assert.Throws<RbiPageFormatException>(() => RbiReferenceRateParser.Parse(html));
    }

    private static decimal Rate(ReferenceRatePage page, string code) =>
        Assert.Single(page.Rates, r => r.CurrencyCode == code).RateInInr;
}
