using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Reporting.Api.Controllers;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The Journal Report's own page (TK-217), on October 2026's books worked by hand:
/// <list type="bullet">
/// <item>Journal JV-0011 on 3 October, a manual journal of 2,500 rent accrued to a
/// payable owed to contact 8, later <b>reversed</b> by JV-0012 on 10 October.</item>
/// <item>Sales invoice INV/2627/0002 on 5 October: 1,180 receivable from contact 7,
/// 1,000 sales and 180 output GST.</item>
/// <item>Spend money 5 on 7 October, 600 of rent with no document number.</item>
/// <item>A USD invoice on 15 October: 10 dollars at 83.5, so 835 in base.</item>
/// <item>A <b>draft</b> journal on 20 October of 100, which has no ledger rows.</item>
/// <item>Postings on 30 September and 1 November, outside the period.</item>
/// </list>
/// The builders run on lists through the same expressions the repository runs against
/// the read models; the composition is pure. Nothing here needs PostgreSQL.
/// </summary>
public sealed class JournalReportTests
{
    private const long Bank = 1001, Receivable = 1100, Payable = 2001, OutputGst = 2200, Sales = 4001, Rent = 5001;
    private const long Customer = 7, Landlord = 8;
    private static readonly Guid Asha = Guid.Parse("00000000-0000-0000-0000-00000000a001");
    private static readonly Guid Ravi = Guid.Parse("00000000-0000-0000-0000-00000000a002");
    private static readonly Guid Meena = Guid.Parse("00000000-0000-0000-0000-00000000a003");

    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October31 = new(2026, 10, 31);

    private static readonly IReadOnlyDictionary<Guid, string> Names =
        new Dictionary<Guid, string> { [Asha] = "Asha", [Ravi] = "Ravi", [Meena] = "Meena" };

    private static AccountRead Account(long id, string name) =>
        new() { AccountId = id, AccountCode = id.ToString(), AccountName = name, IsActive = true };

    private static readonly List<AccountRead> Accounts =
    [
        Account(Bank, "Bank"), Account(Receivable, "Accounts Receivable"), Account(Payable, "Accounts Payable"),
        Account(OutputGst, "Output GST"), Account(Sales, "Sales"), Account(Rent, "Rent"),
    ];

    private static readonly List<ContactRead> Contacts =
    [
        new() { ContactId = Customer, ContactCode = "C-7", DisplayName = "Kaveri Constructions" },
        new() { ContactId = Landlord, ContactCode = "V-8", DisplayName = "Mylapore Properties" },
    ];

    private static long _id;

    private static JournalLedgerRead Leg(
        string type, long transactionId, DateOnly on, int line, long account, decimal debit, decimal credit,
        string? documentNo = null, long? contact = null, long? journal = null, Guid? user = null, string currency = "INR",
        decimal rate = 1m, decimal? sourceDebit = null, decimal? sourceCredit = null, string? desc = null) =>
        new()
        {
            LedgerId = ++_id, LedgerDate = on, AccountId = account, TransactionTypeCode = type, TransactionId = transactionId,
            TransactionDetailId = line, DebitAmountBase = debit, CreditAmountBase = credit,
            DebitAmount = sourceDebit ?? debit, CreditAmount = sourceCredit ?? credit,
            CurrencyCode = currency, ExchangeRate = rate, DocumentNo = documentNo, ContactId = contact, JournalId = journal,
            TransactionDesc = desc, CreatedBy = user, CreatedAt = user is null ? null : new DateTimeOffset(on.Year, on.Month, on.Day, 11, 0, 0, TimeSpan.Zero),
        };

