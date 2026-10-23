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
/// The Account Transaction report's own page (TK-218), on books worked by hand.
/// The financial year starts on 1 April, and October 2026 is the period:
/// <list type="bullet">
/// <item>Before it: sales of 3,000 in February (last financial year) and 5,000 in
/// June, both to contact 7, and 2,000 of rent paid in June.</item>
/// <item>In it: invoice 70 on the 3rd (1,180 receivable, 1,000 sales, 180 GST), rent
/// of 600 paid on the 5th, a credit note of 400 on the 7th against sales returns,
/// a journal of 2,500 rent owed to contact 8 on the 12th, and a USD invoice of 10 at
/// 83.5 (835) on the 15th.</item>
/// <item>After it: an invoice of 70 on 2 November.</item>
/// </list>
/// The builders run on lists through the same expressions the repository runs
/// against the read models; the composition is pure. Nothing here needs PostgreSQL.
/// </summary>
public sealed class AccountTransactionReportTests
{
    private const long Bank = 1001, Receivable = 1100, Payable = 2001, Gst = 2200, Sales = 4001, Returns = 4002, Rent = 5001;
    private const long Customer = 7, Landlord = 8;
    private const long GstTaxSubAccount = 501, ItemSubAccount = 502, GstGroup = 90;

    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October31 = new(2026, 10, 31);

    private static AccountRead Account(long id, int type, string name, bool contra = false) =>
        new() { AccountId = id, AccountTypeId = type, AccountCode = id.ToString(), AccountName = name, IsContra = contra, IsActive = true };

    private static readonly List<AccountRead> Accounts =
    [
        Account(Bank, 1, "Bank"),
        Account(Receivable, 1, "Accounts Receivable"),
        Account(Payable, 2, "Accounts Payable"),
        Account(Gst, 2, "Output GST"),
        Account(Sales, 4, "Sales"),
        Account(Returns, 4, "Sales Returns", contra: true),
        Account(Rent, 5, "Rent"),
    ];

    private static readonly List<ContactRead> Contacts =
    [
        new() { ContactId = Customer, ContactCode = "C-7", DisplayName = "Kaveri Constructions" },
        new() { ContactId = Landlord, ContactCode = "V-8", DisplayName = "Mylapore Properties" },
    ];

    private static readonly List<ContactLicenceRead> Licences =
    [
        new() { ContactLicenceId = 1, ContactId = Customer, LicenceNumber = "DL-77", ExpiresOn = new DateOnly(2026, 10, 10), IsActive = true },
        new() { ContactLicenceId = 2, ContactId = Customer, LicenceNumber = "DL-78", ExpiresOn = null, IsActive = true },
        new() { ContactLicenceId = 3, ContactId = Customer, LicenceNumber = "X-OFF", ExpiresOn = null, IsActive = false },
        new() { ContactLicenceId = 4, ContactId = Landlord, LicenceNumber = "LAND-1", ExpiresOn = new DateOnly(2026, 1, 1), IsActive = true },
    ];

    // A tax sub-account's reference is the tax group. 502 is an item sub-account whose reference
    // number happens to be the same, which a tax lookup must not mistake for the rate.
    private static readonly List<SubAccountRead> SubAccounts =
    [
        new() { SubAccountId = GstTaxSubAccount, AccountId = Gst, ReferenceType = SubAccountReferenceType.Tax, ReferenceId = GstGroup, SubAccountName = "Output GST 18%" },
        new() { SubAccountId = ItemSubAccount, AccountId = Sales, ReferenceType = SubAccountReferenceType.Item, ReferenceId = GstGroup, SubAccountName = "Cement" },
    ];

    // One group, two revisions of the same rate: 17.5 in 2025 and 18 from 2026.
    private static readonly List<TaxMasterRead> TaxMasters =
    [
        new() { TaxMasterId = 90, TaxGroupId = GstGroup, TaxName = "GST 18%", TotalRate = 17.5m, EffectiveFrom = new DateOnly(2025, 1, 1), EffectiveTo = new DateOnly(2025, 12, 31) },
        new() { TaxMasterId = 91, TaxGroupId = GstGroup, TaxName = "GST 18%", TotalRate = 18m, EffectiveFrom = new DateOnly(2026, 1, 1), EffectiveTo = null },
        new() { TaxMasterId = 95, TaxGroupId = 0, TaxName = "GST 5%", TotalRate = 5m, EffectiveFrom = new DateOnly(2025, 1, 1), EffectiveTo = null },
    ];

    private static readonly List<JournalRead> Journals =
    [
        new()
        {
            JournalId = 11, JournalNo = "JV-0011", JournalDate = new DateOnly(2026, 10, 12), Status = JournalStatus.Posted,
            Reference = "REF-9", CurrencyCode = "INR", ExchangeRate = 1m, TransactionTypeCode = "JRN",
        },
    ];

