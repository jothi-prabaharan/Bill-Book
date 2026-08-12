using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Accounting.Entity.TableEntities;

namespace Accounting.Api.Services;

/// <summary>
/// Turns rows of cells into statement lines, using the profile that says which
/// column is which.
///
/// <b>Every failure here names the row and what it could not read.</b> A person
/// importing a statement is looking at the file; "row 47: could not read
/// '31-04-2026' as a date using dd/MM/yyyy" is something they can act on, and
/// "import failed" is not.
///
/// <b>Nothing is guessed.</b> The date format comes from the profile rather than
/// from trying several — 03/04/2026 is a real date under three of them, and a
/// parser that shops around picks the wrong one exactly on the days when nobody
/// would notice.
/// </summary>
public static class StatementRowMapper
{
    /// <summary>What the fields of a row hash are joined on. See where it is used.</summary>
    private const char FieldSeparator = '\u001f';

    /// <summary>
    /// Reads the grid. Returns the lines in file order, with their row hashes
    /// computed and their occurrence numbers resolved.
    /// </summary>
    /// <exception cref="StatementReadException">A row could not be read.</exception>
    public static List<BankStatementLine> Map(StatementGrid grid, StatementImportProfile profile)
    {
        Dictionary<string, int> columns = Index(grid.Header);

        // How many times this exact movement has already been seen in this file.
        // Two identical ₹500 withdrawals on one day are two movements, not a
        // duplicate — so the occurrence goes into the hash and the second one
        // gets its own identity rather than colliding with the first.
        var occurrences = new Dictionary<string, int>();

        var lines = new List<BankStatementLine>(grid.Rows.Count);

        for (int i = 0; i < grid.Rows.Count; i++)
        {
            string[] row = grid.Rows[i];

            // The row number a person sees in their spreadsheet, counting the
            // skipped rows and the header. Anything else sends them to the wrong
            // line when they go looking.
            int fileRow = profile.SkipRows + (profile.HasHeaderRow ? 2 : 1) + i;

            string dateText = Cell(row, columns, profile.DateColumn);

            // A trailing total row is the usual reason a row has no date, and it
            // is not an error — it is the bank's footer.
            if (dateText.Length == 0)
            {
                continue;
            }

            DateOnly date = ParseDate(dateText, profile.DateFormat, fileRow, profile.DateColumn);

            (decimal withdrawal, decimal deposit) = ParseAmounts(row, columns, profile, fileRow);

            // A row that moved nothing is a separator, an opening-balance line or
            // a carried-forward marker. None of them is a movement.
            if (withdrawal == 0 && deposit == 0)
            {
                continue;
            }

            string description = Cell(row, columns, profile.DescriptionColumn);
            string reference = Cell(row, columns, profile.ReferenceColumn);

            string basis = string.Join(
                // ASCII unit separator, written as an escape rather than pasted:
                // it is invisible in an editor, and a separator nobody can see is
                // one somebody deletes. It is the right character because it
                // cannot occur in a bank narration — joining on a comma would let
                // a description ending in one collide with the next field, and
                // two different movements would then share a hash.
                FieldSeparator,
                profile.BankAccountId,
                date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                withdrawal.ToString(CultureInfo.InvariantCulture),
                deposit.ToString(CultureInfo.InvariantCulture),
                Normalize(description),
                Normalize(reference));

            occurrences[basis] = occurrences.GetValueOrDefault(basis) + 1;

            lines.Add(new BankStatementLine
            {
                BankAccountId = profile.BankAccountId,
                LineNumber = lines.Count + 1,
                TransactionDate = date,
                ValueDate = profile.ValueDateColumn is { Length: > 0 } valueDateColumn
                    && Cell(row, columns, valueDateColumn) is { Length: > 0 } valueDateText
                        ? ParseDate(valueDateText, profile.DateFormat, fileRow, valueDateColumn)
                        : null,
                Description = Trim(description, 500),
                ReferenceNo = Trim(reference, 100),
                WithdrawalAmount = withdrawal,
                DepositAmount = deposit,
                RunningBalance = profile.BalanceColumn is { Length: > 0 } balanceColumn
                    ? TryAmount(Cell(row, columns, balanceColumn))
                    : null,
                RowHash = Hash($"{basis}{occurrences[basis]}"),
            });
        }

        if (lines.Count == 0)
        {
            throw new StatementReadException(
                "No movements were found. Check the column mapping and how many rows to skip — "
                    + "every row was either blank, undated or for nothing.");
        }

        return lines;
    }

    /// <summary>
    /// Column name to position, case- and space-insensitively. Bank headers are
    /// inconsistent about both — "Withdrawal Amt." and "withdrawal amt" are the
    /// same column to everyone except a lookup that compares them literally.
    /// </summary>
    private static Dictionary<string, int> Index(IReadOnlyList<string> header)
    {
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < header.Count; i++)
        {
            string key = Normalize(header[i]);

            // First wins. A duplicate header is a bank quirk, and silently
            // rebinding the name to the later one would move the column under a
            // mapping that was already chosen.
            if (key.Length > 0 && !columns.ContainsKey(key))
            {
                columns[key] = i;
            }
        }