    private static readonly List<JournalLedgerRead> Ledger =
    [
        // JV-0011, 3 October, a manual journal that was reversed by JV-0012.
        Leg("JRN", 11, new(2026, 10, 3), 1, Rent, 2_500m, 0m, journal: 11, desc: "Rent accrual"),
        Leg("JRN", 11, new(2026, 10, 3), 2, Payable, 0m, 2_500m, contact: Landlord, journal: 11, desc: "Rent accrual"),

        // The sales invoice, 5 October.
        Leg("INV", 77, new(2026, 10, 5), 1, Receivable, 1_180m, 0m, "INV/2627/0002", Customer, user: Meena),
        Leg("INV", 77, new(2026, 10, 5), 2, Sales, 0m, 1_000m, "INV/2627/0002", user: Meena),
        Leg("INV", 77, new(2026, 10, 5), 3, OutputGst, 0m, 180m, "INV/2627/0002", user: Meena),

        // Spend money, 7 October, with no document number.
        Leg("SPM", 5, new(2026, 10, 7), 1, Rent, 600m, 0m, user: Meena),
        Leg("SPM", 5, new(2026, 10, 7), 2, Bank, 0m, 600m, user: Meena),

        // JV-0012, 10 October, the reversal of JV-0011.
        Leg("JRN", 12, new(2026, 10, 10), 1, Payable, 2_500m, 0m, contact: Landlord, journal: 12),
        Leg("JRN", 12, new(2026, 10, 10), 2, Rent, 0m, 2_500m, journal: 12),

        // A USD invoice, 15 October: 10 dollars at 83.5.
        Leg("INV", 78, new(2026, 10, 15), 1, Receivable, 835m, 0m, "INV/2627/0003", Customer, user: Meena, currency: "USD", rate: 83.5m, sourceDebit: 10m, sourceCredit: 0m),
        Leg("INV", 78, new(2026, 10, 15), 2, Sales, 0m, 835m, "INV/2627/0003", user: Meena, currency: "USD", rate: 83.5m, sourceDebit: 0m, sourceCredit: 10m),

        // Outside the period.
        Leg("INV", 70, new(2026, 9, 30), 1, Receivable, 50m, 0m, "INV/2627/0001", Customer, user: Meena),
        Leg("INV", 70, new(2026, 9, 30), 2, Sales, 0m, 50m, "INV/2627/0001", user: Meena),
        Leg("INV", 79, new(2026, 11, 1), 1, Receivable, 70m, 0m, "INV/2627/0004", Customer, user: Meena),
        Leg("INV", 79, new(2026, 11, 1), 2, Sales, 0m, 70m, "INV/2627/0004", user: Meena),
    ];

    private static JournalRead Journal(
        long id, string? number, DateOnly on, JournalStatus status, string? reference = null, string? memo = null,
        Guid? created = null, Guid? posted = null, long? reverses = null, long? reversedBy = null) =>
        new()
        {
            JournalId = id, JournalNo = number, JournalDate = on, Status = status, Reference = reference, Memo = memo,
            CurrencyCode = "INR", ExchangeRate = 1m, TransactionTypeCode = "JRN",
            CreatedBy = created, CreatedAt = created is null ? null : new DateTimeOffset(on.Year, on.Month, on.Day, 9, 0, 0, TimeSpan.Zero),
            PostedBy = posted, PostedAt = posted is null ? null : new DateTimeOffset(on.Year, on.Month, on.Day, 10, 0, 0, TimeSpan.Zero),
            ReversesJournalId = reverses, ReversedByJournalId = reversedBy,
        };

    private static readonly List<JournalRead> Journals =
    [
        Journal(11, "JV-0011", new(2026, 10, 3), JournalStatus.Reversed, "REF-9", "Rent accrual", Asha, Ravi, reversedBy: 12),
        Journal(12, "JV-0012", new(2026, 10, 10), JournalStatus.Posted, null, "Reversal of JV-0011", Asha, Asha, reverses: 11),
        Journal(13, null, new(2026, 10, 20), JournalStatus.Draft, null, "Not yet posted", Asha),
    ];

