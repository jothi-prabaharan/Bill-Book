using Shared.Kernel.Printing;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// Acceptance test 5.
///
/// Stripping script is what a sanitiser is for, and it fails loudly if it
/// regresses.
///
/// The second half of this file used to guard the placeholder chip, because a
/// stock allow-list drops contenteditable and class and the only symptom was a
/// template whose merge fields had quietly stopped merging. A placeholder is
/// {{Tag}} text now, so that failure cannot happen and the tests below assert
/// the stronger property instead: tags survive a sanitiser that keeps nothing.
/// </summary>
public class SegmentSanitizerTests
{
    private const string Single = "{{Invoice.No}}";
    private const string ListTag = "{{Item.ItemName}}";

    private static readonly SegmentSanitizer Sanitizer = new();

    [Fact]
    public void Script_elements_are_stripped()
    {
        string clean = Sanitizer.Sanitize("<div>before<script>alert(1)</script>after</div>");

        Assert.DoesNotContain("script", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert", clean, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Event_handler_attributes_are_stripped()
    {
        string clean = Sanitizer.Sanitize("<div onclick=\"steal()\">text</div>");

        Assert.DoesNotContain("onclick", clean, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("text", clean, StringComparison.Ordinal);
    }

    [Fact]
    public void Javascript_urls_are_stripped()
    {
        string clean = Sanitizer.Sanitize("<img src=\"javascript:alert(1)\" alt=\"x\">");

        Assert.DoesNotContain("javascript", clean, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("<iframe src=\"https://evil.example\"></iframe>", "iframe")]
    [InlineData("<object data=\"x\"></object>", "object")]
    [InlineData("<link rel=\"stylesheet\" href=\"https://evil.example/x.css\">", "link")]
    [InlineData("<style>body{display:none}</style>", "style>")]
    public void The_dangerous_elements_are_all_stripped(string html, string forbidden)
    {
        string clean = Sanitizer.Sanitize(html);

        Assert.DoesNotContain(forbidden, clean, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_image_may_not_point_outside_the_approved_hosts()
    {
        string clean = Sanitizer.Sanitize("<img src=\"https://evil.example/track.gif\" alt=\"x\">");

        Assert.DoesNotContain("evil.example", clean, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_image_on_an_approved_host_survives()
    {
        var sanitizer = new SegmentSanitizer(["cdn.billbook.example"]);

        string clean = sanitizer.Sanitize("<img src=\"https://cdn.billbook.example/logo.png\" alt=\"Logo\">");

        Assert.Contains("cdn.billbook.example/logo.png", clean, StringComparison.Ordinal);
    }

    [Fact]
    public void A_protocol_relative_url_is_treated_as_absolute_and_dropped()
    {
        // Relative to a URI parser, absolute to a browser. Closing that gap is
        // the whole reason the URL filter does not simply ask IsAbsoluteUri.
        string clean = Sanitizer.Sanitize("<img src=\"//evil.example/track.gif\" alt=\"x\">");

        Assert.DoesNotContain("evil.example", clean, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_relative_image_url_survives()
    {
        string clean = Sanitizer.Sanitize("<img src=\"/api/files/logo.png\" alt=\"Logo\">");

        Assert.Contains("/api/files/logo.png", clean, StringComparison.Ordinal);
    }

    [Fact]
    public void A_placeholder_is_text_and_passes_through_untouched()
    {
        string clean = Sanitizer.Sanitize($"<div>{Single}</div>");

        Assert.Contains("{{Invoice.No}}", clean, StringComparison.Ordinal);
    }

    [Fact]
    public void A_list_placeholder_is_no_different_from_a_single_one()
    {
        // Whether a tag repeats is the catalogue's declared Kind. Nothing in
        // the markup distinguishes them, so nothing in the markup can be lost.
        string clean = Sanitizer.Sanitize($"<td>{ListTag}</td>");

        Assert.Contains("{{Item.ItemName}}", clean, StringComparison.Ordinal);
    }

    [Fact]
    public void Placeholders_survive_a_hostile_document_losing_every_attribute()
    {
        string html = $"<table><tr><td onclick=\"x()\" class=\"gone\" contenteditable=\"true\">{ListTag}"
            + $"<script>alert(1)</script></td><td>{Single}</td></tr></table>";

        string clean = Sanitizer.Sanitize(html);

        Assert.DoesNotContain("script", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contenteditable", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("class", clean, StringComparison.OrdinalIgnoreCase);

        // Every attribute it arrived with is gone, and both tags still resolve.
        Assert.Equal(["Item.ItemName", "Invoice.No"], MergeTags.Extract(clean));
    }

    [Fact]
    public void The_editors_own_formatting_markup_survives()
    {
        string html = "<table><tbody><tr><td colspan=\"2\" align=\"right\" width=\"40%\" "
            + "style=\"font-weight:bold\"><b>Total</b> <i>x</i> <u>y</u></td></tr></tbody></table>";

        string clean = Sanitizer.Sanitize(html);

        foreach (string expected in new[] { "colspan", "align", "width", "<b>", "<i>", "<u>" })
        {
            Assert.Contains(expected, clean, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SanitizeContent_names_the_segment_and_what_was_removed()
    {
        var content = new PrintContent
        {
            HeaderHtml = "<div>clean</div>",
            DetailsHtml = "<div onclick=\"x()\">dirty</div>",
        };

        IReadOnlyList<SegmentViolation> violations = new SegmentSanitizer().SanitizeContent(content);

        SegmentViolation violation = Assert.Single(violations);
        Assert.Equal("attribute", violation.Kind);
        Assert.Contains("d: onclick", violation.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("onclick", content.DetailsHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reformatting_css_is_not_a_violation()
    {
        // AngleSharp re-serialises CSS canonically, so a browser-authored
        // "font-weight:bold" comes back with a space in it. Treating any
        // textual difference as a refusal would reject every legitimate save
        // in the product — the refusal has to mean something was removed.
        SegmentSanitizeResult result = Sanitizer.Inspect("<div style=\"font-weight:bold\">x</div>");

        Assert.Empty(result.Violations);
        Assert.Contains("font-weight: bold", result.Html, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int index = haystack.IndexOf(needle, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