    private static long _id;

    private static JournalLedgerRead Leg(
        string type, long transactionId, DateOnly on, int line, long account, decimal debit, decimal credit,
        string? documentNo = null, long? contact = null, long? journal = null, long? subAccount = null,
        string currency = "INR", decimal rate = 1m, decimal? sourceDebit = null, decimal? sourceCredit = null, string? desc = null) =>
        new()
        {
            LedgerId = ++_id, LedgerDate = on, AccountId = account, SubAccountId = subAccount, TransactionTypeCode = type, TransactionId = transactionId,
            TransactionDetailId = line, DebitAmountBase = debit, CreditAmountBase = credit,
            DebitAmount = sourceDebit ?? debit, CreditAmount = sourceCredit ?? credit,
            CurrencyCode = currency, ExchangeRate = rate, DocumentNo = documentNo, ContactId = contact, JournalId = journal, TransactionDesc = desc,
        };

    private static readonly List<JournalLedgerRead> Ledger =
    [
        // Before the period: last financial year's sale, this year's sale and rent.
        Leg("INV", 60, new(2026, 2, 10), 1, Receivable, 3_000m, 0m, "INV/2526/0001", Customer),
        Leg("INV", 60, new(2026, 2, 10), 2, Sales, 0m, 3_000m, "INV/2526/0001"),
        Leg("INV", 61, new(2026, 6, 10), 1, Receivable, 5_000m, 0m, "INV/2627/0001", Customer),
        Leg("INV", 61, new(2026, 6, 10), 2, Sales, 0m, 5_000m, "INV/2627/0001"),
        Leg("SPM", 3, new(2026, 6, 20), 1, Rent, 2_000m, 0m),
        Leg("SPM", 3, new(2026, 6, 20), 2, Bank, 0m, 2_000m),

        // October.
        Leg("INV", 70, new(2026, 10, 3), 1, Receivable, 1_180m, 0m, "INV/2627/0002", Customer, desc: "Cement"),
        Leg("INV", 70, new(2026, 10, 3), 2, Sales, 0m, 1_000m, "INV/2627/0002", subAccount: ItemSubAccount),
        Leg("INV", 70, new(2026, 10, 3), 3, Gst, 0m, 180m, "INV/2627/0002", subAccount: GstTaxSubAccount),
        Leg("SPM", 4, new(2026, 10, 5), 1, Rent, 600m, 0m),
        Leg("SPM", 4, new(2026, 10, 5), 2, Bank, 0m, 600m),
        Leg("CRN", 9, new(2026, 10, 7), 1, Returns, 400m, 0m, "CN/2627/0001", Customer),
        Leg("CRN", 9, new(2026, 10, 7), 2, Receivable, 0m, 400m, "CN/2627/0001", Customer),
        Leg("JRN", 11, new(2026, 10, 12), 1, Rent, 2_500m, 0m, "JV-0011", journal: 11),
        Leg("JRN", 11, new(2026, 10, 12), 2, Payable, 0m, 2_500m, "JV-0011", Landlord, journal: 11),
        Leg("INV", 71, new(2026, 10, 15), 1, Receivable, 835m, 0m, "INV/2627/0003", Customer, currency: "USD", rate: 83.5m, sourceDebit: 10m, sourceCredit: 0m),
        Leg("INV", 71, new(2026, 10, 15), 2, Sales, 0m, 835m, "INV/2627/0003", currency: "USD", rate: 83.5m, sourceDebit: 0m, sourceCredit: 10m),

        // After the period.
        Leg("INV", 72, new(2026, 11, 2), 1, Receivable, 70m, 0m, "INV/2627/0004", Customer),
        Leg("INV", 72, new(2026, 11, 2), 2, Sales, 0m, 70m, "INV/2627/0004"),
    ];