    private static readonly List<JournalDetailRead> Details =
    [
        new() { JournalDetailId = 1, JournalId = 13, LineNumber = 1, AccountId = Rent, DebitAmount = 100m, DebitAmountBase = 100m, LineMemo = "Stationery" },
        new() { JournalDetailId = 2, JournalId = 13, LineNumber = 2, AccountId = Bank, CreditAmount = 100m, CreditAmountBase = 100m },
    ];

    /// <summary>A page of the report, through the same builders and composition the repository uses.</summary>
    private static JournalReportResultDto Run(
        string? source = null, JournalStatus? status = null, long? accountId = null, int page = 1, int size = 25,
        DateOnly? from = null, DateOnly? to = null)
    {
        DateOnly start = from ?? October1, end = to ?? October31;
        int skip = (page - 1) * size;

        if (status == JournalStatus.Draft)
        {
            IQueryable<JournalRead> drafts = JournalReportRepository.DraftJournals(Journals.AsQueryable(), Details.AsQueryable(), start, end, source, accountId);
            List<JournalRead> pageOfDrafts = [.. drafts.OrderBy(j => j.JournalDate).ThenBy(j => j.JournalId).Skip(skip).Take(size)];
            List<long> draftIds = [.. pageOfDrafts.Select(j => j.JournalId)];
            IQueryable<JournalDetailRead> amounts = JournalReportRepository.DraftAmounts(drafts, Details.AsQueryable());

            return new JournalReportResultDto
            {
                FromDate = start, ToDate = end, PageNumber = page, PageSize = size,
                TotalJournals = drafts.Count(), TotalDebit = amounts.Sum(d => d.DebitAmountBase), TotalCredit = amounts.Sum(d => d.CreditAmountBase),
                Journals = JournalReportRepository.Compose(
                    [.. pageOfDrafts.Select(j => new JournalKey { SourceCode = "JRN", TransactionId = j.JournalId, Date = j.JournalDate, FirstLedgerId = j.JournalId })],
                    [.. JournalReportRepository.DraftLines(Journals.AsQueryable(), Details.AsQueryable(), Accounts.AsQueryable(), draftIds)],
                    [.. pageOfDrafts.Select(JournalReportRepository.HeaderOf)],
                    new Dictionary<long, string?>(), Names),
            };
        }

        IQueryable<JournalLedgerRead> legs = JournalReportRepository.Legs(Ledger.AsQueryable(), Journals.AsQueryable(), start, end, source, status, accountId);
        List<JournalKey> keys = [.. JournalReportRepository.Keys(legs).OrderBy(k => k.Date).ThenBy(k => k.FirstLedgerId).Skip(skip).Take(size)];
        List<string> codes = [.. keys.Select(k => k.SourceCode).Distinct()];
        List<long> ids = [.. keys.Select(k => k.TransactionId).Distinct()];
        List<long> journalIds = [.. keys.Where(k => k.SourceCode == JournalReportRepository.ManualJournal).Select(k => k.TransactionId)];
        List<JournalHeader> headers = [.. JournalReportRepository.Headers(Journals.AsQueryable(), journalIds)];
        List<long> related = [.. headers.SelectMany(h => new[] { h.ReversesJournalId, h.ReversedByJournalId }).Where(i => i is not null).Select(i => i!.Value)];

        return new JournalReportResultDto
        {
            FromDate = start, ToDate = end, PageNumber = page, PageSize = size,
            TotalJournals = JournalReportRepository.Keys(legs).Count(),
            TotalDebit = legs.Sum(l => l.DebitAmountBase), TotalCredit = legs.Sum(l => l.CreditAmountBase),
            Journals = JournalReportRepository.Compose(
                keys,
                [.. JournalReportRepository.Lines(legs, Accounts.AsQueryable(), Contacts.AsQueryable(), codes, ids)],
                headers,
                Journals.Where(j => related.Contains(j.JournalId)).ToDictionary(j => j.JournalId, j => j.JournalNo),
                Names),
        };
    }

