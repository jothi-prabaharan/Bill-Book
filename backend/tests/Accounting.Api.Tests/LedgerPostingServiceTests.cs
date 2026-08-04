using Accounting.Api.Services;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// The ledger door, against a real database.
///
/// What is being proved here is the property T0.1 exists for: a document has
/// several kinds of leg, they only balance together, and two different services
/// write to the same document without either taking the other's rows.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class LedgerPostingServiceTests
{
    private const int Item = 1;
    private const int Tax = 2;
    private const int Control = 3;
    private const int Cogs = 4;
    private const int RoundOff = 6;

    /// <summary>`mst.LedgerSources` 1 — a document posting.</summary>
    private const int DocumentPosting = 1;

    /// <summary>`mst.LedgerSources` 2 and 8 — the two halves of an overpayment.</summary>
    private const int BillPayment = 2;

    private const int VendorPrepayment = 8;

    private readonly PostgresFixture _postgres;

    public LedgerPostingServiceTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>
    /// The whole reason the leg type moved onto the leg. An invoice's revenue
    /// credits are per line, its GST credits are per rate, and its one receivable
    /// debit is neither — no subset of them balances, so before this they could
    /// not be posted at all.
    /// </summary>
    [SkippableFact]
    public async Task An_invoice_with_every_leg_type_posts_in_one_call()
    {
        await using Harness harness = await Harness.CreateAsync(_postgres);

        PostLedgerResult result = await harness.Postings.PostAsync(new PostLedgerRequest
        {
            TransactionTypeCode = "INV",
            TransactionId = 5001,
            LedgerDate = new DateOnly(2026, 8, 1),
            Legs =
            [
                // Three lines of revenue, two GST rates, one receivable, a round-off.
                Leg(Item, 1, harness.RevenueId, credit: 1000m),
                Leg(Item, 2, harness.RevenueId, credit: 500m),
                Leg(Item, 3, harness.RevenueId, credit: 250m),
                Leg(Tax, 0, harness.OutputGstId, credit: 180m),
                Leg(Tax, 0, harness.OutputGstId, credit: 25m),
                Leg(Control, 0, harness.ReceivableId, debit: 1955.40m),
                Leg(RoundOff, 0, harness.RoundOffId, credit: 0.40m),
            ],
        }, CancellationToken.None);

        Assert.Equal(PostLedgerOutcome.Ok, result.Outcome);
        Assert.Equal(7, result.Posted);
    }

    /// <summary>
    /// The two-writer case. Sales owns the invoice's revenue, tax and receivable
    /// legs; Inventory's costing worker adds the cost of sale to the same invoice
    /// minutes later. Re-posting either must leave the other's rows exactly where
    /// they were, or a re-costed movement would silently delete the sale.
    /// </summary>
    [SkippableFact]
    public async Task Re_posting_a_document_leaves_another_services_legs_untouched()
    {
        await using Harness harness = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await harness.Postings.PostAsync(new PostLedgerRequest
        {
            TransactionTypeCode = "INV",
            TransactionId = 6001,
            LedgerDate = new DateOnly(2026, 8, 1),
            Legs =
            [
                Leg(Item, 1, harness.RevenueId, credit: 1000m),
                Leg(Control, 0, harness.ReceivableId, debit: 1000m),
            ],
        }, ct);

        // Inventory, on the same invoice, under its own leg type.
        await harness.Postings.PostAsync(new PostLedgerRequest
        {
            TransactionTypeCode = "INV",
            TransactionId = 6001,
            LedgerDate = new DateOnly(2026, 8, 1),
            Legs =
            [
                Leg(Cogs, 1, harness.CogsId, debit: 600m),
                Leg(Cogs, 1, harness.InventoryId, credit: 600m),
            ],
        }, ct);

        // Sales posts again — a retry, or a corrected total.
        PostLedgerResult again = await harness.Postings.PostAsync(new PostLedgerRequest
        {
            TransactionTypeCode = "INV",
            TransactionId = 6001,
            LedgerDate = new DateOnly(2026, 8, 1),
            Legs =
            [
                Leg(Item, 1, harness.RevenueId, credit: 1000m),
                Leg(Control, 0, harness.ReceivableId, debit: 1000m),
            ],
        }, ct);

        Assert.Equal(PostLedgerOutcome.Ok, again.Outcome);
        Assert.Equal(2, again.Replaced);

        List<JournalLedger> rows = await harness.Db.JournalLedger
            .Where(l => l.TransactionTypeCode == "INV" && l.TransactionId == 6001)
            .ToListAsync(ct);

        // Four rows, not six: the two Sales legs were replaced rather than
        // doubled, and the two COGS legs survived untouched.
        Assert.Equal(4, rows.Count);
        Assert.Equal(2, rows.Count(r => r.LedgerTypeId == Cogs));
    }

    /// <summary>
    /// Inventory posts one movement at a time, so a document-wide replace by leg
    /// type would have line two erase line one. This is the case that decides the
    /// replace has to be keyed on the line as well as the type.
    /// </summary>
    [SkippableFact]
    public async Task Posting_a_second_line_does_not_erase_the_first()
    {
        await using Harness harness = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        foreach (long line in new long[] { 1, 2, 3 })
        {
            await harness.Postings.PostAsync(new PostLedgerRequest
            {
                TransactionTypeCode = "INV",
                TransactionId = 7001,
                LedgerDate = new DateOnly(2026, 8, 1),
                Legs =
                [
                    Leg(Cogs, line, harness.CogsId, debit: 100m),
                    Leg(Cogs, line, harness.InventoryId, credit: 100m),
                ],
            }, ct);
        }

        List<JournalLedger> rows = await harness.Db.JournalLedger
            .Where(l => l.TransactionTypeCode == "INV" && l.TransactionId == 7001)
            .ToListAsync(ct);

        Assert.Equal(6, rows.Count);
        Assert.Equal(3, rows.Select(r => r.TransactionDetailId).Distinct().Count());
    }

    /// <summary>
    /// A void withdraws the document's own leg types across every line, and
    /// leaves the other writer's alone. It has no legs to infer the types from,
    /// which is why it names them.
    /// </summary>
    [SkippableFact]
    public async Task A_withdrawal_clears_the_named_leg_types_and_no_others()
    {
        await using Harness harness = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await harness.Postings.PostAsync(new PostLedgerRequest
        {
            TransactionTypeCode = "INV",
            TransactionId = 8001,
            LedgerDate = new DateOnly(2026, 8, 1),
            Legs =
            [
                Leg(Item, 1, harness.RevenueId, credit: 400m),
                Leg(Item, 2, harness.RevenueId, credit: 600m),
                Leg(Tax, 0, harness.OutputGstId, credit: 180m),
                Leg(Control, 0, harness.ReceivableId, debit: 1180m),
                Leg(Cogs, 1, harness.CogsId, debit: 700m),
                Leg(Cogs, 1, harness.InventoryId, credit: 700m),
            ],
        }, ct);

        PostLedgerResult voided = await harness.Postings.PostAsync(new PostLedgerRequest
        {
            TransactionTypeCode = "INV",
            TransactionId = 8001,
            LedgerDate = new DateOnly(2026, 8, 1),
            WithdrawLedgerTypeIds = [Item, Tax, Control, RoundOff],
            Legs = [],
        }, ct);

        Assert.Equal(PostLedgerOutcome.Ok, voided.Outcome);
        Assert.Equal(4, voided.Replaced);

        List<JournalLedger> rows = await harness.Db.JournalLedger
            .Where(l => l.TransactionTypeCode == "INV" && l.TransactionId == 8001)
            .ToListAsync(ct);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(Cogs, r.LedgerTypeId));
    }

    /// <summary>
    /// Balance is checked across the request, not per key. A set of legs that
    /// does not agree is refused with the difference, rather than reaching the
    /// database and coming back as a constraint violation.
    /// </summary>
    [SkippableFact]
    public async Task An_unbalanced_request_is_refused_before_it_reaches_the_database()
    {
        await using Harness harness = await Harness.CreateAsync(_postgres);

        PostLedgerResult result = await harness.Postings.PostAsync(new PostLedgerRequest
        {
            TransactionTypeCode = "INV",
            TransactionId = 9001,
            LedgerDate = new DateOnly(2026, 8, 1),
            Legs =
            [
                Leg(Item, 1, harness.RevenueId, credit: 1000m),
                Leg(Control, 0, harness.ReceivableId, debit: 900m),
            ],
        }, CancellationToken.None);

        Assert.Equal(PostLedgerOutcome.Unbalanced, result.Outcome);
        Assert.Contains("900", result.Detail);
    }

    /// <summary>
    /// The case that put the ledger source on the leg. Paying ₹11,000 against a
    /// ₹10,000 bill is a bill payment <b>and</b> a vendor prepayment on one
    /// document — ₹10,000 settles the bill and ₹1,000 becomes an advance.
    ///
    /// Stamp the whole document "overpayment" and a payables report filtering on
    /// bill payments misses ₹10,000 of a real one. Nothing fails; the number is
    /// just quietly wrong.
    /// </summary>
    [SkippableFact]
    public async Task An_overpayment_carries_two_sources_on_one_document()
    {
        await using Harness harness = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        PostLedgerResult result = await harness.Postings.PostAsync(new PostLedgerRequest
        {
            TransactionTypeCode = "SPM",
            TransactionId = 4001,
            LedgerDate = new DateOnly(2026, 8, 1),
            Legs =
            [
                Leg(Control, 1, harness.PayableId, debit: 10_000m, ledgerSourceId: BillPayment),
                Leg(Control, 2, harness.AdvanceToVendorId, debit: 1_000m,
                    ledgerSourceId: VendorPrepayment),
                Leg(Control, 0, harness.BankId, credit: 11_000m, ledgerSourceId: BillPayment),
            ],
        }, ct);

        Assert.Equal(PostLedgerOutcome.Ok, result.Outcome);

        List<JournalLedger> rows = await harness.Db.JournalLedger
            .Where(l => l.TransactionTypeCode == "SPM" && l.TransactionId == 4001)
            .ToListAsync(ct);

        // The settled part still reads as a bill payment.
        Assert.Equal(
            10_000m,
            rows.Where(r => r.LedgerSourceId == BillPayment && r.AccountId == harness.PayableId)
                .Sum(r => r.DebitAmountBase));

        // And the excess reads as an advance, on the same document.
        JournalLedger advance = Assert.Single(
            rows, r => r.LedgerSourceId == VendorPrepayment);

        Assert.Equal(harness.AdvanceToVendorId, advance.AccountId);
        Assert.Equal(1_000m, advance.DebitAmountBase);
    }

    /// <summary>A withdrawal that names nothing says nothing about what to remove.</summary>
    [SkippableFact]
    public async Task A_withdrawal_naming_no_leg_types_is_refused()
    {
        await using Harness harness = await Harness.CreateAsync(_postgres);

        PostLedgerResult result = await harness.Postings.PostAsync(new PostLedgerRequest
        {
            TransactionTypeCode = "INV",
            TransactionId = 9002,
            LedgerDate = new DateOnly(2026, 8, 1),
            Legs = [],
        }, CancellationToken.None);

        Assert.Equal(PostLedgerOutcome.WithdrawalTypesMissing, result.Outcome);
    }

    /// <summary>
    /// A settlement's three legs do not share one rate, and the door has to let
    /// them not share it.
    ///
    /// A USD 1,000 bill booked at ₹80 to the dollar, paid when the rate says
    /// ₹100: the payable comes off at ₹80,000 because that is what it went on at,
    /// ₹100,000 leaves the bank, and the ₹20,000 between them is the loss. Force
    /// all three onto the posting's one rate and either the payable keeps a
    /// residue or the request does not balance — there is no third answer, which
    /// is why the rate belongs on the leg.
    /// </summary>
    [SkippableFact]
    public async Task A_leg_can_convert_at_its_own_rate_and_in_its_own_currency()
    {
        await using Harness harness = await Harness.CreateAsync(_postgres);

        LedgerLegRequest payable = Leg(Control, 1, harness.PayableId, debit: 1_000m, ledgerSourceId: BillPayment);
        payable.ExchangeRate = 1m / 80m;

        LedgerLegRequest bank = Leg(Control, 1, harness.BankId, credit: 1_000m, ledgerSourceId: BillPayment);

        LedgerLegRequest fx = Leg(5, 1, harness.FxId, debit: 20_000m, ledgerSourceId: BillPayment);
        fx.CurrencyCode = "INR";
        fx.ExchangeRate = 1m;

        PostLedgerResult result = await harness.Postings.PostAsync(new PostLedgerRequest
        {
            TransactionTypeCode = "SPM",
            TransactionId = 7100,
            LedgerDate = new DateOnly(2026, 8, 1),
            CurrencyCode = "USD",
            ExchangeRate = 1m / 100m,
            Legs = [payable, bank, fx],
        }, CancellationToken.None);

        // It balances only because the three converted differently — the balance
        // check is in base, and this is what makes the three base amounts agree.
        Assert.Equal(PostLedgerOutcome.Ok, result.Outcome);

        List<JournalLedger> rows = await harness.Db.JournalLedger
            .Where(l => l.TransactionTypeCode == "SPM" && l.TransactionId == 7100)
            .ToListAsync(CancellationToken.None);

        JournalLedger payableRow = Assert.Single(rows, r => r.AccountId == harness.PayableId);
        Assert.Equal(80_000m, payableRow.DebitAmountBase);
        Assert.Equal("USD", payableRow.CurrencyCode);

        JournalLedger bankRow = Assert.Single(rows, r => r.AccountId == harness.BankId);
        Assert.Equal(100_000m, bankRow.CreditAmountBase);

        // The difference is denominated in base and stored that way, because it
        // never existed in dollars.
        JournalLedger fxRow = Assert.Single(rows, r => r.AccountId == harness.FxId);
        Assert.Equal(20_000m, fxRow.DebitAmountBase);
        Assert.Equal("INR", fxRow.CurrencyCode);
        Assert.Equal(1m, fxRow.ExchangeRate);
    }

    /// <summary>
    /// What a document was booked at, read back off its own ledger rows — the
    /// number a payment needs before it can settle a foreign-currency document.
    ///
    /// It is the <b>control</b> leg's rate, not any leg's: once settlement legs
    /// exist a document's legs carry different rates, and the one that matters is
    /// the rate the balance being cleared was booked at.
    /// </summary>
    [SkippableFact]
    public async Task The_settlement_rate_is_read_off_the_documents_control_leg()
    {
        await using Harness harness = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        LedgerLegRequest revenue = Leg(Item, 1, harness.RevenueId, credit: 1_000m);
        LedgerLegRequest receivable = Leg(Control, 0, harness.ReceivableId, debit: 1_000m);

        await harness.Postings.PostAsync(new PostLedgerRequest
        {
            TransactionTypeCode = "INV",
            TransactionId = 7200,
            LedgerDate = new DateOnly(2026, 8, 1),
            CurrencyCode = "USD",
            ExchangeRate = 1m / 80m,
            Legs = [revenue, receivable],
        }, ct);

        var reports = new LedgerReportService(harness.Db);

        SettlementRateView? rate = await reports.GetSettlementRateAsync("inv", 7200, ct);

        Assert.NotNull(rate);
        Assert.Equal("INV", rate.TransactionTypeCode);
        Assert.Equal("USD", rate.CurrencyCode);
        Assert.Equal(1m / 80m, rate.ExchangeRate);

        // A document with nothing posted against it answers null — which is also
        // the answer for one in another branch, deliberately.
        Assert.Null(await reports.GetSettlementRateAsync("INV", 7201, ct));
    }

    private static LedgerLegRequest Leg(
        int ledgerTypeId,
        long detailId,
        long accountId,
        decimal debit = 0m,
        decimal credit = 0m,
        int ledgerSourceId = DocumentPosting) =>
        new()
        {
            LedgerTypeId = ledgerTypeId,
            LedgerSourceId = ledgerSourceId,
            TransactionDetailId = detailId,
            AccountId = accountId,
            DebitAmount = debit,
            CreditAmount = credit,
        };

    /// <summary>
    /// One branch with the handful of accounts these tests post to. A fresh
    /// OrgId per test, so the query filter keeps them from seeing each other.
    /// </summary>
    private sealed class Harness : IAsyncDisposable
    {
        public required AccountingDbContext Db { get; init; }

        public required LedgerPostingService Postings { get; init; }

        public required long RevenueId { get; init; }

        public required long ReceivableId { get; init; }

        public required long OutputGstId { get; init; }

        public required long CogsId { get; init; }

        public required long InventoryId { get; init; }

        public required long RoundOffId { get; init; }

        public required long PayableId { get; init; }

        public required long BankId { get; init; }

        public required long AdvanceToVendorId { get; init; }

        public required long FxId { get; init; }

        public static async Task<Harness> CreateAsync(PostgresFixture postgres)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var orgId = Guid.NewGuid();
            AccountingDbContext db = postgres.CreateContext(Guid.NewGuid(), orgId);

            async Task<long> Account(string code, string name, int typeId)
            {
                var account = new Account
                {
                    OrgId = orgId,
                    AccountTypeId = typeId,
                    AccountCode = code,
                    AccountName = name,
                    IsActive = true,
                };

                db.Accounts.Add(account);
                await db.SaveChangesAsync();
                return account.AccountId;
            }

            return new Harness
            {
                Db = db,
                Postings = new LedgerPostingService(
                    db,
                    new TenantContext { CustomerId = Guid.NewGuid(), OrgId = orgId },
                    new StubBaseCurrency()),
                RevenueId = await Account("4100", "Sales Revenue", 4),
                ReceivableId = await Account("1100", "Accounts Receivable", 1),
                OutputGstId = await Account("2200", "Output GST", 2),
                CogsId = await Account("5100", "Cost of Goods Sold", 5),
                InventoryId = await Account("1200", "Inventory", 1),
                RoundOffId = await Account("4990", "Round Off", 4),
                PayableId = await Account("2100", "Accounts Payable", 2),
                BankId = await Account("1510", "Bank — Current", 1),
                AdvanceToVendorId = await Account("1600", "Advance to Vendor", 1),
                FxId = await Account("4900", "Realized FX Gain/Loss", 4),
            };
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
