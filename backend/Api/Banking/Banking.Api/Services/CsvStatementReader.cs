using System.Text;

namespace Banking.Api.Services;

/// <summary>
/// Turns a CSV bank statement into rows of cells.
///
/// <b>Written rather than taken from a package, and that is a real decision.</b>
/// RFC 4180 is a page long, the whole of it is implemented below, and the
/// alternative was a dependency for eighty lines. What actually needs care is
/// not the grammar — it is that bank exports break it: descriptions contain
/// commas, quoted fields contain quotes, and a narration can run across a line
/// break inside its quotes. All three are handled here, because all three are in
/// real files.
///
/// <b>Delimiter is detected, not assumed.</b> A "CSV" from an Indian bank is
/// comma-separated about as often as it is tab- or semicolon-separated —
/// especially from a portal that localizes its exports. Detection is on the
/// first non-empty line, counting outside quotes.
/// </summary>
public static class CsvStatementReader
{
    private static readonly char[] Candidates = [',', '\t', ';', '|'];

    public static StatementGrid Read(Stream stream, int skipRows, bool hasHeaderRow)
    {
        // Detect the encoding from a byte-order mark when there is one and fall
        // back to UTF-8. A statement carrying a customer's name in Tamil or
        // Chinese read as Latin-1 is mojibake nobody can match against.
        using var reader = new StreamReader(
            stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        string text = reader.ReadToEnd();

        if (text.Length == 0)
        {
            throw new StatementReadException("The file is empty.");
        }

        char delimiter = DetectDelimiter(text);

        List<string[]> all = Split(text, delimiter);

        // Trailing blank rows are what a spreadsheet leaves behind, and an
        // all-empty row in the middle is a separator the bank drew rather than a
        // movement. Neither is data.
        all.RemoveAll(row => row.All(string.IsNullOrWhiteSpace));

        if (skipRows > 0)
        {
            all = [.. all.Skip(skipRows)];
        }

        if (all.Count == 0)
        {
            throw new StatementReadException(
                skipRows > 0
                    ? $"There is nothing left after skipping {skipRows} row(s)."
                    : "The file has no rows.");
        }

        if (!hasHeaderRow)
        {
            return new StatementGrid([], all);
        }

        return new StatementGrid(all[0], [.. all.Skip(1)]);
    }

    /// <summary>
    /// The delimiter that appears most often outside quotes on the first line
    /// with any content. Counting outside quotes matters: a description like
    /// "SHARMA, TRADERS" would otherwise make a comma win a tab-separated file.
    /// </summary>
    private static char DetectDelimiter(string text)
    {
        foreach (string line in text.Split('\n'))
        {
            string candidate = line.Trim('\r');
            if (candidate.Length == 0)
            {
                continue;
            }

            char best = ',';
            int bestCount = 0;

            foreach (char delimiter in Candidates)
            {
                int count = 0;
                bool quoted = false;

                foreach (char c in candidate)
                {
                    if (c == '"')
                    {
                        quoted = !quoted;
                    }
                    else if (c == delimiter && !quoted)
                    {
                        count++;
                    }
                }

                if (count > bestCount)
                {
                    best = delimiter;
                    bestCount = count;
                }
            }

            return best;
        }

        return ',';
    }

    /// <summary>
    /// RFC 4180, in full. A quoted field may contain the delimiter, a newline,
    /// and a doubled quote standing for one.
    /// </summary>
    private static List<string[]> Split(string text, char delimiter)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (quoted)
            {
                if (c == '"')
                {
                    // A doubled quote inside a quoted field is one quote.
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            if (c == '"' && field.Length == 0)
            {
                quoted = true;
            }
            else if (c == delimiter)
            {
                row.Add(field.ToString().Trim());
                field.Clear();
            }
            else if (c is '\n' or '\r')
            {
                // \r\n is one break, not two empty rows.
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                row.Add(field.ToString().Trim());
                field.Clear();
                rows.Add([.. row]);
                row.Clear();
            }
            else
            {
                field.Append(c);
            }
        }

        // Whatever the last line left, when the file does not end in a newline.
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString().Trim());
            rows.Add([.. row]);
        }

        return rows;
    }
}
