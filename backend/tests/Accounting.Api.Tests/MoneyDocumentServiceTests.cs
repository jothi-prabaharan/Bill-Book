using Accounting.Api.Services;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// Spend, receive and transfer money end to end — draft, post, void — with the
/// ledger recorded rather than mocked away, because what these documents are
/// <i>for</i> is the postings they produce.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class MoneyDocumentServiceTests
{
    private const int BillPayment = 2;
    private const int VendorOverpayment = 16;
    private const int VendorPrepayment = 8;
    private const int VendorPrepaymentRefund = 20;
    private const int InvoicePayment = 3;
    private const int CustomerOverpayment = 17;

    private readonly PostgresFixture _postgres;

    public MoneyDocumentServiceTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>
    /// The case the whole design exists for. Paying ₹11,000 against a ₹10,000
    /// bill posts <b>four</b> legs: the settled part debits payables under the
    /// contact's trade balance, the excess debits receivables under the
    /// contact's overpayment advance, and each is paid for by its own bank leg
    /// carrying the same source.
    /// </summary>
    [SkippableFact]
    public async Task An_overpayment_posts_each_half_to_its_own_account()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        MoneyDocumentResult draft = await h.Spend.CreateAsync(new SaveMoneyDocumentRequest
        {
            TransactionDate = new DateOnly(2026, 8, 1),
            BankAccountId = h.BankAccountId,
            ContactId = 7,
            Amount = 11_000m,
            Lines =
            [
                new SaveMoneyLineRequest
                {
                    LedgerSourceId = BillPayment,
                    MappingTransactionTypeCode = "BIL",
                    MappingTransactionId = 500,
                    Amount = 10_000m,
                },
                new SaveMoneyLineRequest { LedgerSourceId = VendorOverpayment, Amount = 1_000m },
            ],
        }, ct);

        Assert.Equal(MoneyDocumentOutcome.Ok, draft.Outcome);
        Assert.Equal(MoneyDocumentOutcome.Ok, (await h.Spend.PostAsync(draft.DocumentId, ct)).Outcome);

        LedgerPosting posting = h.Ledger.Last;

        Assert.Equal("SPM", posting.TransactionTypeCode);
        Assert.Equal(4, posting.Legs.Count);

        // The settled part: payables, the contact's trade balance, debited.
        LedgerPostingLeg settled = Assert.Single(
            posting.Legs,
            l => l.AccountSystemName == "Accounts Payable" && l.DebitAmount > 0);

        Assert.Equal(10_000m, settled.DebitAmount);
        Assert.Equal(MoneyPostingMap.Primary, settled.SubAccountPurpose);
        Assert.Equal(7, settled.SubAccountReferenceId);
        Assert.Equal(BillPayment, settled.LedgerSourceId);

        // The excess: receivables, the contact's overpayment advance.
        LedgerPostingLeg excess = Assert.Single(
            posting.Legs,
            l => l.AccountSystemName == "Accounts Receivable" && l.DebitAmount > 0);

        Assert.Equal(1_000m, excess.DebitAmount);
        Assert.Equal(MoneyPostingMap.OverpaymentAdvance, excess.SubAccountPurpose);
        Assert.Equal(VendorOverpayment, excess.LedgerSourceId);

        // Two bank legs, one per line, each carrying its line's source — so a
        // payables report reading bill payments still sees the ₹10,000.
        List<LedgerPostingLeg> bank = [.. posting.Legs.Where(l => l.AccountId == h.LedgerAccountId)];

        Assert.Equal(2, bank.Count);
        Assert.Equal(11_000m, bank.Sum(l => l.CreditAmount));
        Assert.Contains(bank, l => l.LedgerSourceId == BillPayment);
        Assert.Contains(bank, l => l.LedgerSourceId == VendorOverpayment);

        // And the whole thing balances.
        Assert.Equal(posting.Legs.Sum(l => l.DebitAmount), posting.Legs.Sum(l => l.CreditAmount));
    }

    /// <summary>A supplier deposit is an asset — receivables, prepayment advance.</summary>
    [SkippableFact]
    public async Task A_supplier_deposit_posts_to_the_prepayment_advance()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        MoneyDocumentResult draft = await h.Spend.CreateAsync(
            h.Payment(5_000m, VendorPrepayment), ct);

        await h.Spend.PostAsync(draft.DocumentId, ct);

        LedgerPostingLeg control = Assert.Single(
            h.Ledger.Last.Legs, l => l.AccountSystemName is not null);

        Assert.Equal("Accounts Receivable", control.AccountSystemName);
        Assert.Equal(MoneyPostingMap.PrepaymentAdvance, control.SubAccountPurpose);
        Assert.Equal(5_000m, control.DebitAmount);
    }

    /// <summary>
    /// The mirror of the deposit: the supplier returns it, and the refund
    /// clears the <b>prepayment</b> balance the deposit was placed in — not the
    /// overpayment balance, which is what "Refund of our overpayment" clears.
    /// </summary>
    [SkippableFact]
    public async Task A_returned_supplier_advance_clears_the_prepayment_advance()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        MoneyDocumentResult draft = await h.Receive.CreateAsync(
            h.Payment(2_000m, VendorPrepaymentRefund), ct);

        await h.Receive.PostAsync(draft.DocumentId, ct);

        LedgerPostingLeg control = Assert.Single(
            h.Ledger.Last.Legs, l => l.AccountSystemName is not null);

        Assert.Equal("Accounts Receivable", control.AccountSystemName);
        Assert.Equal(MoneyPostingMap.PrepaymentAdvance, control.SubAccountPurpose);
        Assert.Equal(2_000m, control.CreditAmount);
    }

    /// <summary>Money in reverses every leg: the bank is debited, the control credited.</summary>
    [SkippableFact]
    public async Task A_receipt_debits_the_bank_and_credits_the_contact()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        MoneyDocumentResult draft = await h.Receive.CreateAsync(new SaveMoneyDocumentRequest
        {
            TransactionDate = new DateOnly(2026, 8, 1),
            BankAccountId = h.BankAccountId,
            ContactId = 7,
            Amount = 2_500m,
            Lines =
            [
                new SaveMoneyLineRequest
                {
                    LedgerSourceId = InvoicePayment,
                    MappingTransactionTypeCode = "INV",
                    MappingTransactionId = 900,
                    Amount = 2_500m,
                },
            ],
        }, ct);

        Assert.Equal(MoneyDocumentOutcome.Ok, (await h.Receive.PostAsync(draft.DocumentId, ct)).Outcome);

        LedgerPosting posting = h.Ledger.Last;
        Assert.Equal("RCM", posting.TransactionTypeCode);

        LedgerPostingLeg control = Assert.Single(
            posting.Legs, l => l.AccountSystemName == "Accounts Receivable");

        Assert.Equal(2_500m, control.CreditAmount);
        Assert.Equal(MoneyPostingMap.Primary, control.SubAccountPurpose);

        LedgerPostingLeg bank = Assert.Single(posting.Legs, l => l.AccountId == h.LedgerAccountId);
        Assert.Equal(2_500m, bank.DebitAmount);
    }

    /// <summary>A customer overpayment is a liability — payables, overpayment advance.</summary>
    [SkippableFact]
    public async Task A_customer_overpayment_posts_to_the_payable_side()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        MoneyDocumentResult draft = await h.Receive.CreateAsync(
            h.Payment(750m, CustomerOverpayment), ct);

        await h.Receive.PostAsync(draft.DocumentId, ct);

        LedgerPostingLeg control = Assert.Single(
            h.Ledger.Last.Legs, l => l.AccountSystemName is not null);

        Assert.Equal("Accounts Payable", control.AccountSystemName);
        Assert.Equal(MoneyPostingMap.OverpaymentAdvance, control.SubAccountPurpose);
        Assert.Equal(750m, control.CreditAmount);
    }

    /// <summary>
    /// A transfer settles nothing: two bank legs, no contact, no sub-account. It
    /// is also the only money document that needs no invoice or bill to exist.
    /// </summary>
    [SkippableFact]
    public async Task A_transfer_posts_two_bank_legs_and_no_counterparty()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        MoneyDocumentResult draft = await h.Transfer.CreateAsync(new SaveTransferRequest
        {
            TransactionDate = new DateOnly(2026, 8, 1),
            FromBankAccountId = h.BankAccountId,
            ToBankAccountId = h.CashAccountId,
            Amount = 500m,
        }, ct);

        Assert.Equal(MoneyDocumentOutcome.Ok, draft.Outcome);
        Assert.Equal(MoneyDocumentOutcome.Ok, (await h.Transfer.PostAsync(draft.DocumentId, ct)).Outcome);

        LedgerPosting posting = h.Ledger.Last;

        Assert.Equal("TRM", posting.TransactionTypeCode);
        Assert.Null(posting.ContactId);
        Assert.Equal(2, posting.Legs.Count);
        Assert.All(posting.Legs, l => Assert.Null(l.AccountSystemName));
        Assert.All(posting.Legs, l => Assert.Null(l.SubAccountReferenceId));
        Assert.All(
            posting.Legs,
            l => Assert.Equal(MoneyPostingMap.MoneyTransferSource, l.LedgerSourceId));

        // Into the cash drawer, out of the bank.
        Assert.Equal(500m, Assert.Single(posting.Legs, l => l.AccountId == h.CashLedgerAccountId).DebitAmount);
        Assert.Equal(500m, Assert.Single(posting.Legs, l => l.AccountId == h.LedgerAccountId).CreditAmount);
    }

    [SkippableFact]
    public async Task A_transfer_to_the_same_account_is_refused_before_it_is_written()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        MoneyDocumentResult result = await h.Transfer.CreateAsync(new SaveTransferRequest
        {
            TransactionDate = new DateOnly(2026, 8, 1),
            FromBankAccountId = h.BankAccountId,
            ToBankAccountId = h.BankAccountId,
            Amount = 500m,
        }, CancellationToken.None);

        Assert.Equal(MoneyDocumentOutcome.SameAccount, result.Outcome);
    }

    /// <summary>Posting takes the number; a draft holds none.</summary>
    [SkippableFact]
    public async Task Posting_takes_the_number_and_freezes_the_document()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        MoneyDocumentResult draft = await h.Spend.CreateAsync(h.Payment(100m, BillPayment), ct);

        MoneyDocumentView? before = await h.Spend.GetAsync(draft.DocumentId, ct);
        Assert.Equal("Draft", before!.Status);
        Assert.Null(before.TransactionNo);

        await h.Spend.PostAsync(draft.DocumentId, ct);

        MoneyDocumentView? after = await h.Spend.GetAsync(draft.DocumentId, ct);
        Assert.Equal("Posted", after!.Status);
        Assert.StartsWith("PAY/", after.TransactionNo);

        // And it is frozen.
        Assert.Equal(
            MoneyDocumentOutcome.NotDraft,
            (await h.Spend.UpdateAsync(draft.DocumentId, h.Payment(100m, BillPayment), ct)).Outcome);

        Assert.Equal(
            MoneyDocumentOutcome.NotDraft,
            (await h.Spend.DeleteAsync(draft.DocumentId, ct)).Outcome);
    }

    /// <summary>A document whose lines do not add up is refused with the difference.</summary>
    [SkippableFact]
    public async Task An_under_allocated_document_is_refused_with_the_difference()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        MoneyDocumentResult draft = await h.Spend.CreateAsync(new SaveMoneyDocumentRequest
        {
            TransactionDate = new DateOnly(2026, 8, 1),
            BankAccountId = h.BankAccountId,
            ContactId = 7,
            Amount = 1_000m,
            Lines = [new SaveMoneyLineRequest { LedgerSourceId = BillPayment, Amount = 600m }],
        }, ct);

        MoneyDocumentResult posted = await h.Spend.PostAsync(draft.DocumentId, ct);

        Assert.Equal(MoneyDocumentOutcome.NotAllocated, posted.Outcome);
        Assert.Contains("400", posted.Detail);
        Assert.Empty(h.Ledger.Postings);
    }

    /// <summary>A void withdraws the ledger rows and keeps the document.</summary>
    [SkippableFact]
    public async Task A_void_withdraws_the_posting_and_keeps_the_number()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        MoneyDocumentResult draft = await h.Spend.CreateAsync(h.Payment(100m, BillPayment), ct);
        await h.Spend.PostAsync(draft.DocumentId, ct);

        string? number = (await h.Spend.GetAsync(draft.DocumentId, ct))!.TransactionNo;

        Assert.Equal(
            MoneyDocumentOutcome.Ok,
            (await h.Spend.VoidAsync(draft.DocumentId, new VoidMoneyDocumentRequest
            {
                Reason = "Paid twice",
            }, ct)).Outcome);

        MoneyDocumentView? after = await h.Spend.GetAsync(draft.DocumentId, ct);

        Assert.Equal("Void", after!.Status);

        // The number stays: a gap in a document series is what an auditor asks
        // about.
        Assert.Equal(number, after.TransactionNo);

        // The withdrawal names its leg types, because it has no legs to infer
        // them from — and it names FX as well as CONTROL even though this
        // document produced no FX leg. A withdrawal that named only the types
        // the document happened to write would leave an exchange difference
        // behind on a voided payment, unbalanced and attached to nothing.
        Assert.Empty(h.Ledger.Last.Legs);
        Assert.Equal(
            [MoneyPostingMap.ControlLedgerType, MoneyPosting.FxLedgerType],
            h.Ledger.Last.WithdrawLedgerTypeIds);
    }

    /// <summary>The period lock reaches Banking through the same guard.</summary>
    [SkippableFact]
    public async Task A_document_in_a_closed_period_cannot_be_posted()
    {
        await using Harness h = await Harness.CreateAsync(
            _postgres, new RecordingLedger(lockedUpto: new DateOnly(2026, 8, 31)));

        CancellationToken ct = CancellationToken.None;

        MoneyDocumentResult draft = await h.Spend.CreateAsync(h.Payment(100m, BillPayment), ct);
        MoneyDocumentResult posted = await h.Spend.PostAsync(draft.DocumentId, ct);

        Assert.Equal(MoneyDocumentOutcome.PeriodClosed, posted.Outcome);
        Assert.Contains("31 Aug 2026", posted.Detail);
        Assert.Empty(h.Ledger.Postings);
    }

    /// <summary>
    /// Unreadable is not unlocked. A lock that failed open because a lookup
    /// blipped is not a lock, so the document is refused as transient.
    /// </summary>
    [SkippableFact]
    public async Task An_unreadable_period_lock_refuses_rather_than_allows()
    {
        await using Harness h = await Harness.CreateAsync(
            _postgres, new RecordingLedger(lockUnavailable: true));

        CancellationToken ct = CancellationToken.None;

        MoneyDocumentResult draft = await h.Spend.CreateAsync(h.Payment(100m, BillPayment), ct);
        MoneyDocumentResult posted = await h.Spend.PostAsync(draft.DocumentId, ct);

        Assert.Equal(MoneyDocumentOutcome.PeriodLockUnavailable, posted.Outcome);
        Assert.Empty(h.Ledger.Postings);
    }

    /// <summary>
    /// A line naming something money cannot leave under is refused at save, not
    /// discovered at post — a guessed account is wrong in a balance nobody
    /// re-reads.
    /// </summary>
    [SkippableFact]
    public async Task A_line_with_a_source_money_cannot_leave_under_is_refused()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        // 3 is INVOICEPAYMENT — money coming in, not going out.
        MoneyDocumentResult result = await h.Spend.CreateAsync(
            h.Payment(100m, InvoicePayment), CancellationToken.None);

        Assert.Equal(MoneyDocumentOutcome.UnknownLedgerSource, result.Outcome);
    }

    /// <summary>
    /// If the ledger refuses, the document stays a draft and takes no number —
    /// the posting goes first precisely so this is the failure mode.
    /// </summary>
    [SkippableFact]
    public async Task A_refused_posting_leaves_the_document_a_draft()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        MoneyDocumentResult draft = await h.Spend.CreateAsync(h.Payment(100m, BillPayment), ct);

        h.Ledger.Outcome = LedgerPostOutcome.Refused;

        Assert.Equal(
            MoneyDocumentOutcome.PostingRefused,
            (await h.Spend.PostAsync(draft.DocumentId, ct)).Outcome);

        MoneyDocumentView? after = await h.Spend.GetAsync(draft.DocumentId, ct);
        Assert.Equal("Draft", after!.Status);
        Assert.Null(after.TransactionNo);
    }

    /// <summary>
    /// T6.6 — one payment across three bills. Three lines, three pairs of legs,
    /// each traceable to the bill it cleared and each with its own bank leg.
    ///
    /// The header names no document, and that is the point: this payment is about
    /// three, and picking one of them for the header would be picking arbitrarily.
    /// A list reads the header first.
    /// </summary>
    [SkippableFact]
    public async Task One_payment_across_three_bills_is_three_traceable_lines()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        MoneyDocumentResult draft = await h.Spend.CreateAsync(new SaveMoneyDocumentRequest
        {
            TransactionDate = new DateOnly(2026, 8, 1),
            BankAccountId = h.BankAccountId,
            ContactId = 7,
            Amount = 6_000m,
            Lines =
            [
                Settles(BillPayment, "BIL", 501, 1_000m),
                Settles(BillPayment, "BIL", 502, 2_000m),
                Settles(BillPayment, "BIL", 503, 3_000m),
            ],
        }, ct);

        Assert.Equal(MoneyDocumentOutcome.Ok, draft.Outcome);

        MoneyDocumentView? saved = await h.Spend.GetAsync(draft.DocumentId, ct);

        Assert.Null(saved!.MappingTransactionTypeCode);
        Assert.Null(saved.MappingTransactionId);
        Assert.Equal([501L, 502L, 503L], saved.Lines.Select(l => l.MappingTransactionId));

        Assert.Equal(MoneyDocumentOutcome.Ok, (await h.Spend.PostAsync(draft.DocumentId, ct)).Outcome);

        LedgerPosting posting = h.Ledger.Last;

        // Six legs: a payable and a bank leg per bill, keyed on the line so each
        // pair is replaceable and traceable on its own.
        Assert.Equal(6, posting.Legs.Count);
        Assert.Equal(3, posting.Legs.Select(l => l.TransactionDetailId).Distinct().Count());
        Assert.Equal(6_000m, posting.Legs.Sum(l => l.DebitAmount));
        Assert.Equal(6_000m, posting.Legs.Sum(l => l.CreditAmount));
    }

    /// <summary>
    /// A payment about exactly one document still says so on its header — the
    /// mapping is derived from the lines, so one line means one header mapping
    /// without the caller having to assert it.
    /// </summary>
    [SkippableFact]
    public async Task A_payment_about_one_document_carries_it_on_the_header()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        MoneyDocumentResult draft = await h.Spend.CreateAsync(new SaveMoneyDocumentRequest
        {
            TransactionDate = new DateOnly(2026, 8, 1),
            BankAccountId = h.BankAccountId,
            ContactId = 7,
            Amount = 4_000m,

            // Deliberately wrong, and deliberately ignored: the header is a
            // summary of the lines, not a fact the caller supplies beside them.
            MappingTransactionTypeCode = "INV",
            MappingTransactionId = 999,
            Lines = [Settles(BillPayment, "BIL", 777, 4_000m)],
        }, ct);

        MoneyDocumentView? saved = await h.Spend.GetAsync(draft.DocumentId, ct);

        Assert.Equal("BIL", saved!.MappingTransactionTypeCode);
        Assert.Equal(777, saved.MappingTransactionId);
    }

    /// <summary>
    /// A bill payment cannot name an invoice. The kind of document follows from
    /// what the line is for, and a payment traced to a document it did not pay is
    /// worse than one traced to nothing: a statement reconciling on that link
    /// reads as answered.
    /// </summary>
    [SkippableFact]
    public async Task A_line_cannot_settle_the_wrong_kind_of_document()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        MoneyDocumentResult draft = await h.Spend.CreateAsync(new SaveMoneyDocumentRequest
        {
            TransactionDate = new DateOnly(2026, 8, 1),
            BankAccountId = h.BankAccountId,
            ContactId = 7,
            Amount = 1_000m,
            Lines = [Settles(BillPayment, "INV", 900, 1_000m)],
        }, CancellationToken.None);

        Assert.Equal(MoneyDocumentOutcome.MappingNotSettleable, draft.Outcome);
    }

    /// <summary>
    /// An advance settles nothing — that is what makes it an advance — so it
    /// cannot claim a document.
    /// </summary>
    [SkippableFact]
    public async Task An_advance_cannot_claim_a_document()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        MoneyDocumentResult draft = await h.Spend.CreateAsync(new SaveMoneyDocumentRequest
        {
            TransactionDate = new DateOnly(2026, 8, 1),
            BankAccountId = h.BankAccountId,
            ContactId = 7,
            Amount = 1_000m,
            Lines = [Settles(VendorPrepayment, "BIL", 500, 1_000m)],
        }, CancellationToken.None);

        Assert.Equal(MoneyDocumentOutcome.MappingNotSettleable, draft.Outcome);
    }

    /// <summary>
    /// Two lines cannot settle the same bill. Splitting a payment across two
    /// lines of one document is not partial payment — partial payment is one line
    /// for less than the document — it is a keying slip, and it leaves two ledger
    /// rows reconciling against one balance.
    /// </summary>
    [SkippableFact]
    public async Task Two_lines_cannot_settle_the_same_document()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        MoneyDocumentResult draft = await h.Spend.CreateAsync(new SaveMoneyDocumentRequest
        {
            TransactionDate = new DateOnly(2026, 8, 1),
            BankAccountId = h.BankAccountId,
            ContactId = 7,
            Amount = 3_000m,
            Lines =
            [
                Settles(BillPayment, "BIL", 500, 1_000m),
                Settles(BillPayment, "BIL", 500, 2_000m),
            ],
        }, CancellationToken.None);

        Assert.Equal(MoneyDocumentOutcome.MappingRepeated, draft.Outcome);
    }

    /// <summary>
    /// The excess on an overpayment lands on the <i>other</i> control account, as
    /// an advance — never as a debit sitting on the trade payable.
    ///
    /// That distinction is the whole of "rather than a negative balance": left on
    /// the trade balance the excess turns the supplier's payable into a debit,
    /// which every aging report reads as "they owe us". They do — but not as a
    /// trade receivable, and not on a line that nets against the next bill.
    /// </summary>
    [SkippableFact]
    public async Task A_partial_payment_and_its_excess_land_on_different_accounts()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        MoneyDocumentResult draft = await h.Spend.CreateAsync(new SaveMoneyDocumentRequest
        {
            TransactionDate = new DateOnly(2026, 8, 1),
            BankAccountId = h.BankAccountId,
            ContactId = 7,
            Amount = 12_000m,
            Lines =
            [
                // Part of one bill, all of another, and money over.
                Settles(BillPayment, "BIL", 501, 4_000m),
                Settles(BillPayment, "BIL", 502, 6_000m),
                new SaveMoneyLineRequest { LedgerSourceId = MoneyPostingMap.Overpayment(spending: true), Amount = 2_000m },
            ],
        }, ct);

        Assert.Equal(MoneyDocumentOutcome.Ok, draft.Outcome);
        await h.Spend.PostAsync(draft.DocumentId, ct);

        List<LedgerPostingLeg> control =
            [.. h.Ledger.Last.Legs.Where(l => l.AccountSystemName is not null)];

        // ₹10,000 against the trade payable, ₹2,000 held as an overpayment
        // advance under receivables. Not ₹12,000 on one account.
        Assert.Equal(
            10_000m,
            control.Where(l => l.AccountSystemName == "Accounts Payable").Sum(l => l.DebitAmount));

        LedgerPostingLeg excess = Assert.Single(
            control, l => l.AccountSystemName == "Accounts Receivable");

        Assert.Equal(2_000m, excess.DebitAmount);
        Assert.Equal(MoneyPostingMap.OverpaymentAdvance, excess.SubAccountPurpose);
    }

    /// <summary>
    /// An overpayment used to name the bill it ran past, but now it acts like a
    /// general advance, so naming a document is refused.
    /// </summary>
    [SkippableFact]
    public async Task An_overpayment_cannot_name_a_document()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        MoneyDocumentResult draft = await h.Receive.CreateAsync(new SaveMoneyDocumentRequest
        {
            TransactionDate = new DateOnly(2026, 8, 1),
            BankAccountId = h.BankAccountId,
            ContactId = 7,
            Amount = 11_000m,
            Lines =
            [
                Settles(InvoicePayment, "INV", 900, 10_000m),
                Settles(CustomerOverpayment, "INV", 900, 1_000m),
            ],
        }, CancellationToken.None);

        Assert.Equal(MoneyDocumentOutcome.MappingNotSettleable, draft.Outcome);
    }

    private static SaveMoneyLineRequest Settles(
        int ledgerSourceId, string typeCode, long transactionId, decimal amount) => new()
        {
            LedgerSourceId = ledgerSourceId,
            MappingTransactionTypeCode = typeCode,
            MappingTransactionId = transactionId,
            Amount = amount,
        };

    /// <summary>
    /// T6.5, and the case it exists for. A USD 1,000 bill booked when the rate
    /// said ₹80, settled when it says ₹100.
    ///
    /// The payable comes off at ₹80,000 — what it was booked at — the bank moves
    /// ₹100,000, and the ₹20,000 between them is a realized loss. Relieve it at
    /// today's rate instead and the ledger balances perfectly while the contact
    /// keeps a ₹20,000 residue against a bill they have paid in full, which is
    /// exactly the kind of wrong that nobody goes looking for.
    /// </summary>
    [SkippableFact]
    public async Task Paying_a_bill_at_a_worse_rate_posts_a_realized_loss()
    {
        var ledger = new RecordingLedger();
        ledger.SettlementRates[("BIL", 500)] = new SettlementRate("USD", Rate(80m));

        await using Harness h = await Harness.CreateAsync(_postgres, ledger);
        CancellationToken ct = CancellationToken.None;

        MoneyDocumentResult draft = await h.Spend.CreateAsync(
            h.Foreign(1_000m, BillPayment, Rate(100m), "BIL", 500), ct);

        Assert.Equal(
            MoneyDocumentOutcome.Ok, (await h.Spend.PostAsync(draft.DocumentId, ct)).Outcome);

        LedgerPosting posting = h.Ledger.Last;
        Assert.Equal(3, posting.Legs.Count);

        // The payable comes off at the rate it went on at, not today's.
        LedgerPostingLeg payable =
            Assert.Single(posting.Legs, l => l.AccountSystemName == "Accounts Payable");

        Assert.Equal(1_000m, payable.DebitAmount);
        Assert.Equal(Rate(80m), payable.ExchangeRate);
        Assert.Null(payable.CurrencyCode);

        // The bank moves at the payment's rate — no override, so the document's.
        LedgerPostingLeg bank = Assert.Single(posting.Legs, l => l.AccountId == h.LedgerAccountId);
        Assert.Equal(1_000m, bank.CreditAmount);
        Assert.Null(bank.ExchangeRate);

        // ₹100,000 left the bank against an ₹80,000 liability: a ₹20,000 loss,
        // denominated in base because it never existed in dollars.
        LedgerPostingLeg fx = Assert.Single(
            posting.Legs, l => l.AccountSystemName == "Realized FX Gain/Loss");

        Assert.Equal(MoneyPosting.FxLedgerType, fx.LedgerTypeId);
        Assert.Equal(20_000m, fx.DebitAmount);
        Assert.Equal(0m, fx.CreditAmount);
        Assert.Equal("INR", fx.CurrencyCode);
        Assert.Equal(1m, fx.ExchangeRate);

        // Provenance stays: the difference came out of a bill payment.
        Assert.Equal(BillPayment, fx.LedgerSourceId);
    }

    /// <summary>
    /// The mirror, and the sign is the part worth proving. A USD invoice raised
    /// at ₹80 and collected at ₹100 brings in ₹20,000 more than the receivable was
    /// carrying — a gain, credited.
    ///
    /// The leg is built by the same call as the loss above, with the two base
    /// amounts swapped; it takes whichever side the pair is short on rather than
    /// asserting a sign per direction, which is why one of these cannot be right
    /// while the other is inverted.
    /// </summary>
    [SkippableFact]
    public async Task Collecting_an_invoice_at_a_better_rate_posts_a_realized_gain()
    {
        var ledger = new RecordingLedger();
        ledger.SettlementRates[("INV", 900)] = new SettlementRate("USD", Rate(80m));

        await using Harness h = await Harness.CreateAsync(_postgres, ledger);
        CancellationToken ct = CancellationToken.None;

        MoneyDocumentResult draft = await h.Receive.CreateAsync(
            h.Foreign(1_000m, InvoicePayment, Rate(100m), "INV", 900), ct);

        Assert.Equal(
            MoneyDocumentOutcome.Ok, (await h.Receive.PostAsync(draft.DocumentId, ct)).Outcome);

        LedgerPostingLeg fx = Assert.Single(
            h.Ledger.Last.Legs, l => l.AccountSystemName == "Realized FX Gain/Loss");

        Assert.Equal(0m, fx.DebitAmount);
        Assert.Equal(20_000m, fx.CreditAmount);
        Assert.Equal("INR", fx.CurrencyCode);
    }

    /// <summary>
    /// Same currency, same rate, no FX leg — and a document in the branch's own
    /// currency never asks about a rate at all.
    /// </summary>
    [SkippableFact]
    public async Task Settling_at_the_rate_it_was_booked_at_posts_no_exchange_difference()
    {
        var ledger = new RecordingLedger();
        ledger.SettlementRates[("BIL", 500)] = new SettlementRate("USD", Rate(80m));

        await using Harness h = await Harness.CreateAsync(_postgres, ledger);
        CancellationToken ct = CancellationToken.None;

        MoneyDocumentResult draft = await h.Spend.CreateAsync(
            h.Foreign(1_000m, BillPayment, Rate(80m), "BIL", 500), ct);

        await h.Spend.PostAsync(draft.DocumentId, ct);

        Assert.Equal(2, h.Ledger.Last.Legs.Count);
        Assert.DoesNotContain(
            h.Ledger.Last.Legs, l => l.AccountSystemName == "Realized FX Gain/Loss");

        // And nothing overrode the control leg's rate, because there was nothing
        // to override it with.
        Assert.All(h.Ledger.Last.Legs, l => Assert.Null(l.ExchangeRate));
    }

    /// <summary>
    /// A line naming a document with nothing posted against it is not an error.
    /// There is no earlier rate to differ from, so there is no difference — and
    /// a payment may legitimately name a document this branch has yet to post.
    /// </summary>
    [SkippableFact]
    public async Task A_settled_document_with_no_postings_yet_is_not_a_refusal()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        MoneyDocumentResult draft = await h.Spend.CreateAsync(
            h.Foreign(1_000m, BillPayment, Rate(100m), "BIL", 4242), ct);

        Assert.Equal(
            MoneyDocumentOutcome.Ok, (await h.Spend.PostAsync(draft.DocumentId, ct)).Outcome);

        Assert.Equal(2, h.Ledger.Last.Legs.Count);
    }

    /// <summary>
    /// An unreadable rate refuses the document as transient, the same way an
    /// unreadable period lock does. Posting the rest of it without the adjustment
    /// is precisely the silent residue the whole mechanism exists to prevent, so
    /// "could not ask" must not look like "no difference".
    /// </summary>
    [SkippableFact]
    public async Task An_unreadable_settlement_rate_refuses_the_document()
    {
        var ledger = new RecordingLedger { SettlementRateUnavailable = true };

        await using Harness h = await Harness.CreateAsync(_postgres, ledger);
        CancellationToken ct = CancellationToken.None;

        MoneyDocumentResult draft = await h.Spend.CreateAsync(
            h.Foreign(1_000m, BillPayment, Rate(100m), "BIL", 500), ct);

        Assert.Equal(
            MoneyDocumentOutcome.SettlementRateUnavailable,
            (await h.Spend.PostAsync(draft.DocumentId, ct)).Outcome);

        MoneyDocumentView? after = await h.Spend.GetAsync(draft.DocumentId, ct);
        Assert.Equal("Draft", after!.Status);
        Assert.Null(after.TransactionNo);
    }

    /// <summary>
    /// Paying a dollar bill with a euro payment is refused rather than converted.
    /// Two conversions in one settlement is a cross-rate, and inventing one would
    /// put a figure in the books that no recorded rate produces.
    /// </summary>
    [SkippableFact]
    public async Task Settling_a_document_raised_in_another_currency_is_refused()
    {
        var ledger = new RecordingLedger();
        ledger.SettlementRates[("BIL", 500)] = new SettlementRate("USD", Rate(80m));

        await using Harness h = await Harness.CreateAsync(_postgres, ledger);
        CancellationToken ct = CancellationToken.None;

        SaveMoneyDocumentRequest request = h.Foreign(1_000m, BillPayment, Rate(50m), "BIL", 500);
        request.CurrencyCode = "EUR";

        MoneyDocumentResult draft = await h.Spend.CreateAsync(request, ct);

        Assert.Equal(
            MoneyDocumentOutcome.SettlementCurrencyMismatch,
            (await h.Spend.PostAsync(draft.DocumentId, ct)).Outcome);
    }

    /// <summary>
    /// The ledger's rate is units of the transaction currency per unit of base,
    /// so a rate quoted the way people say it — ₹80 to the dollar — is its
    /// reciprocal. Written once here rather than inline, because getting it the
    /// wrong way up in a test would prove the opposite of what the test claims.
    ///
    /// The two rates these tests use are ₹80 and ₹100, which are the reciprocals
    /// that terminate in decimal. A rate that does not — ₹83 — leaves a base
    /// amount a hundredth out, and a test failing on the rounding of its own
    /// fixture teaches nothing about the accounting.
    /// </summary>
    private static decimal Rate(decimal rupeesPerUnit) => 1m / rupeesPerUnit;

    private sealed class Harness : IAsyncDisposable
    {
        public required AccountingDbContext Db { get; init; }

        public required RecordingLedger Ledger { get; init; }

        public required SpendMoneyService Spend { get; init; }

        public required ReceiveMoneyService Receive { get; init; }

        public required TransferMoneyService Transfer { get; init; }

        public required long BankAccountId { get; init; }

        public required long CashAccountId { get; init; }

        public required long LedgerAccountId { get; init; }

        public required long CashLedgerAccountId { get; init; }

        /// <summary>A one-line document for the cases where the lines are not the point.</summary>
        public SaveMoneyDocumentRequest Payment(decimal amount, int ledgerSourceId) => new()
        {
            TransactionDate = new DateOnly(2026, 8, 1),
            BankAccountId = BankAccountId,
            ContactId = 7,
            Amount = amount,
            Lines = [new SaveMoneyLineRequest { LedgerSourceId = ledgerSourceId, Amount = amount }],
        };

        /// <summary>
        /// The same, in a foreign currency and settling a named document — which
        /// is the pair of conditions an exchange difference needs to exist at all.
        /// </summary>
        public SaveMoneyDocumentRequest Foreign(
            decimal amount, int ledgerSourceId, decimal rate, string settlesType, long settlesId) =>
            new()
            {
                TransactionDate = new DateOnly(2026, 8, 1),
                BankAccountId = BankAccountId,
                ContactId = 7,
                Amount = amount,
                CurrencyCode = "USD",
                ExchangeRate = rate,
                MappingTransactionTypeCode = settlesType,
                MappingTransactionId = settlesId,
                Lines =
                [
                    new SaveMoneyLineRequest
                    {
                        LedgerSourceId = ledgerSourceId,
                        MappingTransactionTypeCode = settlesType,
                        MappingTransactionId = settlesId,
                        Amount = amount,
                    },
                ],
            };

        public static async Task<Harness> CreateAsync(
            PostgresFixture postgres, RecordingLedger? ledger = null)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var orgId = Guid.NewGuid();
            AccountingDbContext db = postgres.CreateContext(Guid.NewGuid(), orgId);
            RecordingLedger recorder = ledger ?? new RecordingLedger();

            var bank = new Bank
            {
                OrgId = orgId,
                BankCode = "HDFC",
                BankName = "HDFC Bank",
                DisplayOrder = 1,
                IsActive = true,
            };

            db.Banks.Add(bank);
            await db.SaveChangesAsync();

            async Task<long> Account(
                string name, string number, BankAccountType type, long? bankId,
                long ledgerAccountId, bool isDefault)
            {
                var account = new BankAccount
                {
                    OrgId = orgId,
                    BankId = bankId,
                    AccountName = name,
                    AccountNumber = number,
                    AccountType = type,
                    CurrencyCode = "INR",
                    LedgerAccountId = ledgerAccountId,
                    IsDefault = isDefault,
                    IsActive = true,
                };

                db.BankAccounts.Add(account);
                await db.SaveChangesAsync();
                return account.BankAccountId;
            }

            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
            await db.SaveChangesAsync();

            var numbers = new NumberGenerator(
                db, Options.Create(new NumberingOptions()), new StubFinancialYear());

            ICurrentUser user = new StubCurrentUser();
            var currency = new StubBaseCurrency();

            // The ledger account ids Accounting would have issued when these bank
            // accounts were created.
            const long bankLedgerAccount = 9001;
            const long cashLedgerAccount = 9002;

            return new Harness
            {
                Db = db,
                Ledger = recorder,
                LedgerAccountId = bankLedgerAccount,
                CashLedgerAccountId = cashLedgerAccount,
                BankAccountId = await Account(
                    "HDFC Current", "111", BankAccountType.Current, bank.BankId,
                    bankLedgerAccount, isDefault: true),
                CashAccountId = await Account(
                    "Cash in Hand", "CASH", BankAccountType.Cash, null,
                    cashLedgerAccount, isDefault: false),
                Spend = new SpendMoneyService(db, recorder, numbers, currency, user, TimeProvider.System),
                Receive = new ReceiveMoneyService(db, recorder, numbers, currency, user, TimeProvider.System),
                Transfer = new TransferMoneyService(db, recorder, numbers, currency, user, TimeProvider.System),
            };
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
