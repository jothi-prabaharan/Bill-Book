using Printing.Api.Services;
using Printing.Repository.Repositories;
using Printing.Entity.Models;
using Shared.Kernel.Printing;
using Xunit;

namespace Printing.Api.Tests;

/// <summary>A branch's own fields print as <c>{{custom.key}}</c> (TK-112).</summary>
public sealed class CustomFieldPrintTests
{
    private static readonly PrintRendererRepository Renderer = new();

    [Fact]
    public void An_invoice_prints_its_custom_field()
    {
        var payload = new PrintPayload();
        payload.Singles["custom.site_ref"] = "TOWER-B-17";

        PrintRenderResult result = Renderer.Render(new PrintRenderRequest
        {
            Content = new PrintContent { HeaderHtml = "<p>Site {{custom.site_ref}}</p>" },
            DocumentTypeCode = "INV",
            Payload = payload,
        });

        Assert.Contains("Site TOWER-B-17", result.Html);
        Assert.Empty(result.UnknownTags);
    }

    [Fact]
    public void A_custom_tag_the_document_has_no_value_for_prints_nothing_and_is_not_unknown()
    {
        PrintRenderResult result = Renderer.Render(new PrintRenderRequest
        {
            Content = new PrintContent { HeaderHtml = "<p>[{{custom.po_ref}}]</p>" },
            DocumentTypeCode = "INV",
            Payload = new PrintPayload(),
        });

        Assert.Contains("[]", result.Html);
        Assert.Empty(result.UnknownTags);
    }

    [Theory]
    [InlineData("INV", "custom.site_ref", true)]
    [InlineData("BIL", "custom.site_ref", true)]
    [InlineData("INV", "custom.Site-Ref", false)]
    [InlineData("INV", "custom.", false)]
    [InlineData("INV", "customer.name", false)]
    public void Only_well_formed_custom_tags_on_documents_that_carry_them_resolve(string type, string tag, bool resolves) =>
        Assert.Equal(resolves, PlaceholderCatalog.CustomTag(type, tag) is not null);
}
