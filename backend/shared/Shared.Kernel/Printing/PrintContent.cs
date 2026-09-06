namespace Shared.Kernel.Printing;

/// <summary>
/// The five segments' HTML, stored as jsonb. Sanitised on write — nothing here
/// is ever trusted on the way out, because it reached the column through an API.
/// </summary>
public class PrintContent
{
    public string FixedHeaderHtml { get; set; } = string.Empty;

    public string HeaderHtml { get; set; } = string.Empty;

    public string DetailsHtml { get; set; } = string.Empty;

    public string FooterHtml { get; set; } = string.Empty;

    public string FixedFooterHtml { get; set; } = string.Empty;
}

/// <summary>
/// Reading and writing <see cref="PrintContent"/> by segment, so no caller has
/// to switch over five properties and risk getting the order wrong.
/// </summary>
public static class PrintSegments
{
    /// <summary>Every segment, in print order. Ordered by the enum's own values — see <see cref="PrintSegment"/>.</summary>
    public static readonly IReadOnlyList<PrintSegment> InOrder =
    [
        PrintSegment.FixedHeader,
        PrintSegment.Header,
        PrintSegment.Details,
        PrintSegment.Footer,
        PrintSegment.FixedFooter,
    ];

    /// <summary>The two-letter codes the editor and the stored JSON use.</summary>
    public static string Code(PrintSegment segment) => segment switch
    {
        PrintSegment.FixedHeader => "fh",
        PrintSegment.Header => "h",
        PrintSegment.Details => "d",
        PrintSegment.Footer => "f",
        PrintSegment.FixedFooter => "ff",
        _ => throw new ArgumentOutOfRangeException(nameof(segment)),
    };

    public static string Html(PrintContent content, PrintSegment segment) => segment switch
    {
        PrintSegment.FixedHeader => content.FixedHeaderHtml,
        PrintSegment.Header => content.HeaderHtml,
        PrintSegment.Details => content.DetailsHtml,
        PrintSegment.Footer => content.FooterHtml,
        PrintSegment.FixedFooter => content.FixedFooterHtml,
        _ => throw new ArgumentOutOfRangeException(nameof(segment)),
    };

    public static void SetHtml(PrintContent content, PrintSegment segment, string html)
    {
        switch (segment)
        {
            case PrintSegment.FixedHeader: content.FixedHeaderHtml = html; break;
            case PrintSegment.Header: content.HeaderHtml = html; break;
            case PrintSegment.Details: content.DetailsHtml = html; break;
            case PrintSegment.Footer: content.FooterHtml = html; break;
            case PrintSegment.FixedFooter: content.FixedFooterHtml = html; break;
            default: throw new ArgumentOutOfRangeException(nameof(segment));
        }
    }

    public static SegmentMargin Margin(SegmentMargins margins, PrintSegment segment) => segment switch
    {
        PrintSegment.FixedHeader => margins.FixedHeader,
        PrintSegment.Header => margins.Header,
        PrintSegment.Details => margins.Details,
        PrintSegment.Footer => margins.Footer,
        PrintSegment.FixedFooter => margins.FixedFooter,
        _ => throw new ArgumentOutOfRangeException(nameof(segment)),
    };
}
