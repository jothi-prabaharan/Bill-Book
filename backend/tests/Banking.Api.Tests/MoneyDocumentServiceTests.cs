using Banking.Api.Services;
using Banking.Entity.Enums;
using Banking.Entity.Models;
using Banking.Entity.TableEntities;
using Banking.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;
using Xunit;

namespace Banking.Api.Tests;

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
        // them from.
        Assert.Empty(h.Ledger.Last.Legs);
        Assert.Equal([MoneyPostingMap.ControlLedgerType], h.Ledger.Last.WithdrawLedgerTypeIds);
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

    private sealed class Harness : IAsyncDisposable
    {
        public required BankingDbContext Db { get; init; }

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

        public static async Task<Harness> CreateAsync(
            PostgresFixture postgres, RecordingLedger? ledger = null)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var orgId = Guid.NewGuid();
            BankingDbContext db = postgres.CreateContext(Guid.NewGuid(), orgId);
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

public sealed class StubBaseCurrency : Shared.Kernel.Tenancy.IBaseCurrencyProvider
{
    public Task<string?> GetBaseCurrencyAsync(CancellationToken ct = default) =>
        Task.FromResult<string?>("INR");
}

public sealed class StubFinancialYear : IFinancialYearProvider
{
    public Task<int> GetStartMonthAsync(CancellationToken ct = default) => Task.FromResult(4);
}

public sealed class StubCurrentUser : ICurrentUser
{
    public Guid? UserId { get; } = Guid.NewGuid();

    public Guid? CustomerId => null;

    public Guid? OrgId => null;

    public int? RoleId => null;
}