    /// <summary>A page of the report, through the same builders, lookups and composition the repository uses.</summary>
    private static AccountTransactionResultDto Run(
        DateOnly? from = null, DateOnly? to = null, long[]? accounts = null, long? contact = null, long? tax = null,
        string? source = null, int page = 1, int size = 100)
    {
        DateOnly start = from ?? October1, end = to ?? October31;
        DateOnly yearStart = PortalAccountRepository.FinancialYearStart(start, 4);
        List<long> chosen = [.. accounts ?? []];

        IQueryable<JournalLedgerRead> scoped = AccountTransactionReportRepository.Scope(Ledger.AsQueryable(), SubAccounts.AsQueryable(), chosen, contact, tax, source);
        IQueryable<JournalLedgerRead> period = AccountTransactionReportRepository.InPeriod(scoped, start, end);
        List<long> posted = [.. period.Select(l => l.AccountId).Distinct()];
        List<long> idle = [.. AccountTransactionReportRepository.IdleAccounts(chosen, posted, contact, tax, source)];

        List<PostingKey> keys = [.. AccountTransactionReportRepository.Keys(period, Accounts.AsQueryable()).Skip((page - 1) * size).Take(size)];
        List<long> pageAccounts = [.. keys.Select(k => k.AccountId).Distinct()];
        List<long> blockIds = page == 1 ? [.. pageAccounts, .. idle] : pageAccounts;

        List<AccountRead> blockAccounts = [.. Accounts.Where(a => blockIds.Contains(a.AccountId))];
        List<AccountSum> sums = [.. AccountTransactionReportRepository.AccountSums(scoped, blockIds, start, end, yearStart)];

        List<PostingRow> rows = [];
        PageLookups lookups = new();
        (int earlierCount, decimal earlierNet) = (0, 0m);

        if (keys.Count > 0)
        {
            rows = [.. AccountTransactionReportRepository.Postings(period, keys.Select(k => k.LedgerId).ToList())];
            lookups = LookupsFor(rows);

            PostingKey first = keys[0];
            IQueryable<JournalLedgerRead> before = AccountTransactionReportRepository.Before(period, first.AccountId, first.Date, first.LedgerId);
            earlierCount = before.Count();
            earlierNet = before.Sum(l => l.DebitAmountBase - l.CreditAmountBase);
        }

        return new AccountTransactionResultDto
        {
            FromDate = start, ToDate = end, FinancialYearStart = yearStart, PageNumber = page, PageSize = size,
            TotalPostings = period.Count(), TotalAccounts = posted.Count + idle.Count,
            TotalDebit = period.Sum(l => l.DebitAmountBase), TotalCredit = period.Sum(l => l.CreditAmountBase),
            Accounts = AccountTransactionReportRepository.Compose(keys, rows, blockAccounts, sums, idle, page == 1, earlierCount, earlierNet, lookups),
        };
    }

    /// <summary>The batched lookups of <c>RunAsync</c>, over the lists.</summary>
    private static PageLookups LookupsFor(IReadOnlyList<PostingRow> rows)
    {
        List<string> codes = [.. rows.Select(r => r.SourceCode).Distinct()];
        List<long> transactionIds = [.. rows.Select(r => r.TransactionId).Distinct()];
        List<long> contactIds = [.. rows.Where(r => r.ContactId is not null).Select(r => r.ContactId!.Value).Distinct()];
        List<long> subAccountIds = [.. rows.Where(r => r.SubAccountId is not null).Select(r => r.SubAccountId!.Value).Distinct()];
        List<long> journalIds = [.. rows.Select(AccountTransactionReportRepository.JournalIdOf).Where(i => i is not null).Select(i => i!.Value).Distinct()];

        List<DocumentLeg> legs = [.. AccountTransactionReportRepository.DocumentLegs(Ledger.AsQueryable(), codes, transactionIds)];
        List<TaxSubAccount> taxSubAccounts = [.. AccountTransactionReportRepository.TaxSubAccounts(SubAccounts.AsQueryable(), subAccountIds)];
        List<long> groupIds = [.. taxSubAccounts.Select(s => s.TaxGroupId).Distinct()];

        return new PageLookups
        {
            Legs = legs,
            AccountNames = AccountTransactionReportRepository
                .AccountNames(Accounts.AsQueryable(), legs.Select(l => l.AccountId).Distinct().ToList())
                .ToDictionary(n => n.AccountId, n => n.Name),
            Contacts = AccountTransactionReportRepository.ContactRefs(Contacts.AsQueryable(), contactIds).ToDictionary(c => c.ContactId),
            Permits = [.. AccountTransactionReportRepository.Permits(Licences.AsQueryable(), contactIds)],
            TaxSubAccounts = taxSubAccounts.ToDictionary(s => s.SubAccountId, s => s.TaxGroupId),
            TaxRevisions = [.. AccountTransactionReportRepository.TaxRevisions(TaxMasters.AsQueryable(), groupIds)],
            References = AccountTransactionReportRepository.References(Journals.AsQueryable(), journalIds).ToDictionary(r => r.JournalId, r => r.Reference),
        };
    }

    private static AccountTransactionAccountDto Block(AccountTransactionResultDto report, long accountId) =>
        report.Accounts.Single(a => a.AccountId == accountId);

    // ---- Which postings, in what order -------------------------------------------------

