using Microsoft.EntityFrameworkCore;
using Sales.Api.Services.EInvoicing;
using Sales.Api.Services.Printing;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Contacts;
using Shared.Kernel.Documents;
using Shared.Kernel.Errors;
using Shared.Kernel.Persistence;
using Shared.Kernel.Printing;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// Registering at the IRP after posting, retrying, cancelling on void, and what
/// prints (TK-92), against a real database and the sandbox IRP.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class EInvoiceRegistrationTests
{
    private static readonly DateOnly Dated = new(2026, 9, 20);

    private readonly PostgresFixture _postgres;

    public EInvoiceRegistrationTests(PostgresFixture postgres) => _postgres = postgres;

    // ---- Posting ------------------------------------------------------------

    [SkippableFact]
    public async Task Posting_a_b2b_invoice_writes_a_pending_row_and_asks_for_one_attempt_after_commit()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using Harness h = await Harness.StartAsync(_postgres);
        Invoice invoice = await h.AddInvoiceAsync();

        EInvoiceStateView? state = await h.Posting.OnPostedAsync(EInvoiceSource.Invoice, invoice.InvoiceId, invoice, default);

        Assert.NotNull(state);
        Assert.Equal(EInvoiceStatus.Pending, state!.Status);
        Assert.Single(h.AfterCommit.Work);
        EInvoice row = await h.Db.EInvoices.AsNoTracking().SingleAsync();
        Assert.Equal(invoice.InvoiceId, row.SourceId);
        Assert.Equal(0, row.Attempts);
    }

    [SkippableFact]
    public async Task The_attempt_after_commit_registers_once_and_fills_the_response()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using Harness h = await Harness.StartAsync(_postgres);
        Invoice invoice = await h.AddInvoiceAsync();
        EInvoiceStateView state = (await h.Posting.OnPostedAsync(EInvoiceSource.Invoice, invoice.InvoiceId, invoice, default))!;

        await h.AfterCommit.RunAsync();

        Assert.Equal(EInvoiceStatus.Registered, state.Status);
        Assert.Matches("^[0-9a-f]{64}$", state.Irn);
        EInvoice row = await h.Db.EInvoices.AsNoTracking().SingleAsync();
        Assert.Equal(1, row.Attempts);
        Assert.NotNull(row.SignedQrCode);
    }

    [SkippableFact]
    public async Task Posting_twice_writes_one_row()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using Harness h = await Harness.StartAsync(_postgres);
        Invoice invoice = await h.AddInvoiceAsync();

        await h.Posting.OnPostedAsync(EInvoiceSource.Invoice, invoice.InvoiceId, invoice, default);
        await h.Posting.OnPostedAsync(EInvoiceSource.Invoice, invoice.InvoiceId, invoice, default);

        Assert.Equal(1, await h.Db.EInvoices.CountAsync());
    }

    [SkippableFact]
    public async Task A_till_sale_a_consumer_and_a_branch_that_does_not_e_invoice_get_no_row()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using Harness h = await Harness.StartAsync(_postgres);
        Invoice consumer = await h.AddInvoiceAsync(gstin: null);
        h.Book.Address!.Gstin = null;
        h.Book.Address.RegistrationType = "Consumer";

        Assert.Null(await h.Posting.OnPostedAsync(EInvoiceSource.Invoice, consumer.InvoiceId, consumer, default));

        Invoice pos = await h.AddInvoiceAsync();
        pos.TransactionTypeCode = "POS";
        Assert.Null(await h.Posting.OnPostedAsync(EInvoiceSource.Invoice, pos.InvoiceId, pos, default));

        h.Branch.From = null;
        Invoice b2b = await h.AddInvoiceAsync();
        Assert.Null(await h.Posting.OnPostedAsync(EInvoiceSource.Invoice, b2b.InvoiceId, b2b, default));
        Assert.Equal(0, await h.Db.EInvoices.CountAsync());
    }

    [SkippableFact]
    public async Task A_buyer_that_could_not_be_looked_up_gets_a_provisional_row_the_registrar_removes()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using Harness h = await Harness.StartAsync(_postgres);
        Invoice invoice = await h.AddInvoiceAsync(gstin: null);
        h.Book.Fail = true;

        EInvoiceStateView? state = await h.Posting.OnPostedAsync(EInvoiceSource.Invoice, invoice.InvoiceId, invoice, default);
        Assert.NotNull(state);

        // Master answers again, and the buyer is a consumer after all.
        h.Book.Fail = false;
        h.Book.Address!.Gstin = null;
        h.Book.Address.RegistrationType = "Consumer";
        Assert.Null(await h.Registrar.RegisterAsync(state!.EInvoiceId, null, default));
        Assert.Equal(0, await h.Db.EInvoices.CountAsync());
    }

    // ---- The registrar -------------------------------------------------------

    [SkippableFact]
    public async Task A_document_that_fails_validation_is_failed_and_goes_on_the_task_list()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using Harness h = await Harness.StartAsync(_postgres);
        Invoice invoice = await h.AddInvoiceAsync();
        h.Book.Address!.PostalCode = null;
        long id = await h.PendingAsync(invoice);

        EInvoiceStateView state = (await h.Registrar.RegisterAsync(id, null, default))!;

        Assert.Equal(EInvoiceStatus.Failed, state.Status);
        Assert.Contains("PIN code of the customer", state.Message);
        Assert.Equal(0, state.Attempts);
        (string worker, string? job) = Assert.Single(h.Auditor.Audited);
        Assert.Equal("EInvoice", worker);
        Assert.Contains($"EInvoice:{id}", job);
    }

    [SkippableFact]
    public async Task Master_unreachable_waits_with_a_backoff_and_stays_pending()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using Harness h = await Harness.StartAsync(_postgres);
        Invoice invoice = await h.AddInvoiceAsync();
        long id = await h.PendingAsync(invoice);
        h.Book.Fail = true;

        EInvoiceStateView state = (await h.Registrar.RegisterAsync(id, null, default))!;

        Assert.Equal(EInvoiceStatus.Pending, state.Status);
        Assert.NotNull(state.NextAttemptAt);
        Assert.True(state.NextAttemptAt > h.Clock.GetUtcNow());
        Assert.Empty(h.Auditor.Audited);
    }

    [SkippableFact]
    public async Task A_row_already_claimed_is_not_sent_again()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using Harness h = await Harness.StartAsync(_postgres);
        Invoice invoice = await h.AddInvoiceAsync();
        long id = await h.PendingAsync(invoice, nextAttemptAt: h.Clock.GetUtcNow().AddMinutes(1));

        EInvoiceStateView state = (await h.Registrar.RegisterAsync(id, null, default))!;

        Assert.Equal(EInvoiceStatus.Pending, state.Status);
        Assert.Equal(0, state.Attempts);
    }

    [SkippableFact]
    public async Task A_lost_answer_followed_by_a_duplicate_stores_the_existing_irn()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using Harness h = await Harness.StartAsync(_postgres);
        Invoice invoice = await h.AddInvoiceAsync();
        long id = await h.PendingAsync(invoice);

        // The IRP registered the document, and the answer never arrived.
        EInvoiceBuild build = await h.Builder.BuildForInvoiceAsync(invoice.InvoiceId, default);
        IrpResult<IrnDetails> first = await h.Gateway.GenerateIrnAsync(EInvoiceMapperTests.SellerGstin, build.Document!, default);

        EInvoiceStateView state = (await h.Registrar.RegisterAsync(id, null, default))!;

        Assert.Equal(EInvoiceStatus.Registered, state.Status);
        Assert.Equal(first.Value!.Irn, state.Irn);
        Assert.Equal(first.Value.AckNo, state.AckNo);
    }

    [SkippableFact]
    public async Task A_permanent_irp_refusal_is_failed_and_audited()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using Harness h = await Harness.StartAsync(_postgres, new UnconfiguredEInvoiceGateway());
        Invoice invoice = await h.AddInvoiceAsync();
        long id = await h.PendingAsync(invoice);

        EInvoiceStateView state = (await h.Registrar.RegisterAsync(id, null, default))!;

        Assert.Equal(EInvoiceStatus.Failed, state.Status);
        Assert.Equal(1, state.Attempts);
        Assert.Single(h.Auditor.Audited);
    }

    [SkippableFact]
    public async Task A_person_can_retry_a_failed_row_after_fixing_it()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using Harness h = await Harness.StartAsync(_postgres);
        Invoice invoice = await h.AddInvoiceAsync();
        h.Book.Address!.PostalCode = null;
        long id = await h.PendingAsync(invoice);
        await h.Registrar.RegisterAsync(id, null, default);

        h.Book.Address.PostalCode = "600018";
        EInvoiceStateView state = (await h.Actions.RetryAsync(EInvoiceSource.Invoice, invoice.InvoiceId, default))!;
        await h.AfterCommit.RunAsync();

        Assert.Equal(EInvoiceStatus.Registered, state.Status);
    }

    // ---- Voiding -------------------------------------------------------------

    [SkippableFact]
    public async Task Void_at_23_hours_cancels_the_irn()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using Harness h = await Harness.StartAsync(_postgres);
        Invoice invoice = await h.AddInvoiceAsync();
        await h.RegisteredAsync(invoice);

        h.Clock.Advance(TimeSpan.FromHours(23));
        string? refusal = await h.Posting.BeforeVoidAsync(EInvoiceSource.Invoice, invoice.InvoiceId, "Wrong customer", null, default);
        await h.Db.SaveChangesAsync();

        Assert.Null(refusal);
        EInvoice row = await h.Db.EInvoices.AsNoTracking().SingleAsync();
        Assert.Equal(EInvoiceStatus.Cancelled, row.Status);
        Assert.Equal(EInvoiceCancelReason.Other, row.CancelReason);
        Assert.Equal("Wrong customer", row.CancelRemark);
    }

    [SkippableFact]
    public async Task Void_at_25_hours_is_refused_and_names_the_credit_note()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using Harness h = await Harness.StartAsync(_postgres);
        Invoice invoice = await h.AddInvoiceAsync();
        await h.RegisteredAsync(invoice);

        h.Clock.Advance(TimeSpan.FromHours(25));
        string? refusal = await h.Posting.BeforeVoidAsync(EInvoiceSource.Invoice, invoice.InvoiceId, "Late", null, default);

        Assert.NotNull(refusal);
        Assert.Contains("credit note", refusal);
        Assert.Equal(EInvoiceStatus.Registered, (await h.Db.EInvoices.AsNoTracking().SingleAsync()).Status);
    }

    [SkippableFact]
    public async Task A_pending_e_invoice_is_simply_cancelled_with_the_void()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using Harness h = await Harness.StartAsync(_postgres, new UnconfiguredEInvoiceGateway());
        Invoice invoice = await h.AddInvoiceAsync();
        await h.PendingAsync(invoice);

        string? refusal = await h.Posting.BeforeVoidAsync(
            EInvoiceSource.Invoice, invoice.InvoiceId, "Duplicate", EInvoiceCancelReason.Duplicate, default);
        await h.Db.SaveChangesAsync();

        Assert.Null(refusal);
        EInvoice row = await h.Db.EInvoices.AsNoTracking().SingleAsync();
        Assert.Equal(EInvoiceStatus.Cancelled, row.Status);
        Assert.Equal(EInvoiceCancelReason.Duplicate, row.CancelReason);
    }

    // ---- Printing ------------------------------------------------------------

    [Theory]
    [InlineData(DocumentStatus.Posted, EInvoiceStatus.Pending, "IRN PENDING")]
    [InlineData(DocumentStatus.Posted, EInvoiceStatus.Failed, "IRN PENDING")]
    [InlineData(DocumentStatus.Posted, EInvoiceStatus.Registered, null)]
    [InlineData(DocumentStatus.Void, EInvoiceStatus.Cancelled, "VOID")]
    [InlineData(DocumentStatus.Void, EInvoiceStatus.Pending, "VOID")]
    [InlineData(DocumentStatus.Draft, null, "PROFORMA")]
    public void A_pending_invoice_prints_stamped(DocumentStatus status, EInvoiceStatus? eInvoice, string? expected)
    {
        Assert.Equal(expected, InvoicePrintService.Watermark(status, eInvoice));
    }

    [Fact]
    public void Only_a_registered_e_invoice_puts_its_irn_in_the_payload()
    {
        var pending = new PrintPayload();
        InvoicePrintService.AddEInvoice(pending, new EInvoice { Status = EInvoiceStatus.Pending, Irn = "x" });
        var registered = new PrintPayload();
        InvoicePrintService.AddEInvoice(registered, new EInvoice
        {
            Status = EInvoiceStatus.Registered,
            Irn = "abc",
            AckNo = "1126",
            AckDate = new DateTimeOffset(2026, 9, 20, 20, 0, 0, TimeSpan.Zero),
            SignedQrCode = "qr",
        });

        Assert.Empty(pending.Singles);
        Assert.Equal("abc", registered.Singles["EInvoice.Irn"]);
        Assert.Equal("qr", registered.Singles["EInvoice.QrImage"]);
        // 20:00 UTC is past midnight in India.
        Assert.Equal(new DateOnly(2026, 9, 21), registered.Singles["EInvoice.AckDate"]);
    }

    // ---- Harness -------------------------------------------------------------

    private sealed class Harness : IAsyncDisposable
    {
        public required SalesDbContext Db { get; init; }
        public required EInvoicePosting Posting { get; init; }
        public required EInvoiceRegistrar Registrar { get; init; }
        public required EInvoiceActions Actions { get; init; }
        public required EInvoiceDocumentBuilder Builder { get; init; }
        public required IEInvoiceGateway Gateway { get; init; }
        public required RecordingAfterCommit AfterCommit { get; init; }
        public required RecordingAuditor Auditor { get; init; }
        public required Book Book { get; init; }
        public required Branch Branch { get; init; }
        public required ManualClock Clock { get; init; }

        public static Task<Harness> StartAsync(PostgresFixture postgres, IEInvoiceGateway? gateway = null)
        {
            SalesDbContext db = postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
            var clock = new ManualClock(new DateTimeOffset(2026, 9, 21, 4, 0, 0, TimeSpan.Zero));
            gateway ??= new SandboxEInvoiceGateway(clock);
            var branch = new Branch { From = new DateOnly(2026, 4, 1) };
            var book = new Book
            {
                Address = new ContactPostalAddress
                {
                    ContactId = 42,
                    LegalName = "Kaveri Constructions Pvt Ltd",
                    Gstin = EInvoiceMapperTests.IntraBuyerGstin,
                    AddressLine1 = "12 Anna Salai",
                    City = "Chennai",
                    PostalCode = "600018",
                    StateCode = "33",
                    RegistrationType = "Regular",
                },
            };
            var identity = new StubOrgIdentity(new OrgIdentity(
                "Test Traders", EInvoiceMapperTests.SellerGstin, "1 Test Street", null, "Chennai", "33", "600001"));
            var builder = new EInvoiceDocumentBuilder(db, branch, identity, book, new StubNameLookup(), clock);
            var auditor = new RecordingAuditor();
            var registrar = new EInvoiceRegistrar(db, builder, gateway, auditor, clock);
            var afterCommit = new RecordingAfterCommit();

            return Task.FromResult(new Harness
            {
                Db = db,
                Builder = builder,
                Gateway = gateway,
                Registrar = registrar,
                Posting = new EInvoicePosting(db, branch, book, identity, gateway, afterCommit, registrar, clock),
                Actions = new EInvoiceActions(db, afterCommit, registrar),
                AfterCommit = afterCommit,
                Auditor = auditor,
                Book = book,
                Branch = branch,
                Clock = clock,
            });
        }

        public async Task<Invoice> AddInvoiceAsync(string? gstin = EInvoiceMapperTests.IntraBuyerGstin)
        {
            var invoice = new Invoice
            {
                TransactionTypeCode = "INV",
                DocumentNo = $"INV/{Guid.NewGuid():N}"[..14],
                DocumentDate = Dated,
                DueDate = Dated.AddDays(30),
                ContactId = 42,
                ContactGstin = gstin,
                CurrencyCode = "INR",
                SubTotal = 1000m,
                TaxableAmount = 1000m,
                CgstAmount = 90m,
                SgstAmount = 90m,
                TotalAmount = 1180m,
                Status = DocumentStatus.Posted,
                PostedAt = DateTimeOffset.UtcNow,
                Lines =
                [
                    new InvoiceDetail
                    {
                        LineNumber = 1,
                        ItemId = 7,
                        HsnSacCode = "72142090",
                        Quantity = 10m,
                        BaseQuantity = 10m,
                        UomId = 1,
                        UqcCode = "NOS",
                        UnitPrice = 100m,
                        GrossAmount = 1000m,
                        TaxableAmount = 1000m,
                        TaxAmount = 180m,
                        LineTotal = 1180m,
                        Taxes =
                        [
                            new InvoiceDetailTax { TaxComponent = TaxComponent.Cgst, SubAccountId = 1, Rate = 9m, TaxableAmount = 1000m, Amount = 90m, AmountBase = 90m },
                            new InvoiceDetailTax { TaxComponent = TaxComponent.Sgst, SubAccountId = 1, Rate = 9m, TaxableAmount = 1000m, Amount = 90m, AmountBase = 90m },
                        ],
                    },
                ],
            };
            Db.Invoices.Add(invoice);
            await Db.SaveChangesAsync();
            return invoice;
        }

        public async Task<long> PendingAsync(Invoice invoice, DateTimeOffset? nextAttemptAt = null)
        {
            var row = new EInvoice
            {
                SourceType = EInvoiceSource.Invoice,
                SourceId = invoice.InvoiceId,
                Status = EInvoiceStatus.Pending,
                NextAttemptAt = nextAttemptAt,
            };
            Db.EInvoices.Add(row);
            await Db.SaveChangesAsync();
            return row.EInvoiceId;
        }

        public async Task RegisteredAsync(Invoice invoice)
        {
            long id = await PendingAsync(invoice);
            EInvoiceStateView? state = await Registrar.RegisterAsync(id, null, default);
            Assert.Equal(EInvoiceStatus.Registered, state!.Status);
        }

        public async ValueTask DisposeAsync() => await Db.DisposeAsync();
    }

    private sealed class Branch : IBranchSettingsProvider
    {
        public DateOnly? From { get; set; }

        public Task<BranchSettings?> GetSettingsAsync(CancellationToken ct = default) =>
            Task.FromResult<BranchSettings?>(new BranchSettings("33", true, EInvoiceFrom: From));
    }

    private sealed class Book : IContactAddressBook
    {
        public ContactPostalAddress? Address { get; set; }

        public bool Fail { get; set; }

        public Task<IReadOnlyDictionary<long, ContactPostalAddress>> FindAsync(IEnumerable<long> ids, CancellationToken ct)
        {
            if (Fail)
            {
                throw new HttpRequestException("Master is down.");
            }

            IReadOnlyDictionary<long, ContactPostalAddress> found = Address is null
                ? new Dictionary<long, ContactPostalAddress>()
                : new Dictionary<long, ContactPostalAddress> { [Address.ContactId] = Address };
            return Task.FromResult(found);
        }
    }

    /// <summary>Holds the work instead of running it; the test says when the "commit" happened.</summary>
    private sealed class RecordingAfterCommit : IAfterCommit
    {
        public List<Func<CancellationToken, Task>> Work { get; } = [];

        public void Enqueue(Func<CancellationToken, Task> work) => Work.Add(work);

        public async Task RunAsync()
        {
            foreach (Func<CancellationToken, Task> work in Work.ToList())
            {
                await work(default);
            }

            Work.Clear();
        }
    }

    private sealed class RecordingAuditor : IWorkerErrorAuditor
    {
        public List<(string Worker, string? Job)> Audited { get; } = [];

        public Task<Guid?> AuditAsync(string workerName, string? jobReference, Exception exception, CancellationToken ct)
        {
            Audited.Add((workerName, jobReference));
            return Task.FromResult<Guid?>(Guid.NewGuid());
        }
    }

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