    private static JournalReportEntryDto Entry(JournalReportResultDto report, string code, long id) =>
        report.Journals.Single(j => j.SourceCode == code && j.TransactionId == id);

    // ---- The journals and their order -----------------------------------------------

    [Fact]
    public void Journals_are_the_documents_posted_in_the_period_in_date_order()
    {
        JournalReportResultDto report = Run();

        Assert.Equal(5, report.TotalJournals);
        Assert.Equal(
            [("JRN", 11L), ("INV", 77L), ("SPM", 5L), ("JRN", 12L), ("INV", 78L)],
            report.Journals.Select(j => (j.SourceCode, j.TransactionId)));
    }

    [Fact]
    public void A_posting_outside_the_period_is_in_no_journal_and_no_total()
    {
        JournalReportResultDto report = Run();

        Assert.DoesNotContain(report.Journals, j => j.Number is "INV/2627/0001" or "INV/2627/0004");
    }

    [Fact]
    public void The_period_dates_are_inclusive()
    {
        // One day, the 5th: only the sales invoice.
        JournalReportResultDto report = Run(from: new DateOnly(2026, 10, 5), to: new DateOnly(2026, 10, 5));

        Assert.Equal([("INV", 77L)], report.Journals.Select(j => (j.SourceCode, j.TransactionId)));
    }

    // ---- Every journal balances, and the period's totals are the ledger's ------------

    [Fact]
    public void Every_journal_balances_and_adds_up_its_lines()
    {
        JournalReportResultDto report = Run();

        Assert.All(report.Journals, j => Assert.True(j.IsBalanced, j.Number));
        Assert.Equal((1_180m, 1_180m), (Entry(report, "INV", 77).TotalDebit, Entry(report, "INV", 77).TotalCredit));
        Assert.Equal(3, Entry(report, "INV", 77).Lines.Count);
    }

    [Fact]
    public void The_periods_totals_are_every_ledger_leg_in_it_and_agree()
    {
        JournalReportResultDto report = Run();

        // 2,500 + 1,180 + 600 + 2,500 + 835.
        Assert.Equal(7_615m, report.TotalDebit);
        Assert.Equal(7_615m, report.TotalCredit);
    }

    [Fact]
    public void The_periods_totals_equal_the_general_ledger_summarys_debits_and_credits_for_the_same_dates()
    {
        // The Trial Balance's movement is the same sum by account; this holds the report to it.
        List<GeneralLedgerSummaryRow> summary =
        [
            .. GeneralLedgerSummarySourceRepository.Rows(
                Accounts.AsQueryable(), Ledger.AsQueryable(), October1, October31, new DateOnly(2026, 4, 1)),
        ];

        JournalReportResultDto report = Run();

        Assert.Equal(summary.Sum(r => r.Debit), report.TotalDebit);
        Assert.Equal(summary.Sum(r => r.Credit), report.TotalCredit);
    }

    // ---- A journal's header ---------------------------------------------------------

