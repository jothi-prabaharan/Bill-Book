using Shared.Kernel.Documents;
using Shared.Kernel.Printing;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// What a sales or purchase document prints as, tested without a service or a database: the one builder
/// every trading document's print route shares (TK-263). Written by an AI and not run (section 0.5 of
/// docs/Task_Details.md).
/// </summary>
public sealed class DocumentPrintPayloadTests
{
    private sealed class TestDocument : DocumentHeaderBase;

    private sealed class TestLine : DocumentLineBase;

    private sealed class TestTax : DocumentLineTaxBase;

    private static readonly OrgIdentity Org = new(
        "Sample Traders", "33AABCU9603R1ZM", "12 Anna Salai", "Mount Road", "Chennai", "33", "600002", "9123456780", "hello@example.test");

    private static TestDocument Document(DocumentStatus status = DocumentStatus.Posted) => new()
    {
        TransactionTypeCode = "QTE",
        DocumentNo = "QT/26/00004",
        DocumentDate = new DateOnly(2026, 11, 2),
        ContactId = 9,
        ContactGstin = "29AABCU9603R1ZX",
        BillingAddress = "1 MG Road\nBengaluru <560001>",
        ShippingAddress = "Dock 4",
        CurrencyCode = "INR",
        ExchangeRate = 1m,
        SubTotal = 1500m,
        DiscountAmount = 10m,
        TaxableAmount = 1490m,
        CgstAmount = 20m,
        SgstAmount = 20m,
        IgstAmount = 3m,
        CessAmount = 1m,
        RoundOffAmount = -0.5m,
        TotalAmount = 1533.5m,
        Notes = "Thank you",
        TermsAndConditions = "Net 30",
        Status = status,
    };

    private static TestLine Line(int number, long? itemId, string? description, decimal taxable) => new()
    {
        LineNumber = number,
        ItemId = itemId,
        Description = description,
        HsnSacCode = "7108",
        Quantity = 2m,
        UnitPrice = taxable / 2m,
        DiscountAmount = 1m,
        TaxableAmount = taxable,
        LineTotal = taxable * 1.18m,
    };

    private static TestTax Tax(TaxComponent component, decimal rate, decimal taxable, decimal amount) => new()
    {
        TaxComponent = component,
        Rate = rate,
        TaxableAmount = taxable,
        Amount = amount,
    };

    private static DocumentPrintLine Entry(TestLine line, params TestTax[] taxes) => new(line, [.. taxes]);

    private static PrintPayload Build(
        TestDocument? document = null, IReadOnlyList<DocumentPrintLine>? lines = null, IReadOnlyDictionary<long, string>? names = null) =>
        DocumentPrintPayload.Build(
            document ?? Document(),
            lines ?? [],
            Org,
            "Karnataka Traders",
            names ?? new Dictionary<long, string>());

    [Fact]
    public void The_organization_is_the_letterhead_and_the_party_is_the_other_side()
    {
        PrintPayload payload = Build();

        Assert.Equal("Sample Traders", payload.Singles["Organization.Name"]);
        Assert.Equal("Sample Traders", payload.Singles["Organization.LegalName"]);
        Assert.Equal("33AABCU9603R1ZM", payload.Singles["Organization.Gstin"]);
        Assert.Equal("33", payload.Singles["Organization.StateName"]);
        Assert.Equal("9123456780", payload.Singles["Organization.Phone"]);
        Assert.Equal("hello@example.test", payload.Singles["Organization.Email"]);
        Assert.Equal("12 Anna Salai<br>Mount Road<br>Chennai 600002", payload.Singles["Organization.Address"]);

        Assert.Equal("Karnataka Traders", payload.Singles["Party.Name"]);
        Assert.Equal("29AABCU9603R1ZX", payload.Singles["Party.Gstin"]);
        Assert.Equal("Dock 4", payload.Singles["Party.ShippingAddress"]);
    }

    [Fact]
    public void The_document_identity_and_totals_are_its_own_and_keep_their_types()
    {
        PrintPayload payload = Build();

        Assert.Equal("QT/26/00004", payload.Singles["Document.No"]);
        Assert.Equal(new DateOnly(2026, 11, 2), payload.Singles["Document.Date"]);
        Assert.Equal("Posted", payload.Singles["Document.Status"]);
        Assert.Equal("INR", payload.Singles["Document.Currency"]);

        Assert.Equal(1500m, payload.Singles["Totals.SubTotal"]);
        Assert.Equal(10m, payload.Singles["Totals.Discount"]);
        Assert.Equal(1490m, payload.Singles["Totals.TaxableValue"]);
        Assert.Equal(-0.5m, payload.Singles["Totals.RoundOff"]);
        Assert.Equal(1533.5m, payload.Singles["Totals.GrandTotal"]);

        // Every tax column adds into the one figure the template's Tax row shows.
        Assert.Equal(44m, payload.Singles["Totals.Tax"]);
    }

    [Fact]
    public void A_typed_address_and_terms_are_escaped_and_keep_their_line_breaks()
    {
        PrintPayload payload = Build();

        Assert.Equal("1 MG Road<br>Bengaluru &lt;560001&gt;", payload.Singles["Party.Address"]);
        Assert.Equal("Thank you", payload.Singles["Document.Notes"]);
        Assert.Equal("Net 30", payload.Singles["Document.Terms"]);
    }

