using Accounting.Api.Services;
using Accounting.Api.Services.Payments;
using Accounting.Entity.Enums;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// Paying online from the client portal (TK-98, sandbox gateway per D-25): the
/// payment starts only for the contact's own invoices and no more than is owed,
/// the money is recorded only on a verified callback, and however many
/// callbacks arrive, one receipt is made, allocated to the invoices chosen.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class OnlinePaymentTests
{
    private const long Contact = 7;
    private const long Stranger = 8;
    private const long Invoice = 5001;

    private readonly PostgresFixture _postgres;

    public OnlinePaymentTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public void A_callback_with_a_bad_signature_or_the_wrong_shape_is_not_read()
    {
        var sandbox = Sandbox();
        string good = sandbox.Callback("op_ref", "sbx_order_1", "sbx_pay_1", succeeded: true, 100m);

        Assert.NotNull(sandbox.ReadCallback(good));
        Assert.Null(sandbox.ReadCallback(good.Replace("100.00", "1000.00").Replace("\"Amount\":100", "\"Amount\":1000")));
        Assert.Null(new SandboxPaymentGateway(Config("another-secret-entirely-long")).ReadCallback(good));
        Assert.Null(sandbox.ReadCallback("not json"));
        Assert.Null(sandbox.ReadCallback("{}"));
    }

    [Fact]
    public void A_reference_names_its_customer_and_branch()
    {
        Guid customer = Guid.NewGuid();
        Guid org = Guid.NewGuid();

        Assert.True(OnlinePaymentService.TryReadReference(OnlinePaymentService.Reference(customer, org, 42), out Guid c, out Guid o));
        Assert.Equal((customer, org), (c, o));
        Assert.False(OnlinePaymentService.TryReadReference("op_pending_abc", out _, out _));
        Assert.False(OnlinePaymentService.TryReadReference(null, out _, out _));
    }

    [SkippableFact]
    public async Task Another_contacts_invoice_or_more_than_is_owed_is_refused()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        Assert.Equal(StartPaymentOutcome.Invalid, (await h.StartAsync(Stranger, 100m)).Outcome);
        Assert.Equal(StartPaymentOutcome.Invalid, (await h.StartAsync(Contact, 1_180.01m)).Outcome);
        Assert.Empty(await h.Db.OnlinePayments.ToListAsync());
    }

    [SkippableFact]
    public async Task Without_an_online_payment_account_or_a_gateway_nothing_starts()
    {
        await using Harness h = await Harness.CreateAsync(_postgres, onlineAccount: false);
        Assert.Equal(StartPaymentOutcome.NotSetUp, (await h.StartAsync(Contact, 100m)).Outcome);

        await using Harness off = await Harness.CreateAsync(_postgres, gateway: new UnconfiguredPaymentGateway());
        Assert.Equal(StartPaymentOutcome.GatewayUnavailable, (await off.StartAsync(Contact, 100m)).Outcome);
    }

    [SkippableFact]
    public async Task A_replayed_callback_creates_one_receipt_that_settles_the_chosen_invoice()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        StartPaymentResult started = await h.StartAsync(Contact, 500m, extra: 50m);
        Assert.Equal(StartPaymentOutcome.Ok, started.Outcome);
        Assert.Equal($"/pay/sandbox/{started.OnlinePaymentId}", started.CheckoutUrl);

        OnlinePayment payment = await h.Db.OnlinePayments.AsNoTracking().SingleAsync();
        Assert.StartsWith("op_", payment.Reference);
        Assert.Equal(550m, payment.Amount);

        GatewayCallback callback = h.Sandbox.ReadCallback(
            h.Sandbox.Callback(payment.Reference, payment.GatewayOrderId!, "sbx_pay_42", succeeded: true, 550m))!;

        Assert.Equal(CompletePaymentOutcome.Ok, await h.Payments.CompleteAsync(callback, default));
        Assert.Equal(CompletePaymentOutcome.AlreadyRecorded, await h.Payments.CompleteAsync(callback, default));

        ReceiveMoney receipt = await h.Db.ReceiveMoney.AsNoTracking().SingleAsync();
        Assert.Equal(550m, receipt.Amount);
        Assert.Equal(Contact, receipt.ContactId);
        Assert.Equal(MoneyDocumentStatus.Posted, receipt.Status);

        List<ReceiveMoneyDetail> lines = await h.Db.ReceiveMoneyDetails.AsNoTracking().OrderBy(l => l.LineNumber).ToListAsync();
        Assert.Equal(2, lines.Count);
        Assert.Equal(("INV", (long?)Invoice, 500m, 3), (lines[0].MappingTransactionTypeCode, lines[0].MappingTransactionId, lines[0].Amount, lines[0].LedgerSourceId));
        Assert.Equal((50m, 9), (lines[1].Amount, lines[1].LedgerSourceId));

        OnlinePayment paid = await h.Db.OnlinePayments.AsNoTracking().SingleAsync();
        Assert.Equal(OnlinePaymentStatus.Paid, paid.Status);
        Assert.Equal("sbx_pay_42", paid.GatewayPaymentId);
        Assert.Equal(receipt.ReceiveMoneyId, paid.ReceiveMoneyId);
    }

    [SkippableFact]
    public async Task A_failed_payment_records_no_receipt()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        StartPaymentResult started = await h.StartAsync(Contact, 500m);

        Assert.Equal(CompletePaymentOutcome.Ok,
            await h.Payments.SandboxCheckoutAsync(Contact, started.OnlinePaymentId, succeed: false, h.Sandbox, default));

        Assert.Equal(OnlinePaymentStatus.Failed, (await h.Db.OnlinePayments.AsNoTracking().SingleAsync()).Status);
        Assert.Empty(await h.Db.ReceiveMoney.ToListAsync());
    }

    [SkippableFact]
    public async Task A_callback_for_another_amount_is_refused_and_records_nothing()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        await h.StartAsync(Contact, 500m);
        OnlinePayment payment = await h.Db.OnlinePayments.AsNoTracking().SingleAsync();

        GatewayCallback callback = h.Sandbox.ReadCallback(
            h.Sandbox.Callback(payment.Reference, payment.GatewayOrderId!, "sbx_pay_9", succeeded: true, 5m))!;

        Assert.Equal(CompletePaymentOutcome.Mismatch, await h.Payments.CompleteAsync(callback, default));
        Assert.Equal(OnlinePaymentStatus.Created, (await h.Db.OnlinePayments.AsNoTracking().SingleAsync()).Status);
    }

    /// <summary>
    /// The money was taken, so it is never lost: a receipt refused — here by a
    /// closed period — leaves the payment paid with a note for staff, once.
    /// </summary>
    [SkippableFact]
    public async Task A_payment_whose_receipt_is_refused_is_marked_paid_with_a_note_and_no_receipt()
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        await using Harness h = await Harness.CreateAsync(_postgres, ledger: new RecordingLedger(lockedUpto: today));
        StartPaymentResult started = await h.StartAsync(Contact, 500m);

        Assert.Equal(CompletePaymentOutcome.Ok,
            await h.Payments.SandboxCheckoutAsync(Contact, started.OnlinePaymentId, succeed: true, h.Sandbox, default));

        OnlinePayment paid = await h.Db.OnlinePayments.AsNoTracking().SingleAsync();
        Assert.Equal(OnlinePaymentStatus.Paid, paid.Status);
        Assert.Null(paid.ReceiveMoneyId);
        Assert.NotNull(paid.Note);
        Assert.Empty(await h.Db.ReceiveMoney.ToListAsync());
    }

    [SkippableFact]
    public async Task Another_contacts_payment_is_not_found()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        StartPaymentResult started = await h.StartAsync(Contact, 500m);

        Assert.Null(await h.Payments.GetAsync(Stranger, started.OnlinePaymentId, default));
        Assert.Equal(CompletePaymentOutcome.NotFound,
            await h.Payments.SandboxCheckoutAsync(Stranger, started.OnlinePaymentId, succeed: true, h.Sandbox, default));
        Assert.Equal("Created", (await h.Payments.GetAsync(Contact, started.OnlinePaymentId, default))!.Status);
    }

    private static IConfiguration Config(string secret) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Payments:Sandbox:Secret"] = secret }).Build();

    private static SandboxPaymentGateway Sandbox() => new(Config("a-test-sandbox-secret-long-enough"));

    private sealed class Harness : IAsyncDisposable
    {
        public required AccountingDbContext Db { get; init; }

        public required OnlinePaymentService Payments { get; init; }

        public required SandboxPaymentGateway Sandbox { get; init; }

        public Task<StartPaymentResult> StartAsync(long contactId, decimal invoiceAmount, decimal extra = 0m) =>
            Payments.StartAsync(contactId, new StartPaymentRequest
            {
                Invoices = [new PaymentAllocationRequest { InvoiceId = Invoice, Amount = invoiceAmount }],
                Amount = invoiceAmount + extra,
            }, default);

        public static async Task<Harness> CreateAsync(
            PostgresFixture postgres, bool onlineAccount = true, IPaymentGateway? gateway = null, RecordingLedger? ledger = null)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid() };
            Guid orgId = tenant.OrgId!.Value;
            AccountingDbContext db = postgres.CreateContext(tenant.CustomerId!.Value, orgId);

            async Task<long> Account(string code, string name, int type)
            {
                var account = new Account
                {
                    OrgId = orgId, AccountCode = code, AccountName = name, AccountSystemName = name, AccountTypeId = type, IsActive = true,
                };
                db.Accounts.Add(account);
                await db.SaveChangesAsync();
                return account.AccountId;
            }

            long receivable = await Account("1100", "Accounts Receivable", 1);
            long revenue = await Account("4100", "Sales Revenue", 4);
            long bankLedger = await Account("1510", "Gateway Clearing", 1);

            // The invoice as Sales posted it: owed by the contact on the control leg.
            db.JournalLedger.AddRange(
                Leg(orgId, receivable, 3, debit: 1_180m, contactId: Contact),
                Leg(orgId, revenue, 1, credit: 1_180m, contactId: Contact));
            await db.SaveChangesAsync();

            db.BankAccounts.Add(new BankAccount
            {
                OrgId = orgId,
                AccountName = "Gateway clearing",
                AccountNumber = "GW-1",
                AccountType = BankAccountType.Current,
                CurrencyCode = "INR",
                LedgerAccountId = bankLedger,
                IsActive = true,
                IsOnlinePaymentAccount = onlineAccount,
            });
            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var numbers = new NumberGenerator(db, Options.Create(new NumberingOptions()), new StubFinancialYear());
            var receipts = new ReceiveMoneyService(
                db, ledger ?? new RecordingLedger(), numbers, new StubBaseCurrency(), new StubCurrentUser(), TimeProvider.System);

            SandboxPaymentGateway sandbox = OnlinePaymentTests.Sandbox();

            return new Harness
            {
                Db = db,
                Sandbox = sandbox,
                Payments = new OnlinePaymentService(db, tenant, gateway ?? sandbox, new StubBaseCurrency(), receipts, TimeProvider.System),
            };
        }

        private static JournalLedger Leg(Guid orgId, long accountId, int ledgerType, decimal debit = 0m, decimal credit = 0m, long? contactId = null) => new()
        {
            OrgId = orgId,
            LedgerDate = new DateOnly(2026, 9, 1),
            AccountId = accountId,
            TransactionTypeCode = "INV",
            TransactionId = Invoice,
            TransactionDetailId = 0,
            DebitAmount = debit,
            CreditAmount = credit,
            DebitAmountBase = debit,
            CreditAmountBase = credit,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            ContactId = contactId,
            LedgerTypeId = ledgerType,
            LedgerSourceId = 1,
            DocumentNo = "INV/26/5001",
        };

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
