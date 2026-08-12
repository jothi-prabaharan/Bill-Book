using System.Text;
using Accounting.Api.Services;
using Accounting.Entity.TableEntities;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// Reading a bank's file, and turning its rows into movements.
///
/// <b>No database needed for any of this</b>, which is the point of keeping the
/// readers and the mapper out of the service: what is being proved here is
/// parsing, and parsing is a pure function of bytes and a profile.
///
/// The cases are the ones real bank exports actually produce. None of them is
/// hypothetical — commas inside narrations, a preamble above the header, one
/// signed column instead of two, and the same file downloaded twice.
/// </summary>
public class StatementImportTests
{
    /// <summary>The two-column layout, which is what most Indian banks export.</summary>
    [Fact]
    public void A_two_column_statement_reads_into_movements()
    {
        StatementGrid grid = Csv(
            """
            Date,Narration,Chq/Ref No,Withdrawal Amt.,Deposit Amt.,Closing Balance
            01/04/2026,NEFT DR-SHARMA TRADERS,N0412345,"11,000.00",,"1,89,000.00"
            03/04/2026,UPI-RAMESH-9876543210,UPI412999,,"5,000.00","1,94,000.00"
            """);

        List<BankStatementLine> lines = StatementRowMapper.Map(grid, TwoColumn());

        Assert.Equal(2, lines.Count);

        Assert.Equal(new DateOnly(2026, 4, 1), lines[0].TransactionDate);
        Assert.Equal(11_000m, lines[0].WithdrawalAmount);
        Assert.Equal(0m, lines[0].DepositAmount);
        Assert.Equal("N0412345", lines[0].ReferenceNo);

        // Indian digit grouping — 1,89,000 rather than 189,000 — parsed by
        // stripping separators rather than by a culture that would read the
        // first group as thousands.
        Assert.Equal(189_000m, lines[0].RunningBalance);

        Assert.Equal(5_000m, lines[1].DepositAmount);
        Assert.Equal(0m, lines[1].WithdrawalAmount);
    }

    /// <summary>
    /// A narration containing the delimiter, quoted. Left unhandled this is the
    /// single most common way a statement import shifts every column right and
    /// posts nonsense.
    /// </summary>
    [Fact]
    public void A_description_containing_a_comma_does_not_shift_the_columns()
    {
        StatementGrid grid = Csv(
            """
            Date,Narration,Chq/Ref No,Withdrawal Amt.,Deposit Amt.,Closing Balance
            01/04/2026,"SHARMA, TRADERS & SONS, MUMBAI",N001,1000.00,,5000.00
            """);

        BankStatementLine line = Assert.Single(StatementRowMapper.Map(grid, TwoColumn()));

        Assert.Equal("SHARMA, TRADERS & SONS, MUMBAI", line.Description);
        Assert.Equal(1_000m, line.WithdrawalAmount);
        Assert.Equal(5_000m, line.RunningBalance);
    }

    /// <summary>
    /// The branch address and account summary banks print above the table. Counted
    /// rather than detected — a heuristic that guesses where the table starts eats
    /// a real row the day a bank adds a line to its header.
    /// </summary>
    [Fact]
    public void Rows_above_the_header_are_skipped_by_count()
    {
        StatementGrid grid = Csv(
            """
            HDFC BANK LTD
            Statement of account 0123456789
            For the period 01/04/2026 to 30/04/2026

            Date,Narration,Chq/Ref No,Withdrawal Amt.,Deposit Amt.,Closing Balance
            01/04/2026,NEFT DR-SHARMA,N001,1000.00,,5000.00
            """,
            skipRows: 3);

        StatementImportProfile profile = TwoColumn();
        profile.SkipRows = 3;

        BankStatementLine line = Assert.Single(StatementRowMapper.Map(grid, profile));
        Assert.Equal(1_000m, line.WithdrawalAmount);
    }

