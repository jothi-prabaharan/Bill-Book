using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sales.Api.Services;
using Sales.Repository.Repositories;
using Sales.Repository.Clients;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.CustomFields;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Shared.Kernel.Printing;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// Custom fields on sales documents (TK-112), against a real PostgreSQL: the
/// card's "done when" — a field typed on a quote reaches the invoice made from
/// it and prints there.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class DocumentCustomFieldCarryTests
{
    private readonly PostgresFixture _pg;

    public DocumentCustomFieldCarryTests(PostgresFixture pg) => _pg = pg;

    private sealed class Definitions : ICustomFieldDefinitions
    {
        public Task<IReadOnlyList<CustomFieldDefinitionDto>?> GetAsync(CustomFieldEntityKind kind, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CustomFieldDefinitionDto>?>(
            [
                new CustomFieldDefinitionDto { EntityKind = kind, Key = "site_ref", Label = "Site reference", DataType = CustomFieldDataType.Text, CarryForward = true, ShowOnPrint = true },
                new CustomFieldDefinitionDto { EntityKind = kind, Key = "internal_note", Label = "Internal note", DataType = CustomFieldDataType.Text },
            ]);
    }

    /// <summary>The card's "done when".</summary>
    [SkippableFact]
    public async Task A_field_typed_on_a_quote_reaches_the_invoice_made_from_it_and_prints()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        await using SalesDbContext db = _pg.CreateContext(customerId, orgId);
        db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));

        Quote quote = new()
        {
            TransactionTypeCode = "QTE",
            DocumentNo = "QTE/26-27/0001",
            DocumentDate = new DateOnly(2026, 9, 1),
            ValidUntil = new DateOnly(2026, 9, 30),
            ContactId = 42,
            CurrencyCode = "INR",
            Status = DocumentStatus.Posted,
            CustomFields = JsonDocument.Parse("""{ "site_ref": "TOWER-B-17", "internal_note": "not for the customer" }"""),
        };
        db.Quotes.Add(quote);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        TenantContext tenant = new() { CustomerId = customerId, OrgId = orgId, CustomerCode = "0000000042" };
        StubNameLookup names = new();
        InvoiceRepository invoices = new(
            db, tenant, new NumberGenerator(db, Options.Create(new NumberingOptions()), new StubFinancialYear()),
            new StubBaseCurrency(), new StubBranchSettings(), new StubTaxRates(), names, names, new StubCurrentUser(), TimeProvider.System,
            new RecordingInventory(), new RecordingLedger(), new StubCreditCheck(), new StubDocumentStorage(), new StubInvoicePdf(),
            new StubOrgIdentity(), new StubUqcLookup(), new StubEInvoicing(),
            customFields: new DocumentCustomFieldGate(new Definitions()));

        InvoiceResult created = await invoices.CreateAsync(new SaveInvoiceRequest
        {
            DocumentDate = new DateOnly(2026, 9, 2),
            DueDate = new DateOnly(2026, 10, 2),
            ContactId = 42,
            QuoteId = quote.QuoteId,
            PlaceOfSupplyStateCode = "33",
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            Lines =
            [
                new SaveInvoiceLineRequest
                {
                    ItemId = 7,
                    Description = "Installation",
                    LineType = DocumentLineType.Stock,
                    Quantity = 1m,
                    ConversionFactor = 1m,
                    UnitPrice = 1_000m,
                    TaxGroupId = 1,
                },
            ],
        }, default);
        Assert.Equal(InvoiceOutcome.Ok, created.Outcome);

        db.ChangeTracker.Clear();
        Invoice invoice = await db.Invoices.AsNoTracking().SingleAsync(i => i.InvoiceId == created.InvoiceId);
        Dictionary<string, JsonElement> stored = CustomFieldStore.Read(invoice.CustomFields);

        // Carried because the quote marks it carry-forward; the internal note is not.
        Assert.Equal("TOWER-B-17", stored["site_ref"].GetString());
        Assert.False(stored.ContainsKey("internal_note"));

        var payload = new PrintPayload();
        CustomFieldPrint.Add(payload, invoice.CustomFields, await new Definitions().GetAsync(CustomFieldEntityKind.Invoice));
        Assert.Equal("TOWER-B-17", payload.Singles["custom.site_ref"]);
        Assert.NotNull(PlaceholderCatalog.Find("INV", "custom.site_ref"));
    }
}