    [Fact]
    public void Postings_come_account_by_account_in_code_order_and_date_order_within_one()
    {
        AccountTransactionResultDto report = Run();

        Assert.Equal([Bank, Receivable, Payable, Gst, Sales, Returns, Rent], report.Accounts.Select(a => a.AccountId));
        Assert.Equal(11, report.TotalPostings);
        Assert.Equal(7, report.TotalAccounts);
        Assert.Equal([70L, 9L, 71L], Block(report, Receivable).Rows.Select(r => r.TransactionId));
        Assert.Equal([4L, 11L], Block(report, Rent).Rows.Select(r => r.TransactionId));
    }

    [Fact]
    public void A_posting_outside_the_period_is_in_no_account_and_no_total()
    {
        AccountTransactionResultDto report = Run();

        Assert.DoesNotContain(report.Accounts.SelectMany(a => a.Rows), r => r.DocumentNo is "INV/2526/0001" or "INV/2627/0001" or "INV/2627/0004");
    }

    [Fact]
    public void The_period_dates_are_inclusive()
    {
        AccountTransactionResultDto report = Run(from: new DateOnly(2026, 10, 3), to: new DateOnly(2026, 10, 3));

        Assert.Equal(3, report.TotalPostings);
        Assert.All(report.Accounts.SelectMany(a => a.Rows), r => Assert.Equal("INV/2627/0002", r.DocumentNo));
    }

    [Fact]
    public void The_periods_totals_are_every_posting_in_it_and_agree()
    {
        AccountTransactionResultDto report = Run();

        // 1,180 + 600 + 400 + 2,500 + 835.
        Assert.Equal((5_515m, 5_515m), (report.TotalDebit, report.TotalCredit));
    }

    // ---- Opening, running and closing balances -----------------------------------------

    [Fact]
    public void A_balance_sheet_account_opens_at_everything_before_the_period()
    {
        AccountTransactionAccountDto receivable = Block(Run(), Receivable);

        // 3,000 + 5,000 before 1 October, then 1,180 − 400 + 835 in it.
        Assert.Equal((8_000m, 9_615m, LedgerSide.Debit), (receivable.OpeningBalance, receivable.ClosingBalance, receivable.NormalSide));
        Assert.Equal((2_015m, 400m, 3), (receivable.TotalDebit, receivable.TotalCredit, receivable.PostingCount));
        Assert.Equal([9_180m, 8_780m, 9_615m], receivable.Rows.Select(r => r.RunningBalance));
    }

    [Fact]
    public void An_income_account_opens_at_what_was_posted_since_its_financial_year_began()
    {
        AccountTransactionAccountDto sales = Block(Run(), Sales);

        // February's 3,000 was last financial year's; June's 5,000 is this year's. Credit side.
        Assert.Equal((5_000m, 6_835m, LedgerSide.Credit), (sales.OpeningBalance, sales.ClosingBalance, sales.NormalSide));
        Assert.Equal([6_000m, 6_835m], sales.Rows.Select(r => r.RunningBalance));
    }

    [Fact]
    public void An_expense_account_opens_from_the_financial_year_start_too()
    {
        AccountTransactionAccountDto rent = Block(Run(), Rent);

        Assert.Equal((2_000m, 5_100m, LedgerSide.Debit), (rent.OpeningBalance, rent.ClosingBalance, rent.NormalSide));
        Assert.Equal([2_600m, 5_100m], rent.Rows.Select(r => r.RunningBalance));
    }

    [Fact]
    public void A_contra_account_runs_opposite_its_type()
    {
        AccountTransactionAccountDto returns = Block(Run(), Returns);

        // Sales returns is income, which grows on the credit side, but as a contra account it grows on the debit side.
        Assert.Equal((LedgerSide.Debit, 0m, 400m), (returns.NormalSide, returns.OpeningBalance, returns.ClosingBalance));
        Assert.True(returns.IsContra);
        Assert.Equal([400m], returns.Rows.Select(r => r.RunningBalance));
    }

    [Fact]
    public void An_asset_can_run_negative_and_says_so()
    {
        AccountTransactionAccountDto bank = Block(Run(), Bank);

        Assert.Equal((-2_000m, -2_600m), (bank.OpeningBalance, bank.ClosingBalance));
        Assert.Equal([-2_600m], bank.Rows.Select(r => r.RunningBalance));
    }

    [Fact]
    public void A_liability_runs_on_the_credit_side()
    {
        AccountTransactionAccountDto payable = Block(Run(), Payable);

        Assert.Equal((LedgerSide.Credit, 0m, 2_500m), (payable.NormalSide, payable.OpeningBalance, payable.ClosingBalance));
    }

    [Fact]
    public void Opening_plus_the_movement_is_the_closing_balance_for_every_account_and_the_last_row_ends_there()
    {
        foreach (AccountTransactionAccountDto account in Run().Accounts)
        {
            decimal movement = account.NormalSide == LedgerSide.Debit
                ? account.TotalDebit - account.TotalCredit
                : account.TotalCredit - account.TotalDebit;

            Assert.Equal(account.OpeningBalance + movement, account.ClosingBalance);
            Assert.Equal(account.ClosingBalance, account.Rows[^1].RunningBalance);
        }
    }