    /// <summary>
    /// The other layout: one signed column. Negative is money out, unless the
    /// bank does it the other way round — which is a setting rather than an
    /// assumption because some genuinely do.
    /// </summary>
    [Fact]
    public void A_single_signed_amount_column_splits_into_the_two_sides()
    {
        StatementGrid grid = Csv(
            """
            Txn Date,Description,Ref,Amount
            01/04/2026,PAID TO SHARMA,N001,-11000.00
            03/04/2026,RECEIVED FROM RAMESH,U002,5000.00
            """);

        var profile = new StatementImportProfile
        {
            BankAccountId = 1,
            DateFormat = "dd/MM/yyyy",
            DateColumn = "Txn Date",
            DescriptionColumn = "Description",
            ReferenceColumn = "Ref",
            AmountColumn = "Amount",
        };

        List<BankStatementLine> lines = StatementRowMapper.Map(grid, profile);

        Assert.Equal(11_000m, lines[0].WithdrawalAmount);
        Assert.Equal(0m, lines[0].DepositAmount);
        Assert.Equal(5_000m, lines[1].DepositAmount);
        Assert.Equal(0m, lines[1].WithdrawalAmount);
    }

    /// <summary>
    /// The same file read twice produces the same hashes, so re-importing an
    /// overlapping period adds nothing. This is what makes the feature usable the
    /// way people actually work — downloading the last thirty days every week.
    /// </summary>
    [Fact]
    public void The_same_file_read_twice_produces_the_same_row_identities()
    {
        const string file =
            """
            Date,Narration,Chq/Ref No,Withdrawal Amt.,Deposit Amt.,Closing Balance
            01/04/2026,NEFT DR-SHARMA,N001,1000.00,,5000.00
            03/04/2026,UPI-RAMESH,U002,,2000.00,7000.00
            """;

        string[] first = [.. StatementRowMapper.Map(Csv(file), TwoColumn()).Select(l => l.RowHash)];
        string[] again = [.. StatementRowMapper.Map(Csv(file), TwoColumn()).Select(l => l.RowHash)];

        Assert.Equal(first, again);

        // And re-padding the narration, which banks do between exports of the
        // same period, does not change identity either.
        string respaced = file.Replace("NEFT DR-SHARMA", "NEFT  DR-SHARMA ");

        Assert.Equal(
            first[0], StatementRowMapper.Map(Csv(respaced), TwoColumn())[0].RowHash);
    }

    /// <summary>
    /// Two genuinely identical movements on one day are two movements. A hash over
    /// the bank's fields alone would call the second a duplicate and silently drop
    /// it — the account would then be short by exactly one withdrawal, and the
    /// only sign would be a reconciliation that never quite finishes.
    /// </summary>
    [Fact]
    public void Two_identical_movements_on_one_day_are_kept_apart()
    {
        StatementGrid grid = Csv(
            """
            Date,Narration,Chq/Ref No,Withdrawal Amt.,Deposit Amt.,Closing Balance
            01/04/2026,ATM CASH,,500.00,,9500.00
            01/04/2026,ATM CASH,,500.00,,9000.00
            """);

        List<BankStatementLine> lines = StatementRowMapper.Map(grid, TwoColumn());

        Assert.Equal(2, lines.Count);
        Assert.NotEqual(lines[0].RowHash, lines[1].RowHash);
    }

    /// <summary>
    /// A trailing total row has no date, and is the bank's footer rather than a
    /// movement. Skipped rather than refused — refusing would make every real
    /// statement fail on its last line.
    /// </summary>
    [Fact]
    public void A_footer_row_with_no_date_is_not_a_movement()
    {
        StatementGrid grid = Csv(
            """
            Date,Narration,Chq/Ref No,Withdrawal Amt.,Deposit Amt.,Closing Balance
            01/04/2026,NEFT DR-SHARMA,N001,1000.00,,5000.00
            ,TOTAL,,1000.00,0.00,
            """);

        Assert.Single(StatementRowMapper.Map(grid, TwoColumn()));
    }

    /// <summary>
    /// An unreadable date names the row and the format, because the person
    /// reading the message is looking at the file. "Import failed" would send them
    /// hunting through three hundred rows.
    /// </summary>
    [Fact]
    public void An_unreadable_date_says_which_row_and_which_format()
    {
        StatementGrid grid = Csv(
            """
            Date,Narration,Chq/Ref No,Withdrawal Amt.,Deposit Amt.,Closing Balance
            01/04/2026,GOOD ROW,N001,1000.00,,5000.00
            2026-04-31,BAD ROW,N002,1000.00,,4000.00
            """);

        StatementReadException error = Assert.Throws<StatementReadException>(
            () => StatementRowMapper.Map(grid, TwoColumn()));

        // Row 3: one skipped row would be zero, the header is row 1, the good row
        // is row 2 — which is the number the user sees in their spreadsheet.
        Assert.Contains("Row 3", error.Message);
        Assert.Contains("dd/MM/yyyy", error.Message);
    }

