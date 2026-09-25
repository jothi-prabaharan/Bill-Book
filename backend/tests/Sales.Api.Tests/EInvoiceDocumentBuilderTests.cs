using Sales.Api.Services.EInvoicing;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Contacts;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// From a posted document in the database to a checked INV-01 (TK-91): which
/// documents need an IRN, where the buyer's fields come from, and what happens
/// when Master cannot be asked.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class EInvoiceDocumentBuilderTests
{
    private static readonly DateOnly Dated = new(2026, 9, 20);

    private readonly PostgresFixture _postgres;

    public EInvoiceDocumentBuilderTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task A_posted_b2b_invoice_on_an_e_invoicing_branch_builds_a_ready_document()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using SalesDbContext db = NewBranch();
        Invoice invoice = await AddInvoiceAsync(db);

        EInvoiceBuild build = await Builder(db).BuildForInvoiceAsync(invoice.InvoiceId, default);

        Assert.True(build.Ready, string.Join("; ", build.Problems.Select(p => p.Message)));
        Inv01Document document = build.Document!;
        Assert.Equal("B2B", document.TranDtls.SupTyp);
        Assert.Equal("INV", document.DocDtls.Typ);
        Assert.Equal(invoice.DocumentNo, document.DocDtls.No);
        Assert.Equal("20/09/2026", document.DocDtls.Dt);
        Assert.Equal(EInvoiceMapperTests.SellerGstin, document.SellerDtls.Gstin);
        Assert.Equal(EInvoiceMapperTests.IntraBuyerGstin, document.BuyerDtls.Gstin);
        Assert.Equal(600018, document.BuyerDtls.Pin);
        Inv01Item item = Assert.Single(document.ItemList);
        Assert.Equal("NOS", item.Unit);
        Assert.Equal("Name 7", item.PrdDesc);
        Assert.Equal(1180m, document.ValDtls.TotInvVal);
    }

    [SkippableFact]
    public async Task The_gstin_the_document_was_raised_against_wins_over_the_contacts_current_one()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using SalesDbContext db = NewBranch();
        Invoice invoice = await AddInvoiceAsync(db);
        var book = new AddressBook { Address = Address() };
        book.Address.Gstin = "33AAAAA0000A1Z9";

        EInvoiceBuild build = await Builder(db, book: book).BuildForInvoiceAsync(invoice.InvoiceId, default);

        Assert.Equal(EInvoiceMapperTests.IntraBuyerGstin, build.Document!.BuyerDtls.Gstin);
    }

    [SkippableFact]
    public async Task A_branch_that_does_not_e_invoice_needs_no_irn()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using SalesDbContext db = NewBranch();
        Invoice invoice = await AddInvoiceAsync(db);

        EInvoiceBuild build = await Builder(db, eInvoicing: false).BuildForInvoiceAsync(invoice.InvoiceId, default);

        Assert.False(build.Applies);
        Assert.Null(build.Document);
    }

    [SkippableFact]
    public async Task A_document_dated_before_the_branch_starts_needs_no_irn()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using SalesDbContext db = NewBranch();
        Invoice invoice = await AddInvoiceAsync(db);

        EInvoiceBuild build = await Builder(db, from: Dated.AddDays(1)).BuildForInvoiceAsync(invoice.InvoiceId, default);

        Assert.False(build.Applies);
    }

    [SkippableFact]
    public async Task A_sale_to_a_consumer_needs_no_irn()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using SalesDbContext db = NewBranch();
        Invoice invoice = await AddInvoiceAsync(db, contactGstin: null);
        var book = new AddressBook { Address = Address() };
        book.Address.Gstin = null;
        book.Address.RegistrationType = "Consumer";

        EInvoiceBuild build = await Builder(db, book: book).BuildForInvoiceAsync(invoice.InvoiceId, default);

        Assert.False(build.Applies);
    }

    [SkippableFact]
    public async Task Master_unreachable_is_a_transient_problem_and_nothing_is_built()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using SalesDbContext db = NewBranch();
        Invoice invoice = await AddInvoiceAsync(db);

        EInvoiceBuild build = await Builder(db, book: new AddressBook { Fail = true }).BuildForInvoiceAsync(invoice.InvoiceId, default);

        Assert.True(build.Applies);
        Assert.True(build.Transient);
        Assert.Null(build.Document);
        Assert.Equal("BUYER_UNREAD", Assert.Single(build.Problems).Code);
    }

    [SkippableFact]
    public async Task A_buyer_without_a_pin_is_a_problem_to_fix_not_a_retry()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using SalesDbContext db = NewBranch();
        Invoice invoice = await AddInvoiceAsync(db);
        var book = new AddressBook { Address = Address() };
        book.Address.PostalCode = null;

        EInvoiceBuild build = await Builder(db, book: book).BuildForInvoiceAsync(invoice.InvoiceId, default);

        Assert.False(build.Ready);
        Assert.False(build.Transient);
        Assert.Contains(build.Problems, p => p.Code == "BUYER_PIN");
    }

    [SkippableFact]
    public async Task A_credit_note_builds_as_a_crn_naming_its_invoice()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using SalesDbContext db = NewBranch();
        Invoice invoice = await AddInvoiceAsync(db);
        CreditNote note = new()
        {
            TransactionTypeCode = "CRN",
            DocumentNo = "CRN/26-27/0003",
            DocumentDate = Dated,
            ContactId = 42,
            ContactGstin = EInvoiceMapperTests.IntraBuyerGstin,
            CurrencyCode = "INR",
            InvoiceId = invoice.InvoiceId,
            SubTotal = 100m,
            TaxableAmount = 100m,
            CgstAmount = 9m,
            SgstAmount = 9m,
            TotalAmount = 118m,
            Status = DocumentStatus.Posted,
            PostedAt = DateTimeOffset.UtcNow,
            Lines =
            [
                new CreditNoteDetail
                {
                    LineNumber = 1,
                    ItemId = 7,
                    HsnSacCode = "72142090",
                    Quantity = 1m,
                    BaseQuantity = 1m,
                    UnitPrice = 100m,
                    GrossAmount = 100m,
                    TaxableAmount = 100m,
                    TaxAmount = 18m,
                    LineTotal = 118m,
                    UqcCode = "NOS",
                    InvoiceDetailId = invoice.Lines[0].InvoiceDetailId,
                    Taxes = [Tax<CreditNoteDetailTax>(TaxComponent.Cgst, 100m, 9m), Tax<CreditNoteDetailTax>(TaxComponent.Sgst, 100m, 9m)],
                },
            ],
        };
        db.CreditNotes.Add(note);
        await db.SaveChangesAsync();

        EInvoiceBuild build = await Builder(db).BuildForCreditNoteAsync(note.CreditNoteId, default);

        Assert.True(build.Ready, string.Join("; ", build.Problems.Select(p => p.Message)));
        Assert.Equal("CRN", build.Document!.DocDtls.Typ);
        Assert.Equal(invoice.DocumentNo, Assert.Single(build.Document.RefDtls!.PrecDocDtls).InvNo);
    }

    private SalesDbContext NewBranch() => _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

    /// <param name="eInvoicing">False for a branch that does not e-invoice at all.</param>
    /// <param name="from">When the branch starts; 1 April 2026 when not given.</param>
    private static EInvoiceDocumentBuilder Builder(
        SalesDbContext db, bool eInvoicing = true, DateOnly? from = null, AddressBook? book = null) =>
        new(
            db,
            new Branch(eInvoicing ? from ?? new DateOnly(2026, 4, 1) : null),
            new StubOrgIdentity(new OrgIdentity(
                "Test Traders", EInvoiceMapperTests.SellerGstin, "1 Test Street", null, "Chennai", "33", "600001")),
            book ?? new AddressBook { Address = Address() },
            new StubNameLookup(),
            new FixedClock(new DateTimeOffset(2026, 9, 21, 6, 0, 0, TimeSpan.Zero)));

    private static ContactPostalAddress Address() => new()
    {
        ContactId = 42,
        LegalName = "Kaveri Constructions Pvt Ltd",
        Gstin = EInvoiceMapperTests.IntraBuyerGstin,
        AddressLine1 = "12 Anna Salai",
        City = "Chennai",
        PostalCode = "600018",
        StateCode = "33",
        RegistrationType = "Regular",
    };

    private static async Task<Invoice> AddInvoiceAsync(SalesDbContext db, string? contactGstin = EInvoiceMapperTests.IntraBuyerGstin)
    {
        var invoice = new Invoice
        {
            TransactionTypeCode = "INV",
            DocumentNo = $"INV/{Guid.NewGuid():N}"[..14],
            DocumentDate = Dated,
            DueDate = Dated.AddDays(30),
            ContactId = 42,
            ContactGstin = contactGstin,
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
                    Taxes = [Tax<InvoiceDetailTax>(TaxComponent.Cgst, 1000m, 90m), Tax<InvoiceDetailTax>(TaxComponent.Sgst, 1000m, 90m)],
                },
            ],
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice;
    }

    private static T Tax<T>(TaxComponent component, decimal taxable, decimal amount)
        where T : DocumentLineTaxBase, new() => new()
        {
            TaxComponent = component,
            SubAccountId = 1,
            Rate = 9m,
            TaxableAmount = taxable,
            Amount = amount,
            AmountBase = amount,
        };

    /// <summary>A branch whose e-invoicing starts on <paramref name="from"/>, or never.</summary>
    private sealed class Branch(DateOnly? from) : IBranchSettingsProvider
    {
        public Task<BranchSettings?> GetSettingsAsync(CancellationToken ct = default) =>
            Task.FromResult<BranchSettings?>(new BranchSettings("33", true, EInvoiceFrom: from));
    }

    private sealed class AddressBook : IContactAddressBook
    {
        public ContactPostalAddress? Address { get; init; }

        public bool Fail { get; init; }

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

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