    [Fact]
    public void Each_accounts_closing_balance_is_its_closing_balance_on_the_general_ledger_for_the_same_dates()
    {
        List<GeneralLedgerSummaryRow> summary =
            [.. GeneralLedgerSummarySourceRepository.Rows(Accounts.AsQueryable(), Ledger.AsQueryable(), October1, October31, new DateOnly(2026, 4, 1))];

        foreach (AccountTransactionAccountDto account in Run().Accounts)
        {
            // The summary is in debit terms: a credit balance is negative.
            decimal debitTerms = account.NormalSide == LedgerSide.Debit ? account.ClosingBalance : -account.ClosingBalance;
            decimal opening = account.NormalSide == LedgerSide.Debit ? account.OpeningBalance : -account.OpeningBalance;
            GeneralLedgerSummaryRow row = summary.Single(r => r.AccountId == account.AccountId);

            Assert.Equal(row.Closing, debitTerms);
            Assert.Equal(row.OpeningBalance, opening);
            Assert.Equal((row.Debit, row.Credit), (account.TotalDebit, account.TotalCredit));
        }
    }

    [Theory]
    [InlineData(1, false, true)]
    [InlineData(5, false, true)]
    [InlineData(2, false, false)]
    [InlineData(4, false, false)]
    [InlineData(3, false, false)]
    [InlineData(4, true, true)]
    [InlineData(1, true, false)]
    public void Asset_and_expense_accounts_run_debit_and_a_contra_account_the_other_way(int type, bool contra, bool debitNormal)
    {
        Assert.Equal(debitNormal, AccountTransactionReportRepository.IsDebitNormal(type, contra));
    }

    [Fact]
    public void An_account_with_no_posting_so_far_opens_at_nothing()
    {
        Assert.Equal(0m, AccountTransactionReportRepository.OpeningNet(1, null));
        Assert.Equal(300m, AccountTransactionReportRepository.OpeningNet(1, new AccountSum { NetBefore = 300m, NetBeforeYear = 100m }));
        Assert.Equal(200m, AccountTransactionReportRepository.OpeningNet(5, new AccountSum { NetBefore = 300m, NetBeforeYear = 100m }));
    }

    // ---- What each posting says -------------------------------------------------------

    [Fact]
    public void A_postings_other_side_is_named_or_called_various()
    {
        AccountTransactionResultDto report = Run();

        // The invoice's receivable leg was balanced by sales and GST; each of those by the receivable alone.
        Assert.Equal(AccountTransactionReportRepository.Various, Block(report, Receivable).Rows[0].RelatedAccount);
        Assert.Equal("Accounts Receivable", Block(report, Sales).Rows[0].RelatedAccount);
        Assert.Equal("Accounts Receivable", Block(report, Gst).Rows[0].RelatedAccount);
        Assert.Equal("Sales Returns", Block(report, Receivable).Rows[1].RelatedAccount);
        Assert.Equal("Bank", Block(report, Rent).Rows[0].RelatedAccount);
        Assert.Equal("Rent", Block(report, Bank).Rows[0].RelatedAccount);
    }

    [Fact]
    public void A_document_number_is_the_ledgers_or_the_type_and_id_for_an_older_row()
    {
        AccountTransactionResultDto report = Run();

        Assert.Equal("INV/2627/0002", Block(report, Receivable).Rows[0].DocumentNo);
        Assert.Equal("JV-0011", Block(report, Payable).Rows[0].DocumentNo);
        Assert.Equal("SPM-4", Block(report, Bank).Rows[0].DocumentNo);
    }

    [Fact]
    public void A_contact_is_named_by_code_and_name_and_a_bank_leg_names_none()
    {
        AccountTransactionResultDto report = Run();

        AccountTransactionRowDto receivable = Block(report, Receivable).Rows[0];
        Assert.Equal((Customer, "C-7", "Kaveri Constructions"), (receivable.ContactId, receivable.ContactCode, receivable.ContactName));
        Assert.Equal("Mylapore Properties", Block(report, Payable).Rows[0].ContactName);

        AccountTransactionRowDto bank = Block(report, Bank).Rows[0];
        Assert.Null(bank.ContactId);
        Assert.Null(bank.ContactName);
    }