    [Fact]
    public void The_builder_leaves_the_place_of_supply_to_the_service_that_knows_how_to_resolve_it()
    {
        // Sales takes it from the customer's GSTIN and Purchase from the document's state id, so neither is
        // guessed here.
        Assert.False(Build().Singles.ContainsKey("Document.PlaceOfSupply"));
    }

    [Fact]
    public void Lines_print_in_line_order_under_the_item_name_when_one_resolved()
    {
        PrintPayload payload = Build(
            lines:
            [
                Entry(Line(2, 11, "Making charges", 500m), Tax(TaxComponent.Cgst, 9m, 500m, 45m)),
                Entry(Line(1, 10, "Gold bar", 1000m), Tax(TaxComponent.Cgst, 9m, 1000m, 90m)),
            ],
            names: new Dictionary<long, string> { [10] = "Gold bar 24K" });

        IReadOnlyList<IReadOnlyDictionary<string, object?>> items = payload.Rows("Item");

        Assert.Equal([1, 2], items.Select(i => i["Item.SlNo"]).Cast<int>());
        Assert.Equal("Gold bar 24K", items[0]["Item.ItemName"]);

        // No name resolved for item 11, so its line's own description stands in.
        Assert.Equal("Making charges", items[1]["Item.ItemName"]);
        Assert.Equal(9m, items[1]["Item.TaxRate"]);
        Assert.Equal(2m, items[1]["Item.Quantity"]);
    }

    [Fact]
    public void A_line_note_follows_the_description_after_a_dash()
    {
        PrintPayload payload = Build(lines:
        [
            new DocumentPrintLine(Line(1, null, "Steel plate", 100m), [], "Accepted 1, rejected 1"),
            new DocumentPrintLine(Line(2, null, null, 100m), [], "Short delivery"),
        ]);

        IReadOnlyList<IReadOnlyDictionary<string, object?>> items = payload.Rows("Item");

        Assert.Equal("Steel plate — Accepted 1, rejected 1", items[0]["Item.Description"]);
        Assert.Equal("Short delivery", items[1]["Item.Description"]);
    }

    [Fact]
    public void Tax_is_one_row_per_component_and_rate_summed_across_lines()
    {
        PrintPayload payload = Build(lines:
        [
            Entry(Line(1, null, "a", 1000m), Tax(TaxComponent.Igst, 3m, 1000m, 30m)),
            Entry(Line(2, null, "b", 500m), Tax(TaxComponent.Igst, 18m, 500m, 90m)),
            Entry(Line(3, null, "c", 200m), Tax(TaxComponent.Igst, 18m, 200m, 36m)),
        ]);

        IReadOnlyList<IReadOnlyDictionary<string, object?>> tax = payload.Rows("Tax");

        Assert.Equal(2, tax.Count);
        Assert.Equal(("IGST", 3m, 1000m, 30m), Row(tax[0]));
        Assert.Equal(("IGST", 18m, 700m, 126m), Row(tax[1]));

        static (string, decimal, decimal, decimal) Row(IReadOnlyDictionary<string, object?> row) =>
            ((string)row["Tax.Component"]!, (decimal)row["Tax.Rate"]!, (decimal)row["Tax.TaxableValue"]!, (decimal)row["Tax.Amount"]!);
    }

    [Theory]
    [InlineData(DocumentStatus.Posted, null)]
    [InlineData(DocumentStatus.Draft, "DRAFT")]
    [InlineData(DocumentStatus.ReadyToPost, "DRAFT")]
    [InlineData(DocumentStatus.Void, "VOID")]
    public void Only_a_posted_document_prints_without_a_stamp(DocumentStatus status, string? expected)
    {
        Assert.Equal(expected, DocumentPrintPayload.Watermark(status));
    }

    [Fact]
    public void The_letterhead_helper_fills_the_same_tags_for_a_document_that_is_not_a_trading_one()
    {
        var payload = new PrintPayload();

        DocumentPrintPayload.AddOrganization(payload, Org);

        Assert.Equal("Sample Traders", payload.Singles["Organization.Name"]);
        Assert.Equal("12 Anna Salai<br>Mount Road<br>Chennai 600002", payload.Singles["Organization.Address"]);
    }

    [Fact]
    public void Every_tag_the_builder_writes_is_one_the_catalogue_offers_for_a_trading_document()
    {
        PrintPayload payload = Build(lines: [Entry(Line(1, 10, "Gold bar", 1000m), Tax(TaxComponent.Cgst, 9m, 1000m, 90m))]);

        IEnumerable<string> keys = payload.Singles.Keys
            .Concat(payload.Lists.Values.SelectMany(rows => rows.SelectMany(row => row.Keys)))
            .Distinct();

        // Document.ExchangeRate is carried for a template that wants it but is not offered in the Insert-field
        // panel, so it is the one key the catalogue does not list.
        Assert.All(
            keys.Where(key => key != "Document.ExchangeRate"),
            key => Assert.NotNull(PlaceholderCatalog.Find("QTE", key)));
    }
}
