using System.Text.Json;
using Printing.Api.Rendering;
using Printing.Api.Services;
using Printing.Entity.Models;
using Printing.Repository;
using Shared.Kernel.Printing;
using Xunit;

namespace Printing.Api.Tests;

/// <summary>
/// <c>POST api/print/render</c>: a payload pushed by the document's own service,
/// rendered with the layout the caller's branch resolves.
///
/// <b>The payload goes through JSON in every test here</b>, because that is the
/// path that can go wrong. The renderer's own tests hand it CLR values; a real
/// caller hands it text, and a number that arrives as a <see cref="JsonElement"/>
/// prints as bare digits with no grouping unless something converts it first.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PrintRenderTests
{
    private readonly PostgresFixture _postgres;

    public PrintRenderTests(PostgresFixture postgres) => _postgres = postgres;

    private static CancellationToken Ct => CancellationToken.None;

    private static PrintTemplateService Service(PrintingDbContext db) => new(db, new PrintRenderer());

    /// <summary>A payload as a caller would send it: serialised, then bound.</summary>
    private static RenderPayload OverTheWire(PrintPayload payload) =>
        JsonSerializer.Deserialize<RenderPayload>(JsonSerializer.Serialize(new
        {
            singles = payload.Singles,
            lists = payload.Lists,
        }), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    [SkippableFact]
    public async Task A_sample_payload_renders_from_the_platform_layout_when_the_branch_has_none()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using PrintingDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        PrintTemplateResult<RenderPrintResponse> result = await Service(db).RenderAsync(new RenderPrintRequest
        {
            DocumentTypeCode = "INV",
            Payload = OverTheWire(SamplePayload.For("INV")),
        }, Ct);

        Assert.Equal(PrintTemplateOutcome.Ok, result.Outcome);
        Assert.Equal(PrintTemplateSource.PlatformSeed, result.Value!.Source);
        Assert.Null(result.Value.PrintTemplateId);
        Assert.True(result.Value.PageCount >= 1);
        Assert.Contains("Sample Traders", result.Value.Html, StringComparison.Ordinal);
        Assert.Contains("SAMPLE-0001", result.Value.Html, StringComparison.Ordinal);
        Assert.Empty(result.Value.UnknownTags);
    }

    [SkippableFact]
    public async Task An_amount_sent_as_json_is_formatted_with_the_catalogue_mask()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using PrintingDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        PrintPayload payload = SamplePayload.For("INV");
        payload.Singles["Totals.GrandTotal"] = 1234567.5m;

        PrintTemplateResult<RenderPrintResponse> result = await Service(db).RenderAsync(new RenderPrintRequest
        {
            DocumentTypeCode = "INV",
            Payload = OverTheWire(payload),
        }, Ct);

        // Indian grouping, two decimals — the mask, not the raw digits.
        Assert.Contains("12,34,567.50", result.Value!.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("1234567.5", result.Value.Html, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task The_branch_default_is_used_when_the_document_names_no_template()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using PrintingDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        PrintTemplateService service = Service(db);

        PrintTemplateDetail created = (await service.CreateAsync(
            new CreatePrintTemplateRequest { DocumentTypeCode = "INV", TemplateName = "Branch layout" }, Ct)).Value!;

        await service.UpdateAsync(created.PrintTemplateId, new UpdatePrintTemplateRequest
        {
            TemplateName = created.TemplateName,
            Settings = created.Settings,
            Content = WithHeader(created.Content, "<div>Branch letterhead</div>"),
            TemplateVersion = created.TemplateVersion,
        }, Ct);

        PrintTemplateResult<RenderPrintResponse> result = await service.RenderAsync(new RenderPrintRequest
        {
            DocumentTypeCode = "INV",
            Payload = OverTheWire(SamplePayload.For("INV")),
        }, Ct);

        Assert.Equal(PrintTemplateSource.BranchDefault, result.Value!.Source);
        Assert.Equal(created.PrintTemplateId, result.Value.PrintTemplateId);
        Assert.Contains("Branch letterhead", result.Value.Html, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task Another_branchs_template_is_never_used_even_when_named()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customer = Guid.NewGuid();
        long foreignId;

        await using (PrintingDbContext other = _postgres.CreateContext(customer, Guid.NewGuid()))
        {
            foreignId = (await Service(other).CreateAsync(
                new CreatePrintTemplateRequest { DocumentTypeCode = "INV", TemplateName = "Theirs" }, Ct))
                .Value!.PrintTemplateId;
        }

        await using PrintingDbContext db = _postgres.CreateContext(customer, Guid.NewGuid());

        PrintTemplateResult<RenderPrintResponse> result = await Service(db).RenderAsync(new RenderPrintRequest
        {
            DocumentTypeCode = "INV",
            PrintTemplateId = foreignId,
            Payload = OverTheWire(SamplePayload.For("INV")),
        }, Ct);

        // The query filter hides the other branch's row, so the chain falls
        // through to the platform layout rather than printing on their paper.
        Assert.Equal(PrintTemplateSource.PlatformSeed, result.Value!.Source);
        Assert.Null(result.Value.PrintTemplateId);
    }

    [SkippableFact]
    public async Task The_callers_watermark_is_stamped_on_the_printed_page()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using PrintingDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        PrintTemplateResult<RenderPrintResponse> result = await Service(db).RenderAsync(new RenderPrintRequest
        {
            DocumentTypeCode = "INV",
            Payload = OverTheWire(SamplePayload.For("INV")),
            Watermark = "PROFORMA",
        }, Ct);

        Assert.Contains("class=\"pt-watermark\"", result.Value!.Html, StringComparison.Ordinal);
        Assert.Contains(">PROFORMA<", result.Value.Html, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task A_document_type_with_no_printable_layout_is_refused()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using PrintingDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        PrintTemplateResult<RenderPrintResponse> result = await Service(db).RenderAsync(new RenderPrintRequest
        {
            DocumentTypeCode = "POS",
            Payload = new RenderPayload(),
        }, Ct);

        Assert.Equal(PrintTemplateOutcome.InvalidDocumentType, result.Outcome);
    }

    private static PrintContent WithHeader(PrintContent content, string html)
    {
        PrintContent copy = PrintJson.Deserialize<PrintContent>(PrintJson.Serialize(content));
        copy.HeaderHtml = html;
        return copy;
    }
}

/// <summary>
/// The conversion from JSON to the values the formatter reads. No database: this
/// is the part of the render path that is pure.
/// </summary>
public sealed class PrintPayloadReaderTests
{
    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    [Fact]
    public void A_number_becomes_a_decimal_not_a_double()
    {
        Assert.Equal(0.1m, PrintPayloadReader.Value(Json("0.1")));
        Assert.IsType<decimal>(PrintPayloadReader.Value(Json("1234.50")));
    }

    [Fact]
    public void A_string_stays_a_string_and_null_stays_null()
    {
        Assert.Equal("INV-0001", PrintPayloadReader.Value(Json("\"INV-0001\"")));
        Assert.Null(PrintPayloadReader.Value(Json("null")));
    }

    [Fact]
    public void A_boolean_becomes_a_boolean()
    {
        Assert.Equal(true, PrintPayloadReader.Value(Json("true")));
        Assert.Equal(false, PrintPayloadReader.Value(Json("false")));
    }

    [Fact]
    public void An_object_is_kept_as_its_raw_text_rather_than_dropped()
    {
        Assert.Equal("{\"a\":1}", PrintPayloadReader.Value(Json("{\"a\":1}")));
    }

    [Fact]
    public void Keys_match_whatever_case_the_caller_serialised_them_in()
    {
        var wire = new RenderPayload
        {
            Singles = new() { ["document.no"] = Json("\"INV-7\"") },
            Lists = new()
            {
                ["item"] = [new() { ["item.rate"] = Json("45") }],
            },
        };

        PrintPayload payload = PrintPayloadReader.Read(wire);

        Assert.Equal("INV-7", payload.Singles["Document.No"]);
        Assert.Equal(45m, payload.Rows("Item").Single()["Item.Rate"]);
    }
}