    [Fact]
    public void A_permit_is_the_contacts_licence_that_was_valid_on_the_day()
    {
        AccountTransactionAccountDto receivable = Block(Run(), Receivable);

        // DL-77 expired on the 10th, so the invoice of the 15th shows DL-78; the inactive licence never shows.
        Assert.Equal(["DL-77", "DL-77", "DL-78"], receivable.Rows.Select(r => r.PermitNo));

        // The landlord's only licence had expired by the journal's date, and shows nothing.
        Assert.Null(Block(Run(), Payable).Rows[0].PermitNo);
    }

    [Fact]
    public void A_tax_leg_carries_the_rate_in_force_on_its_date_and_no_other_leg_does()
    {
        AccountTransactionResultDto report = Run();

        AccountTransactionRowDto gst = Block(report, Gst).Rows[0];
        Assert.Equal((18m, "GST 18%"), (gst.TaxRate, gst.TaxName));

        // The sales leg has a sub-account too, an item's, with the same reference number: not a tax.
        Assert.Null(Block(report, Sales).Rows[0].TaxRate);
        Assert.Null(Block(report, Receivable).Rows[0].TaxRate);
    }

    [Fact]
    public void A_tax_revision_is_chosen_by_the_date_and_a_legacy_row_by_its_own_id()
    {
        List<TaxRevision> revisions = [.. AccountTransactionReportRepository.TaxRevisions(TaxMasters.AsQueryable(), [GstGroup, 95])];

        Assert.Equal(17.5m, AccountTransactionReportRepository.TaxOn(revisions, GstGroup, new DateOnly(2025, 6, 1))!.Rate);
        Assert.Equal(18m, AccountTransactionReportRepository.TaxOn(revisions, GstGroup, new DateOnly(2026, 1, 1))!.Rate);
        Assert.Null(AccountTransactionReportRepository.TaxOn(revisions, GstGroup, new DateOnly(2024, 12, 31)));

        // A tax master with no group id is its own group.
        Assert.Equal(5m, AccountTransactionReportRepository.TaxOn(revisions, 95, new DateOnly(2026, 10, 1))!.Rate);
    }

    [Fact]
    public void A_manual_journals_reference_is_shown_and_no_other_postings()
    {
        AccountTransactionResultDto report = Run();

        Assert.Equal("REF-9", Block(report, Rent).Rows[1].Reference);
        Assert.Equal("REF-9", Block(report, Payable).Rows[0].Reference);
        Assert.Null(Block(report, Rent).Rows[0].Reference);
        Assert.Equal(11L, Block(report, Payable).Rows[0].JournalId);
    }

    [Fact]
    public void A_posting_in_another_currency_carries_the_rate_it_was_booked_at()
    {
        AccountTransactionRowDto row = Block(Run(), Receivable).Rows[2];

        Assert.Equal(("USD", 83.5m, 10m, 835m), (row.CurrencyCode, row.ExchangeRate, row.DebitSource, row.Debit));
    }

    // ---- The filters ------------------------------------------------------------------

    [Fact]
    public void A_contact_keeps_only_its_postings_and_narrows_the_opening_with_them()
    {
        AccountTransactionResultDto report = Run(contact: Customer);

        Assert.Equal([Receivable, Returns], report.Accounts.Select(a => a.AccountId));
        Assert.Equal(4, report.TotalPostings);
        Assert.Equal((2_415m, 400m), (report.TotalDebit, report.TotalCredit));
        Assert.Equal(8_000m, Block(report, Receivable).OpeningBalance);

        // Contact 8's only posting is the journal's payable leg.
        AccountTransactionResultDto landlord = Run(contact: Landlord);
        Assert.Equal([Payable], landlord.Accounts.Select(a => a.AccountId));
        Assert.Equal((0m, 2_500m), (Block(landlord, Payable).OpeningBalance, Block(landlord, Payable).ClosingBalance));
    }

    [Fact]
    public void A_source_keeps_only_that_document_types_postings_in_the_opening_too()
    {
        AccountTransactionResultDto invoices = Run(source: "INV");

        Assert.Equal([Receivable, Gst, Sales], invoices.Accounts.Select(a => a.AccountId));
        Assert.Equal(5, invoices.TotalPostings);
        Assert.Equal((2_015m, 2_015m), (invoices.TotalDebit, invoices.TotalCredit));
        Assert.Equal(5_000m, Block(invoices, Sales).OpeningBalance);

        // Rent from spend money alone opens at June's 2,000 and does not take the journal's 2,500.
        AccountTransactionAccountDto rent = Block(Run(source: "SPM"), Rent);
        Assert.Equal((2_000m, 2_600m), (rent.OpeningBalance, rent.ClosingBalance));
    }

    [Fact]
    public void A_tax_rate_keeps_only_the_tax_legs_of_that_group_and_not_an_item_sub_account_with_its_number()
    {
        AccountTransactionResultDto report = Run(tax: GstGroup);

        AccountTransactionAccountDto gst = Assert.Single(report.Accounts);
        Assert.Equal(Gst, gst.AccountId);
        Assert.Equal(1, report.TotalPostings);
        Assert.Equal(180m, gst.ClosingBalance);

        Assert.Empty(Run(tax: 12345).Accounts);
    }

