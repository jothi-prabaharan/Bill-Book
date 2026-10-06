using System.IO.Compression;
using System.Text;
using Accounting.Api.Services;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Accounting.Repository.Repositories;
using Accounting.Repository.SeedData;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Contacts;
using Shared.Kernel.Tax;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// TDS (TK-117). The card's test: the aggregate threshold deducts on the bill
/// that crosses it and not before; a certificate's rate applies within its
/// limit; the vendor is owed the net. And its "done when": a bill that takes a
/// vendor past the year's threshold posts TDS Payable and a net payable.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class TdsTests
{
    private const string IndividualPan = "AAAPA1234A";
    private const string CompanyPan = "AAACB1234C";
    private static readonly DateOnly June = new(2026, 6, 15);

    private readonly PostgresFixture _postgres;

    public TdsTests(PostgresFixture postgres) => _postgres = postgres;

    private static TdsSection Section(string code) =>
        TdsSectionSeed.Build(Guid.NewGuid()).Single(s => s.SectionCode == code);

    private static TdsHistoryRow Earlier(decimal amount, decimal tdsBase = 0m, TdsEntryKind kind = TdsEntryKind.Credit) =>
        new(kind, amount, 0m, tdsBase, null, 0m);

    // ---- The calculator ----------------------------------------------------

    [Fact]
    public void The_aggregate_threshold_deducts_on_the_bill_that_crosses_it_and_not_before()
    {
        TdsSection contract = Section("194C");
        TdsDeductee vendor = new(IndividualPan, null);
        List<TdsHistoryRow> history = [];

        // Three bills of 25,000: each under the 30,000 single threshold, and
        // the year stays at or under the 1,00,000 aggregate.
        foreach (int _ in Enumerable.Range(0, 3))
        {
            TdsComputation below = TdsCalculationRepository.Compute(contract, vendor, history, TdsEntryKind.Credit, 25_000m, June);
            Assert.Equal(0m, below.TdsAmount);
            Assert.Equal(0m, below.TdsBase);
            history.Add(Earlier(25_000m));
        }

        // The fourth takes the year to 1,05,000: everything is taxed now, at 1%.
        TdsComputation crossing = TdsCalculationRepository.Compute(contract, vendor, history, TdsEntryKind.Credit, 30_000m, June);

        Assert.Equal(105_000m, crossing.TdsBase);
        Assert.Equal(1m, crossing.Rate);
        Assert.Equal(1_050m, crossing.TdsAmount);
        Assert.Contains("past the 100,000.00 threshold", crossing.Note);

        // And the next is taxed on itself alone.
        history.Add(Earlier(30_000m, tdsBase: 105_000m));
        TdsComputation after = TdsCalculationRepository.Compute(contract, vendor, history, TdsEntryKind.Credit, 10_000m, June);
        Assert.Equal(10_000m, after.TdsBase);
        Assert.Equal(100m, after.TdsAmount);
    }

    [Fact]
    public void One_document_above_the_single_threshold_is_taxed_on_its_own()
    {
        TdsComputation single = TdsCalculationRepository.Compute(
            Section("194C"), new TdsDeductee(CompanyPan, null), [], TdsEntryKind.Credit, 40_000m, June);

        Assert.Equal(40_000m, single.TdsBase);
        Assert.Equal(2m, single.Rate);
        Assert.Equal(800m, single.TdsAmount);
    }

    [Fact]
    public void A_certificates_rate_applies_within_its_limit_and_the_section_rate_past_it()
    {
        TdsCertificate certificate = new("LDC-001", 2m, new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31), 60_000m);

        TdsComputation computed = TdsCalculationRepository.Compute(
            Section("194JB"), new TdsDeductee(CompanyPan, certificate), [], TdsEntryKind.Credit, 100_000m, June);

        // 60,000 at 2% and 40,000 at 10%.
        Assert.Equal(100_000m, computed.TdsBase);
        Assert.Equal(60_000m, computed.CertificateBase);
        Assert.Equal("LDC-001", computed.CertificateNo);
        Assert.Equal(5_200m, computed.TdsAmount);
    }

    [Fact]
    public void A_certificate_used_up_by_earlier_bills_gives_no_relief()
    {
        TdsCertificate certificate = new("LDC-001", 2m, new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31), 60_000m);
        List<TdsHistoryRow> history = [new(TdsEntryKind.Credit, 60_000m, 0m, 60_000m, "LDC-001", 60_000m)];

        TdsComputation computed = TdsCalculationRepository.Compute(
            Section("194JB"), new TdsDeductee(CompanyPan, certificate), history, TdsEntryKind.Credit, 10_000m, June);

        Assert.Equal(0m, computed.CertificateBase);
        Assert.Equal(1_000m, computed.TdsAmount);
    }

    [Fact]
    public void A_certificate_outside_its_validity_is_ignored()
    {
        TdsCertificate expired = new("LDC-001", 2m, new DateOnly(2025, 4, 1), new DateOnly(2026, 3, 31), 60_000m);

        TdsComputation computed = TdsCalculationRepository.Compute(
            Section("194JB"), new TdsDeductee(CompanyPan, expired), [], TdsEntryKind.Credit, 100_000m, June);

        Assert.Null(computed.CertificateNo);
        Assert.Equal(10_000m, computed.TdsAmount);
    }

    [Fact]
    public void No_valid_pan_takes_the_section_206AA_rate()
    {
        TdsComputation computed = TdsCalculationRepository.Compute(
            Section("194C"), new TdsDeductee(null, null), [], TdsEntryKind.Credit, 40_000m, June);

        Assert.Equal(TdsDeducteeKind.NoPan, computed.DeducteeKind);
        Assert.Equal(20m, computed.Rate);
        Assert.Equal(8_000m, computed.TdsAmount);
        Assert.Contains("206AA", computed.Note);
    }

    [Fact]
    public void Purchase_of_goods_is_taxed_only_on_the_excess_over_its_threshold()
    {
        TdsComputation computed = TdsCalculationRepository.Compute(
            Section("194Q"), new TdsDeductee(CompanyPan, null), [Earlier(4_900_000m)], TdsEntryKind.Credit, 200_000m, June);

        Assert.Equal(100_000m, computed.TdsBase);
        Assert.Equal(100m, computed.TdsAmount);
    }

    [Fact]
    public void A_bill_after_an_advance_that_bore_tds_is_not_taxed_twice()
    {
        List<TdsHistoryRow> history = [new(TdsEntryKind.Advance, 50_000m, 0m, 50_000m, null, 0m)];

        TdsComputation computed = TdsCalculationRepository.Compute(
            Section("194C"), new TdsDeductee(CompanyPan, null), history, TdsEntryKind.Credit, 50_000m, June);

        Assert.Equal(50_000m, computed.CoveredByAdvance);
        Assert.Equal(0m, computed.TdsAmount);
    }

    [Theory]
    [InlineData("AAAPA1234A", TdsDeducteeKind.IndividualOrHuf)]
    [InlineData("AAAHA1234A", TdsDeducteeKind.IndividualOrHuf)]
    [InlineData("AAACB1234C", TdsDeducteeKind.Other)]
    [InlineData("AAAFB1234C", TdsDeducteeKind.Other)]
    [InlineData("NOTAPAN", TdsDeducteeKind.NoPan)]
    [InlineData(null, TdsDeducteeKind.NoPan)]
    public void The_deductee_kind_is_read_off_the_pan(string? pan, TdsDeducteeKind expected) =>
        Assert.Equal(expected, TdsCalculationRepository.KindOf(pan));

    [Theory]
    [InlineData(2026, 4, 1, 2026)]
    [InlineData(2027, 3, 31, 2026)]
    [InlineData(2027, 1, 15, 2026)]
    public void The_financial_year_runs_april_to_march(int year, int month, int day, int expected) =>
        Assert.Equal(expected, TdsCalculationRepository.FinancialYearOf(new DateOnly(year, month, day)));

    [Fact]
    public void Every_branch_is_seeded_with_tds_payable_and_receivable_off_the_journal_picker()
    {
        IReadOnlyList<Account> chart = ChartOfAccountsSeed.Build(Guid.NewGuid());

        Account payable = Assert.Single(chart, a => a.AccountSystemName == "TDS Payable");
        Account receivable = Assert.Single(chart, a => a.AccountSystemName == "TDS Receivable");

        Assert.Equal(2, payable.AccountTypeId);
        Assert.Equal(1, receivable.AccountTypeId);
        Assert.False(payable.IsJE);
        Assert.False(receivable.IsJE);
    }

    // ---- Withholding on a money document ------------------------------------

    [Fact]
    public void Withheld_tds_comes_off_the_bank_legs_of_the_lines_that_bear_it()
    {
        IReadOnlyDictionary<int, decimal> shares = TdsWithholdingRepository.BankShares(
            [(1, 5_000m, false), (2, 800m, true), (3, 10_000m, true)], 1_000m);

        Assert.Equal(5_000m, shares[1]);
        Assert.Equal(0m, shares[2]);
        Assert.Equal(9_800m, shares[3]);
    }

    [Fact]
    public void More_withheld_than_the_bearing_lines_carry_is_refused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TdsWithholdingRepository.BankShares([(1, 500m, true), (2, 5_000m, false)], 600m));

    [Theory]
    [InlineData(2026, 5, 2026, 6, 7)]
    [InlineData(2027, 3, 2027, 4, 30)]
    public void A_months_deductions_fall_due_on_the_seventh_and_march_on_the_thirtieth_of_april(
        int year, int month, int dueYear, int dueMonth, int dueDay) =>
        Assert.Equal(new DateOnly(dueYear, dueMonth, dueDay), TdsRepository.DueDate(new DateOnly(year, month, 1)));

    // ---- 26Q ----------------------------------------------------------------

    [Fact]
    public void The_26q_export_lists_challans_and_deductees_and_warns_about_undeposited_deductions()
    {
        Form26qExport export = Form26qExportRepository.Build(
            [new Form26qChallan(7, "194C", 1_050m, "0510308", "00123", new DateOnly(2026, 7, 5))],
            [
                new Form26qDeduction(7, "194C", IndividualPan, "Ravi Transport", June, 105_000m, 1_050m, 1m, null, TdsDeducteeKind.IndividualOrHuf),
                new Form26qDeduction(null, "194JB", null, "Acme, Consultants", June, 60_000m, 12_000m, 20m, null, TdsDeducteeKind.NoPan),
            ]);

        Assert.Equal(1, export.Challans);
        Assert.Equal(2, export.Deductions);
        Assert.Contains(export.Warnings, w => w.Contains("Acme, Consultants") && w.Contains("not been deposited"));

        using ZipArchive zip = new(new MemoryStream(export.Zip));
        string challans = Read(zip, "challans.csv");
        string deductees = Read(zip, "deductees.csv");

        Assert.Contains("1,194C,1050.00,", challans);
        Assert.Contains("0510308,00123,05-07-2026,200", challans);

        // The individual is code 02 against challan 1; no PAN is PANNOTAVBL, reason C, and the name is quoted.
        Assert.Contains("2,1,194C,02,AAAPA1234A,Ravi Transport,15-06-2026,105000.00,1050.00", deductees);
        Assert.Contains("PANNOTAVBL,\"Acme, Consultants\"", deductees);
        Assert.Contains(",20,C,", deductees);
    }

    [Theory]
    [InlineData(1, 2026, 4, 1, 2026, 6, 30)]
    [InlineData(4, 2027, 1, 1, 2027, 3, 31)]
    public void A_quarter_is_three_months_of_the_financial_year(int quarter, int fy, int fm, int fd, int ty, int tm, int td)
    {
        (DateOnly from, DateOnly to) = Form26qExportRepository.QuarterOf(2026, quarter);
        Assert.Equal(new DateOnly(fy, fm, fd), from);
        Assert.Equal(new DateOnly(ty, tm, td), to);
    }

    private static string Read(ZipArchive zip, string name)
    {
        using StreamReader reader = new(zip.GetEntry(name)!.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    // ---- 26AS ---------------------------------------------------------------

    /// <summary>A Form 26AS text download, trimmed to two deductors in Part I and a Part II that must be skipped.</summary>
    private const string Statement = """
        File Creation Date^14-07-2026
        Permanent Account Number (PAN)^Current Status of PAN^Financial Year^Assessment Year^Name of Assessee
        AAACB1234C^Active^2026-27^2027-28^BRIGHT TRADERS PRIVATE LIMITED
        PART-I - Details of Tax Deducted at Source^
        Sr. No.^Name of Deductor^TAN of Deductor^^^^Total Amount Paid / Credited(Rs.)^Total Tax Deducted #(Rs.)^Total TDS Deposited(Rs.)
        1^GLOBAL RETAIL LTD^MUMG12345B^^^^150000.00^3000.00^3000.00
        ^Sr. No.^Section 1^Transaction Date^Status of Booking*^Date of Booking^Remarks**^Amount Paid / Credited(Rs.)^Tax Deducted #(Rs.)^TDS Deposited(Rs.)
        ^1^194C^15-Jun-2026^F^31-Jul-2026^^100000.00^2000.00^2000.00
        ^2^194C^20-Jun-2026^U^^^50000.00^1000.00^1000.00
        2^NORTH MART^DELN54321C^^^^40000.00^800.00^800.00
        ^Sr. No.^Section 1^Transaction Date^Status of Booking*^Date of Booking^Remarks**^Amount Paid / Credited(Rs.)^Tax Deducted #(Rs.)^TDS Deposited(Rs.)
        ^1^194C^02-Jul-2026^F^31-Jul-2026^^40000.00^800.00^800.00
        PART-II - Details of Tax Deducted at Source for 15G / 15H^
        1^SOMEONE ELSE^CHNS11111A^^^^5000.00^500.00^500.00
        ^1^194A^01-Jul-2026^F^^^5000.00^500.00^500.00
        """;

    [Fact]
    public void A_26as_text_file_is_read_for_its_pan_year_and_part_one_transactions()
    {
        Form26asStatement parsed = Form26asFileRepository.Parse(Statement);

        Assert.Equal("AAACB1234C", parsed.Pan);
        Assert.Equal(2026, parsed.FinancialYear);
        Assert.Equal(3, parsed.Transactions.Count);
        Assert.DoesNotContain(parsed.Transactions, t => t.DeductorTan == "CHNS11111A");

        Form26asTransaction first = parsed.Transactions[0];
        Assert.Equal("MUMG12345B", first.DeductorTan);
        Assert.Equal("GLOBAL RETAIL LTD", first.DeductorName);
        Assert.Equal(new DateOnly(2026, 6, 15), first.TransactionDate);
        Assert.Equal(100_000m, first.AmountPaid);
        Assert.Equal(2_000m, first.Tds);
    }

    [Theory]
    [InlineData("just some text")]
    [InlineData("PAN^Year\nAAACB1234C^no year here")]
    public void A_file_that_is_not_a_26as_download_is_refused_with_a_reason(string text) =>
        Assert.Throws<Form26asFormatException>(() => Form26asFileRepository.Parse(text));

    [Fact]
    public void Matching_26as_against_receipts_finds_matches_differences_and_both_kinds_of_missing()
    {
        Form26asStatement parsed = Form26asFileRepository.Parse(Statement);
        List<BooksTds> books =
        [
            new(11, "RCM-11", "MUMG12345B", new DateOnly(2026, 6, 16), 2_000m, "194C"),
            new(12, "RCM-12", "MUMG12345B", new DateOnly(2026, 6, 21), 1_200m, "194C"),
            new(13, "RCM-13", "BLRX99999Z", new DateOnly(2026, 7, 1), 500m, "194C"),
        ];

        Form26asMatchResult result = Form26asMatcherRepository.Match(parsed.Transactions, books);

        Form26asLine matched = Assert.Single(result.Lines, l => l.MatchState == Form26asMatchState.Matched);
        Assert.Equal(11, matched.ReceiveMoneyId);

        Form26asLine differs = Assert.Single(result.Lines, l => l.MatchState == Form26asMatchState.AmountDiffers);
        Assert.Equal(12, differs.ReceiveMoneyId);
        Assert.Contains("differs by 200.00", differs.Note);
        Assert.Contains("Booking status U", differs.Note);

        Assert.Equal("DELN54321C", Assert.Single(result.Lines, l => l.MatchState == Form26asMatchState.MissingInBooks).DeductorTan);
        Assert.Equal(13, Assert.Single(result.Lines, l => l.MatchState == Form26asMatchState.MissingIn26as).ReceiveMoneyId);

        // At risk: the 500 26AS does not show and the 200 claimed above it.
        Assert.Equal(700m, result.CreditAtRisk);
    }

    // ---- Through the ledger ---------------------------------------------------

    [SkippableFact]
    public async Task A_bill_that_takes_a_vendor_past_the_years_threshold_posts_tds_payable_and_a_net_payable()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        await using AccountingDbContext db = _postgres.CreateContext(customerId, orgId);
        CancellationToken ct = CancellationToken.None;

        db.TdsSections.AddRange(TdsSectionSeed.Build(orgId));
        long inventory = await AccountAsync(db, orgId, "1200", "Inventory", 1);
        long payable = await AccountAsync(db, orgId, "2100", "Accounts Payable", 2);
        long tdsPayable = await AccountAsync(db, orgId, "2220", "TDS Payable", 2);

        StubProfiles profiles = new(new TdsProfile { ContactId = 42, IsTdsApplicable = true, TdsSection = "194C", Pan = IndividualPan });
        TdsRepository tds = new(db, profiles);
        LedgerPostingRepository postings = new(db, new TenantContext { CustomerId = customerId, OrgId = orgId }, new StubBaseCurrency(), tds);

        async Task<TdsQuote> PostBillAsync(long billId, decimal value)
        {
            TdsQuoteResult quoted = await tds.QuoteAsync(new TdsQuoteRequest
            {
                ContactId = 42, TransactionTypeCode = "BIL", TransactionId = billId, Date = June, Amount = value, Kind = 1,
            }, ct);
            TdsQuote quote = quoted.Quote!;

            List<LedgerLegRequest> legs =
            [
                new() { LedgerTypeId = 1, LedgerSourceId = 1, TransactionDetailId = billId * 10, AccountId = inventory, DebitAmount = value },
                new() { LedgerTypeId = 3, LedgerSourceId = 1, AccountId = payable, CreditAmount = value - quote.TdsAmount },
            ];
            if (quote.TdsAmount > 0)
            {
                legs.Add(new() { LedgerTypeId = 2, LedgerSourceId = 1, AccountId = tdsPayable, CreditAmount = quote.TdsAmount });
            }

            PostLedgerResult posted = await postings.PostAsync(new PostLedgerRequest
            {
                TransactionTypeCode = "BIL", TransactionId = billId, LedgerDate = June, ContactId = 42,
                DocumentNo = $"BIL-{billId}", Legs = legs, Tds = quote,
            }, ct);
            Assert.Equal(PostLedgerOutcome.Ok, posted.Outcome);
            return quote;
        }

        Assert.Equal(0m, (await PostBillAsync(1, 25_000m)).TdsAmount);
        Assert.Equal(0m, (await PostBillAsync(2, 25_000m)).TdsAmount);
        Assert.Equal(0m, (await PostBillAsync(3, 25_000m)).TdsAmount);
        TdsQuote crossing = await PostBillAsync(4, 30_000m);

        Assert.Equal(1_050m, crossing.TdsAmount);
        Assert.Equal(-1_050m, await BalanceAsync(db, tdsPayable));

        // The vendor is owed the net on the crossing bill: 30,000 less 1,050.
        decimal owedOnCrossing = await db.JournalLedger
            .Where(l => l.TransactionTypeCode == "BIL" && l.TransactionId == 4 && l.AccountId == payable)
            .SumAsync(l => l.CreditAmount - l.DebitAmount, ct);
        Assert.Equal(28_950m, owedOnCrossing);

        // Every bill is in the register, deducted or not, which is what the aggregate reads.
        Assert.Equal(4, await db.TdsEntries.CountAsync(ct));
        Assert.Equal(1_050m, await db.TdsEntries.SumAsync(e => e.TdsAmount, ct));
    }

    [SkippableFact]
    public async Task A_withdrawal_removes_the_register_row_and_is_refused_once_the_tds_is_deposited()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        await using AccountingDbContext db = _postgres.CreateContext(customerId, orgId);
        CancellationToken ct = CancellationToken.None;

        long inventory = await AccountAsync(db, orgId, "1200", "Inventory", 1);
        long payable = await AccountAsync(db, orgId, "2100", "Accounts Payable", 2);
        long tdsPayable = await AccountAsync(db, orgId, "2220", "TDS Payable", 2);

        TdsRepository tds = new(db, new StubProfiles(null));
        LedgerPostingRepository postings = new(db, new TenantContext { CustomerId = customerId, OrgId = orgId }, new StubBaseCurrency(), tds);

        TdsQuote quote = new()
        {
            Applies = true, Kind = 1, SectionCode = "194C", Pan = CompanyPan, DeducteeKind = 2, FinancialYear = 2026,
            Amount = 40_000m, TdsBase = 40_000m, Rate = 2m, TdsAmount = 800m, Note = "Above the single threshold.",
        };

        PostLedgerRequest Bill(long id) => new()
        {
            TransactionTypeCode = "BIL", TransactionId = id, LedgerDate = June, ContactId = 7, Tds = quote,
            Legs =
            [
                new() { LedgerTypeId = 1, LedgerSourceId = 1, TransactionDetailId = 1, AccountId = inventory, DebitAmount = 40_000m },
                new() { LedgerTypeId = 3, LedgerSourceId = 1, AccountId = payable, CreditAmount = 39_200m },
                new() { LedgerTypeId = 2, LedgerSourceId = 1, AccountId = tdsPayable, CreditAmount = 800m },
            ],
        };

        PostLedgerRequest Withdrawal(long id) => new()
        {
            TransactionTypeCode = "BIL", TransactionId = id, LedgerDate = June, ContactId = 7,
            WithdrawLedgerTypeIds = [1, 2, 3, 6], Legs = [],
        };

        Assert.Equal(PostLedgerOutcome.Ok, (await postings.PostAsync(Bill(1), ct)).Outcome);
        Assert.Equal(PostLedgerOutcome.Ok, (await postings.PostAsync(Withdrawal(1), ct)).Outcome);
        Assert.False(await db.TdsEntries.AnyAsync(e => e.TransactionId == 1, ct));

        // Posted again, then paid over on a challan: the bill is frozen.
        Assert.Equal(PostLedgerOutcome.Ok, (await postings.PostAsync(Bill(2), ct)).Outcome);
        SpendMoney challan = await ChallanAsync(db, orgId, 800m);
        Assert.Equal(1, await tds.MarkDepositedAsync(challan.SpendMoneyId, "194c", June, ct));

        PostLedgerResult refused = await postings.PostAsync(Withdrawal(2), ct);
        Assert.Equal(PostLedgerOutcome.TdsDeposited, refused.Outcome);
        Assert.True(await db.TdsEntries.AnyAsync(e => e.TransactionId == 2, ct));
    }

    [SkippableFact]
    public async Task The_outstanding_list_and_a_challans_figure_come_from_the_undeposited_deductions()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid orgId = Guid.NewGuid();
        await using AccountingDbContext db = _postgres.CreateContext(Guid.NewGuid(), orgId);
        CancellationToken ct = CancellationToken.None;
        TdsRepository tds = new(db, new StubProfiles(null));

        foreach ((long id, DateOnly date, decimal amount) in new[]
        {
            (1L, new DateOnly(2026, 6, 3), 500m),
            (2L, new DateOnly(2026, 6, 28), 300m),
            (3L, new DateOnly(2026, 7, 1), 200m),
        })
        {
            await tds.WriteDeductionAsync("BIL", id, $"BIL-{id}", date, 9, new TdsQuote
            {
                Applies = true, Kind = 1, SectionCode = "194JB", Amount = amount * 10, TdsBase = amount * 10, Rate = 10m, TdsAmount = amount,
            }, ct);
        }

        List<TdsOutstandingView> outstanding = await tds.OutstandingAsync(ct);

        TdsOutstandingView june = Assert.Single(outstanding, o => o.Month == new DateOnly(2026, 6, 1));
        Assert.Equal(800m, june.TdsAmount);
        Assert.Equal(2, june.Deductions);
        Assert.Equal(new DateOnly(2026, 7, 7), june.DueDate);
        Assert.Equal(800m, await tds.OutstandingForAsync("194JB", new DateOnly(2026, 6, 20), ct));
    }

    private static async Task<long> AccountAsync(AccountingDbContext db, Guid orgId, string code, string name, int typeId)
    {
        Account account = new()
        {
            OrgId = orgId,
            AccountTypeId = typeId,
            AccountCode = code,
            AccountName = name,
            AccountSystemName = name,
            IsActive = true,
        };

        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return account.AccountId;
    }

    /// <summary>A draft challan, enough for a register row to point at.</summary>
    private static async Task<SpendMoney> ChallanAsync(AccountingDbContext db, Guid orgId, decimal amount)
    {
        BankAccount account = new()
        {
            OrgId = orgId,
            AccountName = "Current",
            AccountNumber = "0001",
            AccountType = BankAccountType.Cash,
            CurrencyCode = "INR",
            IsActive = true,
        };
        db.BankAccounts.Add(account);
        await db.SaveChangesAsync();

        SpendMoney challan = new()
        {
            OrgId = orgId,
            TransactionDate = new DateOnly(2026, 7, 5),
            BankAccountId = account.BankAccountId,
            ContactId = 1,
            Amount = amount,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            IsTdsChallan = true,
            ChallanNo = "00123",
            BsrCode = "0510308",
            ChallanSectionCode = "194C",
            ChallanMonth = new DateOnly(2026, 6, 1),
        };
        db.SpendMoney.Add(challan);
        await db.SaveChangesAsync();
        return challan;
    }

    private static Task<decimal> BalanceAsync(AccountingDbContext db, long accountId) =>
        db.JournalLedger.Where(l => l.AccountId == accountId).SumAsync(l => l.DebitAmount - l.CreditAmount);

    private sealed class StubProfiles(TdsProfile? profile) : ITdsProfileClient
    {
        public Task<TdsProfile?> ProfileAsync(long contactId, CancellationToken ct) => Task.FromResult(profile);
    }
}
