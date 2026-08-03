using Banking.Entity.Enums;
using Banking.Entity.TableEntities;
using Banking.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Banking.Api.Tests;

/// <summary>
/// The money document's schema, against a real database.
///
/// Nearly all of what T6.1 delivers is enforcement rather than code: a document
/// must add up before it posts, a transfer has no counterparty, a payment has no
/// destination account, a draft holds no number. Each of those is a constraint or
/// a trigger, and none of them can be checked without Postgres.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class MoneyTransactionSchemaTests
{
    /// <summary>`mst.LedgerSources` — the two halves of an overpayment.</summary>
    private const int BillPayment = 2;

    private const int VendorOverpayment = 16;

    private const int MoneyTransfer = 11;

    private readonly PostgresFixture _postgres;

    public MoneyTransactionSchemaTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>
    /// A draft need not add up. Someone allocating a payment across nine bills is
    /// short for eight of them, and a database that refused to save that would
    /// force the whole allocation to be keyed in one sitting.
    /// </summary>
    [SkippableFact]
    public async Task A_draft_may_be_under_allocated()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        MoneyTransaction payment = await h.Draft("SPM", 11_000m, contactId: 7);
        await h.AddLine(payment, 1, BillPayment, 10_000m, "BIL", 500);

        Assert.Equal(1, await h.Db.MoneyTransactionDetails
            .CountAsync(d => d.MoneyTransactionId == payment.MoneyTransactionId));
    }

    /// <summary>
    /// Posting is where it has to add up. This is the path that matters most, and
    /// the line trigger never fires for it — posting changes the header and
    /// leaves the lines alone — so a second trigger on the header covers it.
    /// </summary>
    [SkippableFact]
    public async Task An_under_allocated_document_cannot_be_posted()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        MoneyTransaction payment = await h.Draft("SPM", 11_000m, contactId: 7);
        await h.AddLine(payment, 1, BillPayment, 10_000m, "BIL", 500);

        DbUpdateException error = await Assert.ThrowsAsync<DbUpdateException>(() => h.Post(payment));

        Assert.Contains("allocated", error.InnerException?.Message ?? string.Empty);
    }

    /// <summary>
    /// The case the whole design exists for. Paying ₹11,000 against a ₹10,000
    /// bill is a bill payment <b>and</b> a supplier deposit on one document, and
    /// the two halves carry different ledger sources — so a payables report
    /// reading bill payments still sees the ₹10,000 that was one.
    /// </summary>
    [SkippableFact]
    public async Task An_overpayment_posts_as_two_lines_with_different_sources()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        MoneyTransaction payment = await h.Draft("SPM", 11_000m, contactId: 7);
        await h.AddLine(payment, 1, BillPayment, 10_000m, "BIL", 500);
        await h.AddLine(payment, 2, VendorOverpayment, 1_000m);
        await h.Post(payment);

        List<MoneyTransactionDetail> lines = await h.Db.MoneyTransactionDetails
            .Where(d => d.MoneyTransactionId == payment.MoneyTransactionId)
            .OrderBy(d => d.LineNumber)
            .ToListAsync();

        Assert.Equal(2, lines.Count);

        // The settled part names the bill it cleared.
        Assert.Equal(BillPayment, lines[0].LedgerSourceId);
        Assert.Equal("BIL", lines[0].MappingTransactionTypeCode);
        Assert.Equal(500, lines[0].MappingTransactionId);

        // The excess settles nothing — that is exactly what an advance is.
        Assert.Equal(VendorOverpayment, lines[1].LedgerSourceId);
        Assert.Null(lines[1].MappingTransactionTypeCode);
        Assert.Null(lines[1].MappingTransactionId);

        Assert.Equal(11_000m, lines.Sum(l => l.Amount));
    }

    [SkippableFact]
    public async Task A_posted_document_cannot_be_knocked_out_of_allocation()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        MoneyTransaction payment = await h.Draft("SPM", 1_000m, contactId: 7);
        MoneyTransactionDetail line = await h.AddLine(payment, 1, BillPayment, 1_000m, "BIL", 500);
        await h.Post(payment);

        line.Amount = 900m;
        line.AmountBase = 900m;

        await Assert.ThrowsAsync<DbUpdateException>(() => h.Db.SaveChangesAsync());
    }

    /// <summary>
    /// A transfer is the one money document with no counterparty: both sides are
    /// the organization's own accounts.
    /// </summary>
    [SkippableFact]
    public async Task A_transfer_has_a_destination_and_no_contact()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        MoneyTransaction transfer = await h.Draft(
            "TRM", 500m, contactId: null, toBankAccountId: h.CashAccountId);

        Assert.Null(transfer.ContactId);
        Assert.Equal(h.CashAccountId, transfer.ToBankAccountId);
    }

    [SkippableFact]
    public async Task A_transfer_naming_a_contact_is_refused()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            h.Draft("TRM", 500m, contactId: 7, toBankAccountId: h.CashAccountId));
    }

    /// <summary>Moving money to the account it came from reconciles as nothing happening.</summary>
    [SkippableFact]
    public async Task A_transfer_to_the_same_account_is_refused()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            h.Draft("TRM", 500m, contactId: null, toBankAccountId: h.BankAccountId));
    }

    [SkippableFact]
    public async Task A_payment_with_a_destination_account_is_refused()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            h.Draft("SPM", 500m, contactId: 7, toBankAccountId: h.CashAccountId));
    }

    /// <summary>
    /// The number is taken at post. A draft holding one has consumed a number it
    /// may never use, from a series that has to run without gaps.
    /// </summary>
    [SkippableFact]
    public async Task A_draft_holding_a_number_is_refused()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            h.Draft("RCM", 500m, contactId: 7, number: "REC/2627/00001"));
    }

    /// <summary>
    /// Half a mapping traces to nothing: an id with no type cannot be resolved,
    /// and a type with no id names every document at once.
    /// </summary>
    [SkippableFact]
    public async Task A_line_with_half_a_mapping_is_refused()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        MoneyTransaction payment = await h.Draft("SPM", 500m, contactId: 7);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            h.AddLine(payment, 1, BillPayment, 500m, typeCode: null, mappingId: 500));
    }

    /// <summary>This table is for three types. A fourth would have no posting path behind it.</summary>
    [SkippableFact]
    public async Task A_type_this_table_does_not_own_is_refused()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        await Assert.ThrowsAsync<DbUpdateException>(() => h.Draft("INV", 500m, contactId: 7));
    }

    [SkippableFact]
    public async Task A_transfer_allocates_to_nothing_and_still_posts()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        MoneyTransaction transfer = await h.Draft(
            "TRM", 500m, contactId: null, toBankAccountId: h.CashAccountId);

        await h.AddLine(transfer, 1, MoneyTransfer, 500m);
        await h.Post(transfer);

        Assert.Equal(MoneyTransactionStatus.Posted, transfer.Status);
    }

    private sealed class Harness : IAsyncDisposable
    {
        public required BankingDbContext Db { get; init; }

        public required long BankAccountId { get; init; }

        public required long CashAccountId { get; init; }

        private Guid OrgId { get; init; }

        public async Task<MoneyTransaction> Draft(
            string typeCode,
            decimal amount,
            long? contactId,
            long? toBankAccountId = null,
            string? number = null)
        {
            var transaction = new MoneyTransaction
            {
                OrgId = OrgId,
                TransactionTypeCode = typeCode,
                TransactionNo = number,
                TransactionDate = new DateOnly(2026, 8, 1),
                BankAccountId = BankAccountId,
                ToBankAccountId = toBankAccountId,
                ContactId = contactId,
                Amount = amount,
                CurrencyCode = "INR",
                ExchangeRate = 1m,
                PaymentMethod = PaymentMethod.BankTransfer,
                Status = MoneyTransactionStatus.Draft,
            };

            Db.MoneyTransactions.Add(transaction);
            await Db.SaveChangesAsync();
            return transaction;
        }

        public async Task<MoneyTransactionDetail> AddLine(
            MoneyTransaction transaction,
            int lineNumber,
            int ledgerSourceId,
            decimal amount,
            string? typeCode = null,
            long? mappingId = null)
        {
            var line = new MoneyTransactionDetail
            {
                OrgId = OrgId,
                MoneyTransactionId = transaction.MoneyTransactionId,
                LineNumber = lineNumber,
                LedgerSourceId = ledgerSourceId,
                MappingTransactionTypeCode = typeCode,
                MappingTransactionId = mappingId,
                Amount = amount,
                AmountBase = amount,
            };

            Db.MoneyTransactionDetails.Add(line);
            await Db.SaveChangesAsync();
            return line;
        }

        public async Task Post(MoneyTransaction transaction)
        {
            transaction.Status = MoneyTransactionStatus.Posted;
            transaction.TransactionNo = $"PAY/2627/{transaction.MoneyTransactionId:D5}";
            transaction.PostedAt = DateTimeOffset.UtcNow;
            await Db.SaveChangesAsync();
        }

        public static async Task<Harness> CreateAsync(PostgresFixture postgres)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var orgId = Guid.NewGuid();
            BankingDbContext db = postgres.CreateContext(Guid.NewGuid(), orgId);

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
                string name, string number, BankAccountType type, long? bankId, bool isDefault)
            {
                var account = new BankAccount
                {
                    OrgId = orgId,
                    BankId = bankId,
                    AccountName = name,
                    AccountNumber = number,
                    AccountType = type,
                    CurrencyCode = "INR",
                    IsDefault = isDefault,
                    IsActive = true,
                };

                db.BankAccounts.Add(account);
                await db.SaveChangesAsync();
                return account.BankAccountId;
            }

            return new Harness
            {
                Db = db,
                OrgId = orgId,
                BankAccountId = await Account(
                    "HDFC Current", "111", BankAccountType.Current, bank.BankId, isDefault: true),

                // Cash has no institution, which is why BankId is nullable.
                CashAccountId = await Account(
                    "Cash in Hand", "CASH", BankAccountType.Cash, null, isDefault: false),
            };
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
