using Shared.Kernel.Printing;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// Acceptance test 5.
///
/// Two halves, and the second is the one that fails silently. Stripping script
/// is what a sanitiser is for and it fails loudly if it regresses. Keeping the
/// placeholder chips intact is a configuration choice — a stock allow-list
/// drops contenteditable and class, and the only symptom is a template whose
/// merge fields quietly stopped merging.
/// </summary>
public class SegmentSanitizerTests
{
    private const string Chip = "<span contenteditable=\"false\" class=\"pt-chip\">«Invoice.No»</span>";
    private const string ListChip = "<span contenteditable=\"false\" class=\"pt-chip\">↻«Item.ItemName»</span>";

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
    public void A_placeholder_chip_survives_unchanged()
    {
        string clean = Sanitizer.Sanitize(Chip);

        Assert.Contains("contenteditable=\"false\"", clean, StringComparison.Ordinal);
        Assert.Contains("class=\"pt-chip\"", clean, StringComparison.Ordinal);
        Assert.Contains("«Invoice.No»", clean, StringComparison.Ordinal);
    }

    [Fact]
    public void A_list_chip_keeps_its_repeat_marker()
    {
        string clean = Sanitizer.Sanitize(ListChip);

        Assert.Contains("↻", clean, StringComparison.Ordinal);
        Assert.Contains("«Item.ItemName»", clean, StringComparison.Ordinal);
        Assert.Contains("contenteditable=\"false\"", clean, StringComparison.Ordinal);
    }

    [Fact]
    public void Chips_survive_sitting_inside_a_hostile_document()
    {
        string html = $"<table><tr><td>{ListChip}<script>alert(1)</script></td>"
            + $"<td onclick=\"x()\">{Chip}</td></tr></table>";

        string clean = Sanitizer.Sanitize(html);

        Assert.DoesNotContain("script", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", clean, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("«Item.ItemName»", clean, StringComparison.Ordinal);
        Assert.Contains("«Invoice.No»", clean, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(clean, "contenteditable=\"false\""));
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