    /// <summary>
    /// Tab-separated, which "CSV" from a bank portal frequently is. Detected on
    /// the first line rather than assumed, and counted outside quotes so a comma
    /// inside a narration cannot win.
    /// </summary>
    [Fact]
    public void A_tab_separated_file_is_detected_rather_than_assumed()
    {
        StatementGrid grid = Csv(
            "Date\tNarration\tChq/Ref No\tWithdrawal Amt.\tDeposit Amt.\tClosing Balance\n"
            + "01/04/2026\tNEFT DR-SHARMA, MUMBAI\tN001\t1000.00\t\t5000.00");

        BankStatementLine line = Assert.Single(StatementRowMapper.Map(grid, TwoColumn()));

        Assert.Equal("NEFT DR-SHARMA, MUMBAI", line.Description);
        Assert.Equal(1_000m, line.WithdrawalAmount);
    }

    /// <summary>
    /// A row with an amount in both columns is a mapping pointing at one column
    /// twice, or a file whose columns are not what the profile says. Refused,
    /// because the amounts would otherwise be wrong in a way nothing downstream
    /// could detect.
    /// </summary>
    [Fact]
    public void A_row_with_both_amounts_is_refused()
    {
        StatementGrid grid = Csv(
            """
            Date,Narration,Chq/Ref No,Withdrawal Amt.,Deposit Amt.,Closing Balance
            01/04/2026,BOTH,N001,1000.00,500.00,5000.00
            """);

        Assert.Throws<StatementReadException>(() => StatementRowMapper.Map(grid, TwoColumn()));
    }

    /// <summary>
    /// The export goes out with the reconciliation beside the bank's own figures,
    /// and a narration that looks like a formula is neutralized.
    ///
    /// <b>That last part is a security fix.</b> A bank narration is
    /// attacker-influenced — anyone who can send the organization money chooses
    /// what appears in it — and a spreadsheet opening the export would run
    /// <c>=cmd|…</c> as a formula.
    /// </summary>
    [Fact]
    public void The_csv_export_neutralizes_a_narration_that_looks_like_a_formula()
    {
        var export = new StatementExport(
            "HDFC-Current",
            ["Date", "Description", "Withdrawal"],
            [["2026-04-01", "=cmd|'/c calc'!A1", "1000.00"]]);

        string csv = Encoding.UTF8.GetString(StatementExportWriter.ToCsv(export));

        Assert.Contains("'=cmd", csv);
        Assert.DoesNotContain("\n=cmd", csv);

        // And it opens as UTF-8 in Excel rather than as the system code page,
        // which is what keeps a Tamil or Chinese narration readable.
        Assert.Equal(Encoding.UTF8.GetPreamble(), StatementExportWriter.ToCsv(export)[..3]);
    }

    /// <summary>The Excel export is a real workbook, and reads back as what went in.</summary>
    [Fact]
    public void The_excel_export_reads_back_as_the_rows_that_went_in()
    {
        var export = new StatementExport(
            "HDFC-Current",
            ["Date", "Description", "Withdrawal"],
            [["2026-04-01", "NEFT DR-SHARMA", "1000.00"]]);

        using var stream = new MemoryStream(StatementExportWriter.ToExcel(export));

        StatementGrid grid = ExcelStatementReader.Read(stream, skipRows: 0, hasHeaderRow: true);

        Assert.Equal(["Date", "Description", "Withdrawal"], grid.Header);
        Assert.Equal(["2026-04-01", "NEFT DR-SHARMA", "1000.00"], Assert.Single(grid.Rows));
    }

    private static StatementGrid Csv(string text, int skipRows = 0)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        return CsvStatementReader.Read(stream, skipRows, hasHeaderRow: true);
    }

    /// <summary>The HDFC-shaped layout these tests mostly use.</summary>
    private static StatementImportProfile TwoColumn() => new()
    {
        BankAccountId = 1,
        DateFormat = "dd/MM/yyyy",
        DateColumn = "Date",
        DescriptionColumn = "Narration",
        ReferenceColumn = "Chq/Ref No",
        WithdrawalColumn = "Withdrawal Amt.",
        DepositColumn = "Deposit Amt.",
        BalanceColumn = "Closing Balance",
    };
}
