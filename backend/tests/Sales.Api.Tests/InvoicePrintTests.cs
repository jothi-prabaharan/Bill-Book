using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Sales.Api.Services.Printing;
using Sales.Entity.TableEntities;
using Shared.Kernel.Documents;
using Shared.Kernel.Printing;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// An invoice printed through its template (TK-24): what Sales puts in the
/// payload, and what it sends Printing.
///
/// The payload is keyed by <see cref="PlaceholderCatalog"/>'s tags, so every tag
/// this builder writes is checked against the catalogue — a misspelt key would
/// print as nothing, silently, on every customer's invoice.
/// </summary>
public sealed class InvoicePrintPayloadTests
{
    private static readonly OrgIdentity Seller = new(
        "Sample Traders", "33AABCU9603R1ZM", "12 Anna Salai", null, "Chennai", "33", "600002");

    private static Invoice Invoice() => new()
    {
        InvoiceId = 7,
        TransactionTypeCode = "INV",
        DocumentNo = "INV-0007",
        DocumentDate = new DateOnly(2026, 9, 24),
        DueDate = new DateOnly(2026, 10, 24),
        ContactId = 3,
        ContactGstin = "29AABCU9603R1ZX",
        BillingAddress = "1 MG Road\nBengaluru <560001>",
        CurrencyCode = "INR",
        SubTotal = 1500m,
        TaxableAmount = 1500m,
        IgstAmount = 234m,
        TotalAmount = 1734m,
        Status = DocumentStatus.Posted,
        Lines =
        [
            new InvoiceDetail
            {
                LineNumber = 2,
                ItemId = 11,
                Description = "Making charges",
                Quantity = 1m,
                UnitPrice = 500m,
                TaxableAmount = 500m,
                LineTotal = 590m,
                Taxes = [new InvoiceDetailTax { TaxComponent = TaxComponent.Igst, Rate = 18m, TaxableAmount = 500m, Amount = 90m }],
            },
            new InvoiceDetail
            {
                LineNumber = 1,
                ItemId = 10,
                Description = "Gold bar",
                Quantity = 2m,
                UnitPrice = 500m,
                TaxableAmount = 1000m,
                LineTotal = 1030m,
                Taxes = [new InvoiceDetailTax { TaxComponent = TaxComponent.Igst, Rate = 3m, TaxableAmount = 1000m, Amount = 30m }],
            },
        ],
    };

    private static PrintPayload Build() => InvoicePrintPayload.Build(
        Invoice(), Seller, "Karnataka Traders", new Dictionary<long, string> { [10] = "Gold bar 24K" });

    [Fact]
    public void Every_key_is_a_tag_the_invoice_catalogue_knows()
    {
        PrintPayload payload = Build();

        IEnumerable<string> keys = payload.Singles.Keys
            .Concat(payload.Lists.Values.SelectMany(rows => rows.SelectMany(row => row.Keys)));

        Assert.All(keys.Distinct(), key => Assert.NotNull(PlaceholderCatalog.Find("INV", key)));
    }

    [Fact]
    public void The_seller_the_buyer_and_the_totals_are_the_invoices_own()
    {
        PrintPayload payload = Build();

        Assert.Equal("Sample Traders", payload.Singles["Organization.Name"]);
        Assert.Equal("33AABCU9603R1ZM", payload.Singles["Organization.Gstin"]);
        Assert.Equal("INV-0007", payload.Singles["Document.No"]);
        Assert.Equal(new DateOnly(2026, 9, 24), payload.Singles["Document.Date"]);
        Assert.Equal("Karnataka Traders", payload.Singles["Party.Name"]);
        Assert.Equal(1734m, payload.Singles["Totals.GrandTotal"]);
        Assert.Equal(234m, payload.Singles["Totals.Tax"]);
    }