    [Fact]
    public void Accounts_asked_for_keep_only_those_in_code_order()
    {
        AccountTransactionResultDto report = Run(accounts: [Sales, Receivable]);

        Assert.Equal([Receivable, Sales], report.Accounts.Select(a => a.AccountId));
        Assert.Equal(5, report.TotalPostings);
    }

    [Fact]
    public void An_account_asked_for_that_has_nothing_in_the_period_is_listed_with_its_balance()
    {
        // 20 to 31 October: nothing was posted, but the payable owes 2,500 and the bank is overdrawn.
        AccountTransactionResultDto report = Run(from: new DateOnly(2026, 10, 20), accounts: [Payable, Bank]);

        Assert.Equal([Bank, Payable], report.Accounts.Select(a => a.AccountId));
        Assert.All(report.Accounts, a => Assert.Empty(a.Rows));
        Assert.Equal((-2_600m, -2_600m), (Block(report, Bank).OpeningBalance, Block(report, Bank).ClosingBalance));
        Assert.Equal((2_500m, 2_500m), (Block(report, Payable).OpeningBalance, Block(report, Payable).ClosingBalance));
        Assert.Equal((0, 2, 0m), (report.TotalPostings, report.TotalAccounts, report.TotalDebit));
    }

    [Fact]
    public void An_idle_account_is_placed_among_the_others_by_its_code()
    {
        // 1 to 4 October: only the invoice, which posts to the receivable, the sales and the GST.
        AccountTransactionResultDto report = Run(to: new DateOnly(2026, 10, 4), accounts: [Bank, Receivable, Payable]);

        Assert.Equal([Bank, Receivable, Payable], report.Accounts.Select(a => a.AccountId));
        Assert.Empty(Block(report, Bank).Rows);
        Assert.Single(Block(report, Receivable).Rows);
        Assert.Equal((0m, 0m), (Block(report, Payable).OpeningBalance, Block(report, Payable).ClosingBalance));
        Assert.Equal(3, report.TotalAccounts);
    }

    [Fact]
    public void A_narrowing_filter_lists_only_the_accounts_that_have_postings_that_match()
    {
        // Every account is named, but only the customer's postings are asked for: the others have nothing to say.
        long[] all = [Bank, Receivable, Payable, Gst, Sales, Returns, Rent];

        Assert.Equal([Receivable, Returns], Run(accounts: all, contact: Customer).Accounts.Select(a => a.AccountId));
        Assert.Equal([Payable], Run(accounts: all, contact: Landlord).Accounts.Select(a => a.AccountId));
        Assert.Equal([Gst], Run(accounts: all, tax: GstGroup).Accounts.Select(a => a.AccountId));
        Assert.Equal([Bank, Rent], Run(accounts: all, source: "SPM").Accounts.Select(a => a.AccountId));
        Assert.Equal(2, Run(accounts: all, source: "SPM").TotalAccounts);

        // With nothing narrowing them, the same accounts are all listed.
        Assert.Equal(7, Run(accounts: all).Accounts.Count);
    }

    [Fact]
    public void An_idle_account_is_listed_on_the_first_page_only()
    {
        AccountTransactionResultDto second = Run(from: new DateOnly(2026, 10, 20), accounts: [Payable, Bank], page: 2);

        Assert.Empty(second.Accounts);
    }

    // ---- Pages ------------------------------------------------------------------------

    [Fact]
    public void Postings_come_a_page_at_a_time_with_the_totals_of_all_of_them_and_every_posting_once()
    {
        List<AccountTransactionResultDto> pages = [.. Enumerable.Range(1, 4).Select(p => Run(page: p, size: 3))];

        Assert.All(pages, p => Assert.Equal((11, 5_515m), (p.TotalPostings, p.TotalDebit)));
        Assert.Equal([3, 3, 3, 2], pages.Select(p => p.Accounts.Sum(a => a.Rows.Count)));

        List<long> paged = [.. pages.SelectMany(p => p.Accounts).SelectMany(a => a.Rows).Select(r => r.LedgerId)];
        List<long> whole = [.. Run().Accounts.SelectMany(a => a.Rows).Select(r => r.LedgerId)];
        Assert.Equal(whole, paged);
    }