    [Fact]
    public void A_manual_journal_takes_its_header_from_the_journal()
    {
        JournalReportEntryDto journal = Entry(Run(), "JRN", 11);

        Assert.Equal(("JV-0011", "REF-9", "Rent accrual", JournalStatus.Reversed), (journal.Number, journal.Reference, journal.Narration, journal.Status));
        Assert.Equal(11L, journal.JournalId);
        Assert.Equal(("Asha", "Ravi"), (journal.CreatedByName, journal.PostedByName));
        Assert.Equal(new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero), journal.CreatedAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero), journal.PostedAt);
    }

    [Fact]
    public void A_reversal_shows_what_it_reverses_and_a_reversed_journal_what_reversed_it()
    {
        JournalReportResultDto report = Run();

        Assert.Equal("JV-0012", Entry(report, "JRN", 11).ReversedByNumber);
        Assert.Null(Entry(report, "JRN", 11).ReversesNumber);
        Assert.Equal("JV-0011", Entry(report, "JRN", 12).ReversesNumber);
        Assert.Null(Entry(report, "JRN", 12).ReversedByNumber);
        Assert.Equal(JournalStatus.Posted, Entry(report, "JRN", 12).Status);
    }

    [Fact]
    public void A_documents_own_posting_is_posted_under_its_number_by_the_user_who_posted_it()
    {
        JournalReportEntryDto invoice = Entry(Run(), "INV", 77);

        Assert.Equal(("INV/2627/0002", JournalStatus.Posted, (long?)null), (invoice.Number, invoice.Status, invoice.JournalId));
        Assert.Null(invoice.Reference);
        Assert.Equal(("Meena", "Meena"), (invoice.CreatedByName, invoice.PostedByName));
        Assert.Equal(invoice.CreatedAt, invoice.PostedAt);
    }

    [Fact]
    public void A_posting_written_before_the_ledger_carried_a_number_gets_its_type_and_id()
    {
        Assert.Equal("SPM-5", Entry(Run(), "SPM", 5).Number);

        Assert.Equal("BIL-9", JournalReportRepository.DocumentNoOf(null, "BIL", 9));
        Assert.Equal("BIL-9", JournalReportRepository.DocumentNoOf(" ", "BIL", 9));
        Assert.Equal("INV/1", JournalReportRepository.DocumentNoOf("INV/1", "BIL", 9));
    }

    [Fact]
    public void A_journal_with_no_memo_takes_the_first_lines_description_and_one_with_a_memo_keeps_it()
    {
        // The reversal has a memo and its lines have none; the journal's own memo is what is shown.
        Assert.Equal("Reversal of JV-0011", Entry(Run(), "JRN", 12).Narration);

        // The same journal as JV-0011 with its memo blanked: its first line's description stands in.
        JournalHeader noMemo = JournalReportRepository.HeaderOf(
            Journal(11, "JV-0011", new DateOnly(2026, 10, 3), JournalStatus.Posted, memo: null, created: Asha, posted: Ravi));
        List<JournalLineRow> lines =
        [
            .. JournalReportRepository.Lines(Ledger.AsQueryable(), Accounts.AsQueryable(), Contacts.AsQueryable(), new[] { "JRN" }, new[] { 11L }),
        ];

        JournalReportEntryDto entry = Assert.Single(
            JournalReportRepository.Compose(
                [new JournalKey { SourceCode = "JRN", TransactionId = 11, Date = new DateOnly(2026, 10, 3), FirstLedgerId = 1 }],
                lines, [noMemo], new Dictionary<long, string?>(), Names));

        Assert.Equal("Rent accrual", entry.Narration);

        // A document's own posting has no memo to take, and none on its lines here.
        Assert.Null(Entry(Run(), "INV", 77).Narration);
    }

    // ---- The lines ------------------------------------------------------------------

    [Fact]
    public void A_line_names_its_account_contact_and_amounts_in_both_currencies()
    {
        JournalReportLineDto receivable = Entry(Run(), "INV", 77).Lines.Single(l => l.AccountId == Receivable);

        Assert.Equal(("1100", "Accounts Receivable", Customer, "Kaveri Constructions"),
            (receivable.AccountCode, receivable.AccountName, receivable.ContactId, receivable.ContactName));
        Assert.Equal((1_180m, 0m, 1_180m, 0m), (receivable.Debit, receivable.Credit, receivable.DebitSource, receivable.CreditSource));

        // A line that names no contact is still listed, with none.
        JournalReportLineDto sales = Entry(Run(), "INV", 77).Lines.Single(l => l.AccountId == Sales);
        Assert.Null(sales.ContactId);
        Assert.Null(sales.ContactName);
    }

    [Fact]
    public void A_line_in_another_currency_carries_the_rate_it_was_booked_at()
    {
        JournalReportLineDto line = Entry(Run(), "INV", 78).Lines.Single(l => l.AccountId == Receivable);

        Assert.Equal(("USD", 83.5m, 10m, 835m), (line.CurrencyCode, line.ExchangeRate, line.DebitSource, line.Debit));
    }

    [Fact]
    public void Lines_come_in_line_order()
    {
        Assert.Equal([1, 2, 3], Entry(Run(), "INV", 77).Lines.Select(l => l.LineNumber));
    }

    // ---- The filters ----------------------------------------------------------------

    [Fact]
    public void A_source_keeps_only_the_journals_that_document_type_posted()
    {
        Assert.Equal([77L, 78L], Run(source: "INV").Journals.Select(j => j.TransactionId));
        Assert.Equal([11L, 12L], Run(source: "JRN").Journals.Select(j => j.TransactionId));
        Assert.Empty(Run(source: "BIL").Journals);
        Assert.Equal(2, Run(source: "INV").TotalJournals);
    }

    [Fact]
    public void Reversed_keeps_only_the_manual_journals_that_have_been_reversed()
    {
        JournalReportResultDto report = Run(status: JournalStatus.Reversed);

        Assert.Equal([("JRN", 11L)], report.Journals.Select(j => (j.SourceCode, j.TransactionId)));
        Assert.Equal((2_500m, 2_500m), (report.TotalDebit, report.TotalCredit));
    }

    [Fact]
    public void Posted_keeps_every_journal_that_has_not_been_reversed()
    {
        JournalReportResultDto report = Run(status: JournalStatus.Posted);

        Assert.DoesNotContain(report.Journals, j => j.TransactionId == 11 && j.SourceCode == "JRN");
        Assert.Equal(4, report.TotalJournals);
        Assert.Contains(report.Journals, j => j.SourceCode == "JRN" && j.TransactionId == 12);
    }

    [Fact]
    public void An_account_keeps_the_journals_that_have_a_line_on_it_and_all_of_their_lines()
    {
        JournalReportResultDto report = Run(accountId: Rent);

        Assert.Equal([("JRN", 11L), ("SPM", 5L), ("JRN", 12L)], report.Journals.Select(j => (j.SourceCode, j.TransactionId)));

        // The journal still balances: its payable line is there though the filter asked for rent.
        Assert.Equal(2, Entry(report, "JRN", 11).Lines.Count);
        Assert.All(report.Journals, j => Assert.True(j.IsBalanced));
        Assert.Equal((5_600m, 5_600m), (report.TotalDebit, report.TotalCredit));
    }

    [Fact]
    public void An_account_nothing_was_posted_to_keeps_no_journal()
    {
        Assert.Empty(Run(accountId: 9999).Journals);
    }

    [Fact]
    public void The_filters_combine()
    {
        Assert.Equal([12L], Run(source: "JRN", status: JournalStatus.Posted, accountId: Rent).Journals.Select(j => j.TransactionId));
    }

    // ---- Drafts ---------------------------------------------------------------------

    [Fact]
    public void A_draft_is_listed_only_when_asked_for_and_is_in_no_ledger_total()
    {
        Assert.DoesNotContain(Run().Journals, j => j.Status == JournalStatus.Draft);
        Assert.Equal(7_615m, Run().TotalDebit);

        JournalReportResultDto drafts = Run(status: JournalStatus.Draft);

        JournalReportEntryDto draft = Assert.Single(drafts.Journals);
        Assert.Equal((13L, "Draft 13", JournalStatus.Draft, "Not yet posted"), (draft.JournalId, draft.Number, draft.Status, draft.Narration));
        Assert.Equal(("Asha", (string?)null), (draft.CreatedByName, draft.PostedByName));
        Assert.Equal((100m, 100m, true), (draft.TotalDebit, draft.TotalCredit, draft.IsBalanced));
        Assert.Equal([1, 2], draft.Lines.Select(l => l.LineNumber));
        Assert.Equal("Stationery", draft.Lines[0].Description);
        Assert.Equal((1, 100m, 100m), (drafts.TotalJournals, drafts.TotalDebit, drafts.TotalCredit));
    }

    [Fact]
    public void A_draft_is_only_a_manual_journal_and_the_account_filter_applies_to_its_lines()
    {
        Assert.Empty(Run(status: JournalStatus.Draft, source: "INV").Journals);
        Assert.Single(Run(status: JournalStatus.Draft, source: "JRN").Journals);
        Assert.Single(Run(status: JournalStatus.Draft, accountId: Rent).Journals);
        Assert.Empty(Run(status: JournalStatus.Draft, accountId: Payable).Journals);
    }

    // ---- Pages ----------------------------------------------------------------------

    [Fact]
    public void Journals_come_a_page_at_a_time_with_the_totals_of_all_of_them()
    {
        JournalReportResultDto first = Run(page: 1, size: 2);
        JournalReportResultDto second = Run(page: 2, size: 2);
        JournalReportResultDto third = Run(page: 3, size: 2);

        Assert.Equal([11L, 77L], first.Journals.Select(j => j.TransactionId));
        Assert.Equal([5L, 12L], second.Journals.Select(j => j.TransactionId));
        Assert.Equal([78L], third.Journals.Select(j => j.TransactionId));

        // The count and the totals are of the whole period on every page.
        Assert.All(new[] { first, second, third }, p => Assert.Equal((5, 7_615m), (p.TotalJournals, p.TotalDebit)));
    }

    [Fact]
    public void A_page_past_the_end_is_empty_not_an_error()
    {
        Assert.Empty(Run(page: 9, size: 25).Journals);
    }

    // ---- The request and the guards -------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(JournalReportRequest.FromDate), nameof(JournalReportRequest.ToDate)],
            typeof(JournalReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new JournalReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(
            ["From date is required.", "To date is required."],
            results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(0, 25, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 101, false)]
    [InlineData(1, 100, true)]
    [InlineData(2, 25, true)]
    public void The_page_and_its_size_are_held_to_a_range(int pageNumber, int pageSize, bool accepted)
    {
        var request = new JournalReportRequest { FromDate = October1, ToDate = October31, PageNumber = pageNumber, PageSize = pageSize };

        Assert.Equal(accepted, Validator.TryValidateObject(request, new ValidationContext(request), [], validateAllProperties: true));
    }

    [Fact]
    public void A_period_that_ends_before_it_begins_is_refused_at_the_to_date()
    {
        Assert.Equal(
            [(nameof(JournalReportRequest.ToDate), "To date cannot be before the from date.")],
            JournalReportRepository.DateRefusals(new JournalReportRequest { FromDate = October31, ToDate = October1 }));

        Assert.Empty(JournalReportRepository.DateRefusals(new JournalReportRequest { FromDate = October1, ToDate = October1 }));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new JournalReportController(null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("org_id", Guid.NewGuid().ToString())], "test")),
                },
            },
        };

        IActionResult result = await controller.Run(
            new JournalReportRequest { FromDate = October1, ToDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_accounting_view_and_a_trade()
    {
        Type controller = typeof(JournalReportController);

        Assert.Equal("api/statements/journal-report", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        Assert.Contains(
            "accounting.view",
            controller.GetMethod(nameof(JournalReportController.Run))!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
    }

    // ---- The catalog source, held to the same period --------------------------------

    [Fact]
    public void The_catalog_source_declares_both_dates_required()
    {
        var source = new JournalReportSourceRepository(
            new BatchedNameResolverRepository(
                new HttpClient { BaseAddress = new Uri("http://reporting.tests.invalid") },
                new MemoryCache(new MemoryCacheOptions())));

        Assert.Equal(["from", "to"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
    }
}