    [Fact]
    public void Lines_print_in_line_order_under_the_item_name_when_there_is_one()
    {
        IReadOnlyList<IReadOnlyDictionary<string, object?>> items = Build().Rows("Item");

        Assert.Equal([1, 2], items.Select(i => i["Item.SlNo"]).Cast<int>());
        Assert.Equal("Gold bar 24K", items[0]["Item.ItemName"]);
        // No name resolved for item 11, so its line's own description stands in.
        Assert.Equal("Making charges", items[1]["Item.ItemName"]);
        Assert.Equal(18m, items[1]["Item.TaxRate"]);
    }

    [Fact]
    public void Tax_is_one_row_per_component_and_rate()
    {
        IReadOnlyList<IReadOnlyDictionary<string, object?>> tax = Build().Rows("Tax");

        Assert.Equal(2, tax.Count);
        Assert.Equal(("IGST", 3m, 30m), ((string)tax[0]["Tax.Component"]!, (decimal)tax[0]["Tax.Rate"]!, (decimal)tax[0]["Tax.Amount"]!));
        Assert.Equal(("IGST", 18m, 90m), ((string)tax[1]["Tax.Component"]!, (decimal)tax[1]["Tax.Rate"]!, (decimal)tax[1]["Tax.Amount"]!));
    }

    [Fact]
    public void A_typed_address_is_escaped_and_keeps_its_line_breaks()
    {
        Assert.Equal("1 MG Road<br>Bengaluru &lt;560001&gt;", Build().Singles["Party.Address"]);
    }

    [Theory]
    [InlineData("29AABCU9603R1ZX", "33", "29")]
    [InlineData(null, "33", "33")]
    [InlineData("", "33", "33")]
    [InlineData(null, null, null)]
    public void Place_of_supply_is_the_customers_state_or_else_the_branchs(
        string? customerGstin, string? branchState, string? expected)
    {
        Assert.Equal(expected, InvoicePrintPayload.PlaceOfSupply(customerGstin, branchState));
    }
}

/// <summary>
/// What Sales sends Printing, over a handler that records the request instead of
/// a server — the seam a stubbed <see cref="IPrintingClient"/> would hide.
/// </summary>
public sealed class PrintingClientTests
{
    [Fact]
    public async Task Posts_the_payload_to_the_render_route_under_the_callers_own_token()
    {
        var handler = new RecordingHandler();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        accessor.HttpContext.Request.Headers.Authorization = "Bearer user-token";

        var client = new PrintingClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://printing/") }, accessor);

        var payload = new PrintPayload();
        payload.Singles["Document.No"] = "INV-0007";
        payload.Singles["Totals.GrandTotal"] = 1734.5m;

        PrintedDocument printed = await client.RenderAsync("INV", 5, payload, CancellationToken.None);

        Assert.Equal("/api/print/render", handler.Path);
        Assert.Equal("Bearer user-token", handler.Authorization);

        using JsonDocument body = JsonDocument.Parse(handler.Body!);
        JsonElement root = body.RootElement;
        Assert.Equal("INV", root.GetProperty("documentTypeCode").GetString());
        Assert.Equal(5, root.GetProperty("printTemplateId").GetInt64());

        // Dictionary keys keep their catalogue spelling; values keep their type.
        JsonElement singles = root.GetProperty("payload").GetProperty("singles");
        Assert.Equal("INV-0007", singles.GetProperty("Document.No").GetString());
        Assert.Equal(1734.5m, singles.GetProperty("Totals.GrandTotal").GetDecimal());

        Assert.Equal("<p>printed</p>", printed.Html);
        Assert.Equal(1, printed.PageCount);
    }

    [Fact]
    public async Task A_refusal_from_printing_is_an_error_not_an_empty_document()
    {
        var handler = new RecordingHandler { Status = HttpStatusCode.InternalServerError };
        var client = new PrintingClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://printing/") },
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() });

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.RenderAsync("INV", null, new PrintPayload(), CancellationToken.None));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;

        public string? Path { get; private set; }

        public string? Authorization { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri?.AbsolutePath;
            Authorization = request.Headers.Authorization?.ToString();
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(Status)
            {
                Content = new StringContent(
                    """{"html":"<p>printed</p>","pageCount":1,"unknownTags":[],"printTemplateId":5}""",
                    Encoding.UTF8,
                    "application/json"),
            };
        }
    }
}