    [Fact]
    public void An_account_that_runs_over_a_page_says_so_and_carries_its_balance_across()
    {
        AccountTransactionAccountDto firstPart = Block(Run(page: 1, size: 3), Receivable);
        AccountTransactionAccountDto secondPart = Block(Run(page: 2, size: 3), Receivable);

        // Page 1 holds the bank and two of the receivable's three postings.
        Assert.Equal((false, true), (firstPart.HasEarlierRows, firstPart.HasLaterRows));
        Assert.Equal([9_180m, 8_780m], firstPart.Rows.Select(r => r.RunningBalance));

        // Page 2 starts with the third, whose balance carries on from the first two.
        Assert.Equal((true, false), (secondPart.HasEarlierRows, secondPart.HasLaterRows));
        Assert.Equal([9_615m], secondPart.Rows.Select(r => r.RunningBalance));

        // Both give the account's whole-period figures, not their own.
        Assert.Equal((8_000m, 9_615m, 3), (secondPart.OpeningBalance, secondPart.ClosingBalance, secondPart.PostingCount));
        Assert.Equal((8_000m, 9_615m, 3), (firstPart.OpeningBalance, firstPart.ClosingBalance, firstPart.PostingCount));
    }

    [Fact]
    public void An_account_wholly_on_a_page_has_neither_flag()
    {
        AccountTransactionAccountDto bank = Block(Run(page: 1, size: 3), Bank);

        Assert.Equal((false, false), (bank.HasEarlierRows, bank.HasLaterRows));
    }

    [Fact]
    public void A_page_past_the_end_is_empty_not_an_error()
    {
        AccountTransactionResultDto report = Run(page: 9, size: 25);

        Assert.Empty(report.Accounts);
        Assert.Equal(11, report.TotalPostings);
    }

    // ---- The request and the guards ---------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(AccountTransactionRequest.FromDate), nameof(AccountTransactionRequest.ToDate)],
            typeof(AccountTransactionRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new AccountTransactionRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(
            ["From date is required.", "To date is required."],
            results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(0, 100, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 501, false)]
    [InlineData(1, 500, true)]
    [InlineData(2, 100, true)]
    public void The_page_and_its_size_are_held_to_a_range(int pageNumber, int pageSize, bool accepted)
    {
        var request = new AccountTransactionRequest { FromDate = October1, ToDate = October31, PageNumber = pageNumber, PageSize = pageSize };

        Assert.Equal(accepted, Validator.TryValidateObject(request, new ValidationContext(request), [], validateAllProperties: true));
    }

    [Fact]
    public void A_period_that_ends_before_it_begins_is_refused_at_the_to_date()
    {
        Assert.Equal(
            [(nameof(AccountTransactionRequest.ToDate), "To date cannot be before the from date.")],
            AccountTransactionReportRepository.Refusals(new AccountTransactionRequest { FromDate = October31, ToDate = October1 }));

        Assert.Empty(AccountTransactionReportRepository.Refusals(new AccountTransactionRequest { FromDate = October1, ToDate = October1 }));
    }

    [Fact]
    public void Too_many_accounts_are_refused_at_the_accounts_field()
    {
        var many = new AccountTransactionRequest { FromDate = October1, ToDate = October31, AccountIds = [.. Enumerable.Range(1, AccountTransactionLimits.MaxAccounts + 1).Select(i => (long)i)] };
        var enough = new AccountTransactionRequest { FromDate = October1, ToDate = October31, AccountIds = [.. Enumerable.Range(1, AccountTransactionLimits.MaxAccounts).Select(i => (long)i)] };

        Assert.Equal(
            [(nameof(AccountTransactionRequest.AccountIds), "Choose no more than 100 accounts.")],
            AccountTransactionReportRepository.Refusals(many));
        Assert.Empty(AccountTransactionReportRepository.Refusals(enough));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new AccountTransactionReportController(null!)
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
            new AccountTransactionRequest { FromDate = October1, ToDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_accounting_view_and_a_trade()
    {
        Type controller = typeof(AccountTransactionReportController);

        Assert.Equal("api/statements/account-transaction", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        Assert.Contains(
            "accounting.view",
            controller.GetMethod(nameof(AccountTransactionReportController.Run))!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
    }

    // ---- The catalog source, held to the same period ----------------------------------

    [Fact]
    public void The_catalog_source_declares_both_dates_required_and_the_account_optional()
    {
        var source = new AccountTransactionSourceRepository();

        Assert.Equal(["from", "to", "accountId"], source.Parameters.Select(p => p.Name));
        Assert.Equal([true, true, false], source.Parameters.Select(p => p.IsRequired));
    }

    [Fact]
    public void The_permits_builder_leaves_out_a_licence_that_is_switched_off()
    {
        List<PermitRef> permits = [.. AccountTransactionReportRepository.Permits(Licences.AsQueryable(), [Customer])];

        Assert.Equal([1L, 2L], permits.Select(p => p.ContactLicenceId).OrderBy(i => i));
    }
}