        return columns;
    }

    /// <summary>
    /// One cell, by column name when there is a header and by position when there
    /// is not. A column named in the profile but absent from the file reads as
    /// empty rather than throwing — the mapping may legitimately name an optional
    /// column this particular export omitted.
    /// </summary>
    private static string Cell(
        string[] row, Dictionary<string, int> columns, string? column)
    {
        if (column is not { Length: > 0 })
        {
            return string.Empty;
        }

        int index = columns.TryGetValue(Normalize(column), out int named)
            ? named
            : int.TryParse(column, NumberStyles.Integer, CultureInfo.InvariantCulture, out int position)
                ? position
                : -1;

        return index >= 0 && index < row.Length ? row[index].Trim() : string.Empty;
    }

    private static (decimal Withdrawal, decimal Deposit) ParseAmounts(
        string[] row, Dictionary<string, int> columns, StatementImportProfile profile, int fileRow)
    {
        // One signed column. Positive is money in unless the bank does it the
        // other way round, which some do.
        if (profile.AmountColumn is { Length: > 0 } amountColumn)
        {
            string text = Cell(row, columns, amountColumn);

            if (text.Length == 0)
            {
                return (0m, 0m);
            }

            decimal amount = ParseAmount(text, fileRow, amountColumn);

            bool isDeposit = profile.NegativeIsDeposit ? amount < 0 : amount > 0;

            return isDeposit ? (0m, Math.Abs(amount)) : (Math.Abs(amount), 0m);
        }

        decimal withdrawal = TryAmount(Cell(row, columns, profile.WithdrawalColumn)) ?? 0m;
        decimal deposit = TryAmount(Cell(row, columns, profile.DepositColumn)) ?? 0m;

        // Both filled is a mapping pointing at one column twice, or a file whose
        // columns are not what the profile says. Either way the amounts would be
        // wrong in a way nothing downstream could detect.
        if (withdrawal != 0 && deposit != 0)
        {
            throw new StatementReadException(
                $"Row {fileRow} has an amount in both the withdrawal and the deposit column. "
                    + "Check the column mapping — a movement is one or the other.");
        }

        return (Math.Abs(withdrawal), Math.Abs(deposit));
    }

    private static DateOnly ParseDate(string text, string format, int fileRow, string column)
    {
        // The profile's format first, then ISO — the Excel reader writes dates it
        // recognised as ISO, and those are the same cells the profile's format
        // would fail on.
        if (DateOnly.TryParseExact(
                text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly exact))
        {
            return exact;
        }

        if (DateOnly.TryParseExact(
                text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly iso))
        {
            return iso;
        }

        // Some exports put a time on the date. Only tried after the two exact
        // forms, so it can never reinterpret a date those already understood.
        if (DateTime.TryParseExact(
                text,
                [$"{format} HH:mm:ss", $"{format} HH:mm", "yyyy-MM-dd HH:mm:ss"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime stamped))
        {
            return DateOnly.FromDateTime(stamped);
        }

        throw new StatementReadException(
            $"Row {fileRow}: '{text}' in column '{column}' is not a date in the format "
                + $"'{format}'. Change the date format on the import, or check the mapping.");
    }

    private static decimal ParseAmount(string text, int fileRow, string column) =>
        TryAmount(text)
            ?? throw new StatementReadException(
                $"Row {fileRow}: '{text}' in column '{column}' is not an amount.");

    /// <summary>
    /// An amount as banks actually write them: thousands separators, a currency
    /// symbol, a trailing <c>Cr</c> or <c>Dr</c>, or parentheses for negative.
    /// Null when the cell is empty, which is the ordinary case for the side of a
    /// two-column layout a movement did not use.
    /// </summary>
    private static decimal? TryAmount(string? text)
    {
        if (text is not { Length: > 0 })
        {
            return null;
        }

        string cleaned = text.Trim();
        bool negative = false;

        // Accounting's parentheses. Checked before the characters are stripped,
        // because stripping them first loses the sign entirely.
        if (cleaned.StartsWith('(') && cleaned.EndsWith(')'))
        {
            negative = true;
            cleaned = cleaned[1..^1];
        }

        if (cleaned.EndsWith("CR", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[..^2];
        }
        else if (cleaned.EndsWith("DR", StringComparison.OrdinalIgnoreCase))
        {
            negative = true;
            cleaned = cleaned[..^2];
        }

        var digits = new StringBuilder(cleaned.Length);

        foreach (char c in cleaned)
        {
            if (char.IsDigit(c) || c == '.' || c == '-')
            {
                digits.Append(c);
            }
        }

        if (digits.Length == 0
            || !decimal.TryParse(
                digits.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal value))
        {
            return null;
        }

        return negative ? -Math.Abs(value) : value;
    }

    /// <summary>
    /// Collapses whitespace and case, so the same narration exported twice hashes
    /// the same way. Banks re-pad their descriptions between exports of the same
    /// period, and a hash that noticed would import every line twice.
    /// </summary>
    private static string Normalize(string? text) =>
        text is not { Length: > 0 }
            ? string.Empty
            : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                .ToUpperInvariant();

    private static string? Trim(string text, int max) =>
        text.Length == 0 ? null : text.Length <= max ? text : text[..max];

    private static string Hash(string basis) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(basis)));
}
