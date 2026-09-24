using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace RateSync.Worker.Rbi;

/// <summary>One reference rate read off the page: one unit of the currency in rupees.</summary>
public sealed record ReferenceRate(string CurrencyCode, decimal RateInInr);

/// <summary>The page's rates and the date it says they are for.</summary>
public sealed record ReferenceRatePage(DateOnly RateDate, IReadOnlyList<ReferenceRate> Rates);

/// <summary>
/// Reads the reference rates off RBI's reference-rate page (D-03, TK-26).
///
/// <b>Isolated on purpose.</b> RBI has no API, so this is a scrape, and a scrape
/// breaks when the page changes. Everything that depends on the page's shape is
/// in this one class, which takes a string and touches nothing else — so a
/// changed page is a changed parser and a new saved copy for its tests.
///
/// <b>What it looks for</b>, on the page's text with the markup removed:
/// <list type="bullet">
/// <item>rows written <c>INR / 1 USD</c> followed by a figure, the form RBI and
/// FBIL publish in, with the unit read so <c>INR / 100 JPY</c> becomes the rate
/// for one yen;</item>
/// <item>a date after the words "reference rate", written as
/// <c>24 September 2026</c>, <c>Sep 24, 2026</c>, <c>24-09-2026</c> or
/// <c>24/09/2026</c> — day first, as an Indian page writes it.</item>
/// </list>
///
/// <b>It refuses rather than guesses.</b> No date, or no rate, or a figure that
/// is not a positive number, throws <see cref="RbiPageFormatException"/>; the
/// caller records a failed run and writes nothing. A rate written from a page
/// that was misread would be snapshotted into invoices, which is worse than a
/// day with no rate.
///
/// <b>Not yet checked against the live page.</b> The session that wrote this
/// could not reach www.rbi.org.in, so its test copy is synthetic. See TK-26.
/// </summary>
public static partial class RbiReferenceRateParser
{
    public static ReferenceRatePage Parse(string html)
    {
        string text = Text(html);

        DateOnly date = ReadDate(text)
            ?? throw new RbiPageFormatException("The page names no reference-rate date.");

        var rates = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (Match row in RateRow().Matches(text))
        {
            string code = row.Groups["code"].Value;
            int unit = row.Groups["unit"].Success ? int.Parse(row.Groups["unit"].Value, CultureInfo.InvariantCulture) : 1;
            string figure = row.Groups["rate"].Value.Replace(",", string.Empty, StringComparison.Ordinal);

            if (unit <= 0
                || !decimal.TryParse(figure, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal perUnit)
                || perUnit <= 0m)
            {
                throw new RbiPageFormatException($"The rate for {code} is not a positive number.");
            }

            // The first figure for a currency is the day's; a later one on the
            // same page is an older day in a history table.
            rates.TryAdd(code, Math.Round(perUnit / unit, 8, MidpointRounding.AwayFromZero));
        }

        if (rates.Count == 0)
        {
            throw new RbiPageFormatException("The page holds no reference rate.");
        }

        return new ReferenceRatePage(date, rates.Select(r => new ReferenceRate(r.Key, r.Value)).ToList());
    }

    /// <summary>The page's words, with tags, scripts and entities gone and whitespace collapsed.</summary>
    internal static string Text(string html)
    {
        string withoutScripts = Scripts().Replace(html, " ");
        string withoutTags = Tags().Replace(withoutScripts, " ");
        return Whitespace().Replace(WebUtility.HtmlDecode(withoutTags), " ").Trim();
    }

    private static DateOnly? ReadDate(string text)
    {
        int anchor = text.IndexOf("reference rate", StringComparison.OrdinalIgnoreCase);
        string window = anchor < 0 ? text : text[anchor..Math.Min(text.Length, anchor + 300)];

        Match named = NamedMonthDate().Match(window);
        if (named.Success)
        {
            string day = named.Groups["day"].Success ? named.Groups["day"].Value : named.Groups["day2"].Value;
            string month = named.Groups["month"].Success ? named.Groups["month"].Value : named.Groups["month2"].Value;
            string year = named.Groups["year"].Success ? named.Groups["year"].Value : named.Groups["year2"].Value;

            int monthNumber = Array.FindIndex(
                CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedMonthNames,
                m => m.Length > 0 && month.StartsWith(m, StringComparison.OrdinalIgnoreCase)) + 1;

            return Date(int.Parse(year, CultureInfo.InvariantCulture), monthNumber, int.Parse(day, CultureInfo.InvariantCulture));
        }

        Match numeric = NumericDate().Match(window);
        if (numeric.Success)
        {
            return Date(
                int.Parse(numeric.Groups["year"].Value, CultureInfo.InvariantCulture),
                int.Parse(numeric.Groups["month"].Value, CultureInfo.InvariantCulture),
                int.Parse(numeric.Groups["day"].Value, CultureInfo.InvariantCulture));
        }

        return null;
    }

    private static DateOnly? Date(int year, int month, int day) =>
        month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month)
            ? new DateOnly(year, month, day)
            : null;

    [GeneratedRegex(@"<(script|style)\b[^>]*>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Scripts();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"INR\s*/\s*(?<unit>\d+)?\s*(?<code>[A-Z]{3})\b\s*:?\s*(?<rate>\d{1,3}(?:,\d{2,3})*(?:\.\d+)?|\d+(?:\.\d+)?)")]
    private static partial Regex RateRow();

    [GeneratedRegex(
        @"\b(?:(?<day>\d{1,2})(?:st|nd|rd|th)?[\s\-]+(?<month>Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\.?[\s\-,]+(?<year>\d{4})|(?<month2>Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\.?\s+(?<day2>\d{1,2}),?\s+(?<year2>\d{4}))\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex NamedMonthDate();

    [GeneratedRegex(@"\b(?<day>\d{1,2})[\-/.](?<month>\d{1,2})[\-/.](?<year>\d{4})\b")]
    private static partial Regex NumericDate();
}

/// <summary>The page could not be read as reference rates. Nothing is written.</summary>
public sealed class RbiPageFormatException(string message) : Exception(message);
