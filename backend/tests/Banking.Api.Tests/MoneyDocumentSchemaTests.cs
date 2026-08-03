using Banking.Entity.Enums;
using Banking.Entity.TableEntities;
using Banking.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Banking.Api.Tests;

/// <summary>
/// The three money documents' schema, against a real database.
///
/// Nearly all of what T6.1 delivers is enforcement rather than code: a document
/// must add up before it posts, a draft holds no number, a transfer cannot go to
/// the account it came from. Each of those is a constraint or a trigger, and none
/// can be checked without Postgres.
///
/// <b>Three tables, so the shape of each is the test.</b> A payment has a payee
/// and no destination account; a transfer has a destination and no payee. Under
/// the discriminated single table those were check constraints policing nullable
/// columns; here they are simply the columns each table has, which is most of the
/// argument for the split.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class MoneyDocumentSchemaTests
{
    /// <summary>`mst.LedgerSources` — the two halves of an overpayment.</summary>
    private const int BillPayment = 2;

    private const int VendorOverpayment = 16;

    private readonly PostgresFixture _postgres;

    public MoneyDocumentSchemaTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>
    /// A draft need not add up. Someone allocating a payment across nine bills is
    /// short for eight of them, and a database that refused to save that would
    /// force the whole allocation to be keyed in one sitting.
    /// </summary>
    [SkippableFact]
    public async Task A_draft_payment_may_be_under_allocated()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        SpendMoney payment = await h.DraftPayment(11_000m);
        await h.AddLine(payment, 1, BillPayment, 10_000m, "BIL", 500);

        Assert.Equal(1, await h.Db.SpendMoneyDetails
            .CountAsync(d => d.SpendMoneyId == payment.SpendMoneyId));
    }

    /// <summary>
    /// Posting is where it has to add up, and the line trigger never fires for it
    /// — posting changes the header and leaves the lines alone — so a second
    /// trigger on the header covers it.
    /// </summary>
    [SkippableFact]
    public async Task An_under_allocated_payment_cannot_be_posted()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        SpendMoney payment = await h.DraftPayment(11_000m);
        await h.AddLine(payment, 1, BillPayment, 10_000m, "BIL", 500);

        DbUpdateException error =
            await Assert.ThrowsAsync<DbUpdateException>(() => h.PostPayment(payment));

        Assert.Contains("allocated", error.InnerException?.Message ?? string.Empty);
    }

    /// <summary>
    /// The case the line table exists for. Paying ₹11,000 against a ₹10,000 bill
    /// is a bill payment <b>and</b> a supplier deposit on one document, and the
    /// two halves carry different ledger sources — so a payables report reading
    /// bill payments still sees the ₹10,000 that was one.
    /// </summary>
    [SkippableFact]
    public async Task An_overpayment_posts_as_two_lines_with_different_sources()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        SpendMoney payment = await h.DraftPayment(11_000m);
        await h.AddLine(payment, 1, BillPayment, 10_000m, "BIL", 500);
        await h.AddLine(payment, 2, VendorOverpayment, 1_000m);
        await h.PostPayment(payment);

        List<SpendMoneyDetail> lines = await h.Db.SpendMoneyDetails
            .Where(d => d.SpendMoneyId == payment.SpendMoneyId)
            .OrderBy(d => d.LineNumber)
            .ToListAsync();

        Assert.Equal(2, lines.Count);

        // The settled part names the bill it cleared.
        Assert.Equal(BillPayment, lines[0].LedgerSourceId);
        Assert.Equal("BIL", lines[0].MappingTransactionTypeCode);
        Assert.Equal(500, lines[0].MappingTransactionId);

        // The excess settles nothing — that is exactly what a deposit is.
        Assert.Equal(VendorOverpayment, lines[1].LedgerSourceId);
        Assert.Null(lines[1].MappingTransactionTypeCode);
        Assert.Null(lines[1].MappingTransactionId);

        Assert.Equal(11_000m, lines.Sum(l => l.Amount));
    }

    [SkippableFact]
    public async Task A_posted_payment_cannot_be_knocked_out_of_allocation()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        SpendMoney payment = await h.DraftPayment(1_000m);
        SpendMoneyDetail line = await h.AddLine(payment, 1, BillPayment, 1_000m, "BIL", 500);
        await h.PostPayment(payment);

        line.Amount = 900m;
        line.AmountBase = 900m;

        await Assert.ThrowsAsync<DbUpdateException>(() => h.Db.SaveChangesAsync());
    }

    /// <summary>
    /// The number is taken at post. A draft holding one has consumed a number it
    /// may never use, from a series that has to run without gaps.
    /// </summary>
    [SkippableFact]
    public async Task A_draft_payment_holding_a_number_is_refused()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            h.DraftPayment(500m, number: "PAY/2627/00001"));
    }

    /// <summary>
    /// Half a mapping traces to nothing: an id with no type cannot be resolved,
    /// and a type with no id names every document at once.
    /// </summary>
    [SkippableFact]
    public async Task A_line_with_half_a_mapping_is_refused()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        SpendMoney payment = await h.DraftPayment(500m);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            h.AddLine(payment, 1, BillPayment, 500m, typeCode: null, mappingId: 500));
    }

    /// <summary>The receive side is the mirror, and carries the same rules.</summary>
    [SkippableFact]
    public async Task A_receipt_posts_when_it_adds_up()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        var receipt = new ReceiveMoney
        {
            OrgId = h.OrgId,
            TransactionDate = new DateOnly(2026, 8, 1),
            BankAccountId = h.BankAccountId,
            ContactId = 7,
            Amount = 2_500m,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
        };

        h.Db.ReceiveMoney.Add(receipt);
        await h.Db.SaveChangesAsync();

        h.Db.ReceiveMoneyDetails.Add(new ReceiveMoneyDetail
        {
            OrgId = h.OrgId,
            ReceiveMoneyId = receipt.ReceiveMoneyId,
            LineNumber = 1,
            LedgerSourceId = 3,
            MappingTransactionTypeCode = "INV",
            MappingTransactionId = 900,
            Amount = 2_500m,
            AmountBase = 2_500m,
        });

        await h.Db.SaveChangesAsync();

        receipt.Status = MoneyDocumentStatus.Posted;
        receipt.TransactionNo = "REC/2627/00001";
        receipt.PostedAt = DateTimeOffset.UtcNow;
        await h.Db.SaveChangesAsync();

        Assert.Equal(MoneyDocumentStatus.Posted, receipt.Status);
    }

    /// <summary>
    /// A transfer has no contact column at all — which under the discriminated
    /// single table needed a check constraint, and here is simply the shape.
    /// </summary>
    [SkippableFact]
    public async Task A_transfer_moves_between_two_of_the_organizations_own_accounts()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        TransferMoney transfer = await h.DraftTransfer(500m, h.CashAccountId);

        Assert.Equal(h.BankAccountId, transfer.FromBankAccountId);
        Assert.Equal(h.CashAccountId, transfer.ToBankAccountId);
    }

    /// <summary>Moving money to the account it came from reconciles as nothing happening.</summary>
    [SkippableFact]
    public async Task A_transfer_to_the_same_account_is_refused()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            h.DraftTransfer(500m, h.BankAccountId));
    }

    [SkippableFact]
    public async Task A_draft_transfer_holding_a_number_is_refused()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            h.DraftTransfer(500m, h.CashAccountId, number: "TRF/2627/00001"));
    }

    /// <summary>
    /// A transfer allocates to nothing, so it posts with no lines at all — there
    /// is no detail table to add them to, and nothing waiting for them.
    /// </summary>
    [SkippableFact]
    public async Task A_transfer_posts_with_no_allocation_at_all()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        TransferMoney transfer = await h.DraftTransfer(500m, h.CashAccountId);

        transfer.Status = MoneyDocumentStatus.Posted;
        transfer.TransactionNo = "TRF/2627/00001";
        transfer.PostedAt = DateTimeOffset.UtcNow;
        await h.Db.SaveChangesAsync();

        Assert.Equal(MoneyDocumentStatus.Posted, transfer.Status);
    }

    [SkippableFact]
    public async Task A_payment_of_nothing_is_refused()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        await Assert.ThrowsAsync<DbUpdateException>(() => h.DraftPayment(0m));
    }

    private sealed class Harness : IAsyncDisposable
    {
        public required BankingDbContext Db { get; init; }

        public required Guid OrgId { get; init; }

        public required long BankAccountId { get; init; }

        public required long CashAccountId { get; init; }

        public async Task<SpendMoney> DraftPayment(decimal amount, string? number = null)
        {
            var payment = new SpendMoney
            {
                OrgId = OrgId,
                TransactionNo = number,
                TransactionDate = new DateOnly(2026, 8, 1),
                BankAccountId = BankAccountId,
                ContactId = 7,
                Amount = amount,
                CurrencyCode = "INR",
                ExchangeRate = 1m,
                PaymentMethod = PaymentMethod.BankTransfer,
                Status = MoneyDocumentStatus.Draft,
            };

            Db.SpendMoney.Add(payment);
            await Db.SaveChangesAsync();
            return payment;
        }

        public async Task<TransferMoney> DraftTransfer(
            decimal amount, long toBankAccountId, string? number = null)
        {
            var transfer = new TransferMoney
            {
                OrgId = OrgId,
                TransactionNo = number,
                TransactionDate = new DateOnly(2026, 8, 1),
                FromBankAccountId = BankAccountId,
                ToBankAccountId = toBankAccountId,
                Amount = amount,
                CurrencyCode = "INR",
                ExchangeRate = 1m,
                PaymentMethod = PaymentMethod.BankTransfer,
                Status = MoneyDocumentStatus.Draft,
            };

            Db.TransferMoney.Add(transfer);
            await Db.SaveChangesAsync();
            return transfer;
        }

        public async Task<SpendMoneyDetail> AddLine(
            SpendMoney payment,
            int lineNumber,
            int ledgerSourceId,
            decimal amount,
            string? typeCode = null,
            long? mappingId = null)
        {
            var line = new SpendMoneyDetail
            {
                OrgId = OrgId,
                SpendMoneyId = payment.SpendMoneyId,
                LineNumber = lineNumber,
                LedgerSourceId = ledgerSourceId,
                MappingTransactionTypeCode = typeCode,
                MappingTransactionId = mappingId,
                Amount = amount,
                AmountBase = amount,
            };

            Db.SpendMoneyDetails.Add(line);
            await Db.SaveChangesAsync();
            return line;
        }

        public async Task PostPayment(SpendMoney payment)
        {
            payment.Status = MoneyDocumentStatus.Posted;
            payment.TransactionNo = $"PAY/2627/{payment.SpendMoneyId:D5}";
            payment.PostedAt = DateTimeOffset.UtcNow;
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
