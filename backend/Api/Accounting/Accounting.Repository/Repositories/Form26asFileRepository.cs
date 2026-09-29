using System.Globalization;
using System.Text.RegularExpressions;

namespace Accounting.Repository.Repositories;

/// <summary>One TDS transaction as Form 26AS Part I reports it.</summary>
public sealed record Form26asTransaction(
    string DeductorTan,
    string? DeductorName,
    string? SectionCode,
    DateOnly? TransactionDate,
    string? BookingStatus,
    decimal AmountPaid,
    decimal Tds);

/// <summary>A parsed statement: whose, which year, and Part I's transactions.</summary>
public sealed record Form26asStatement(string? Pan, int FinancialYear, IReadOnlyList<Form26asTransaction> Transactions);

/// <summary>A file that is not a 26AS text download. The message is for the person importing.</summary>
public sealed class Form26asFormatException(string message) : Exception(message);

/// <summary>
/// Reads the Form 26AS text file TRACES downloads (TK-117, D-33). Pure.
///
/// The file is caret-separated, in parts. Part I ("Details of Tax Deducted at
/// Source") has a summary row per deductor — its name and TAN — followed by
/// that deductor's transactions, each starting with an empty field: serial,
/// section, transaction date, booking status, booking date, remarks, amount
/// paid, tax deducted, TDS deposited. Everything outside Part I is skipped.
///
/// Lenient about spacing, blank fields and the header wording, which TRACES
/// has changed over the years; strict about what matching needs: a financial
/// year, and a TAN and an amount on every transaction.
/// </summary>
public static partial class Form26asFileRepository
{
    [GeneratedRegex("^[A-Z]{4}[0-9]{5}[A-Z]$")]
    private static partial Regex Tan();

    [GeneratedRegex("^[A-Z]{5}[0-9]{4}[A-Z]$")]
    private static partial Regex Pan();

    /// <summary>"PART-I", "PART I - Details…", but not PART-II or PART-IV.</summary>
    [GeneratedRegex("^PART[\\s-]*I(?![IVX])")]
    private static partial Regex PartOne();

    [GeneratedRegex("^(20[0-9]{2})-([0-9]{2})$")]
    private static partial Regex Year();

    private static readonly string[] DateFormats = ["dd-MMM-yyyy", "dd-MM-yyyy", "dd/MM/yyyy", "yyyy-MM-dd"];

    public static Form26asStatement Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || !text.Contains('^'))
        {
            throw new Form26asFormatException(
                "That file is not a Form 26AS text file. On TRACES, view Form 26AS, choose 'Text' as the format, and import the .txt it downloads.");
        }

        string? pan = null;
        int? year = null;
        bool inPartOne = false;
        string? tan = null;
        string? name = null;
        List<Form26asTransaction> transactions = [];

        foreach (string raw in text.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            string[] fields = [.. line.Split('^').Select(f => f.Trim())];
            string upper = line.ToUpperInvariant();

            if (upper.StartsWith("PART", StringComparison.Ordinal) || upper.StartsWith("^PART", StringComparison.Ordinal))
            {
                inPartOne = PartOne().IsMatch(upper.TrimStart('^'));
                tan = null;
                name = null;
                continue;
            }

            // The header: the PAN, and the year as "2025-26".
            if (pan is null && fields.Length > 0 && Pan().IsMatch(fields[0].ToUpperInvariant()))
            {
                pan = fields[0].ToUpperInvariant();
            }

            if (year is null)
            {
                foreach (string field in fields)
                {
                    Match m = Year().Match(field);
                    if (m.Success && int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) == (int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) + 1) % 100)
                    {
                        year = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                        break;
                    }
                }
            }

            if (!inPartOne)
            {
                continue;
            }

            // A deductor's summary row: serial, name, TAN.
            if (fields.Length >= 3 && fields[0].Length > 0 && Tan().IsMatch(fields[2].ToUpperInvariant()))
            {
                name = fields[1].Length == 0 ? null : fields[1];
                tan = fields[2].ToUpperInvariant();
                continue;
            }

            // A transaction under the current deductor: starts empty, then a serial number.
            if (tan is not null && fields.Length >= 8 && fields[0].Length == 0 && int.TryParse(fields[1], out _))
            {
                List<string> tail = [.. fields.Reverse().SkipWhile(f => f.Length == 0).Take(3).Reverse()];
                if (tail.Count < 3 || !TryMoney(tail[0], out decimal paid) || !TryMoney(tail[1], out decimal tds))
                {
                    throw new Form26asFormatException($"A transaction of {tan} has no readable amounts: '{line}'.");
                }

                DateOnly? date = DateOnly.TryParseExact(fields[3], DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsed)
                    ? parsed
                    : null;

                transactions.Add(new Form26asTransaction(
                    tan, name, fields[2].Length == 0 ? null : fields[2], date,
                    fields.Length > 4 && fields[4].Length > 0 ? fields[4] : null, paid, tds));
            }
        }

        if (year is null)
        {
            throw new Form26asFormatException("The file names no financial year (such as 2025-26), so it is not a Form 26AS download.");
        }

        return new Form26asStatement(pan, year.Value, transactions);
    }

    private static bool TryMoney(string text, out decimal value) =>
        decimal.TryParse(text.Replace(",", string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
}
