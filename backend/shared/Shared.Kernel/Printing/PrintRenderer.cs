using System.Globalization;
using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;

namespace Shared.Kernel.Printing;

/// <summary>Everything one render needs. Nothing here touches a database.</summary>
public sealed class PrintRenderRequest
{
    public PrintSettings Settings { get; init; } = new();

    public PrintContent Content { get; init; } = new();

    public string DocumentTypeCode { get; init; } = string.Empty;

    public PrintPayload Payload { get; init; } = new();

    public PrintFormatContext Format { get; init; } = PrintFormatContext.Default;

    public PrintMetrics Metrics { get; init; } = PrintMetrics.Default;

    /// <summary>Hosts an image placeholder may resolve to. Passed to the sanitiser.</summary>
    public IReadOnlyList<string> AllowedImageHosts { get; init; } = [];
}

public sealed class PrintRenderResult
{
    public string Html { get; init; } = string.Empty;

    public int PageCount { get; init; }

    /// <summary>Tags nothing could resolve. Each printed as nothing, each logged.</summary>
    public IReadOnlyList<string> UnknownTags { get; init; } = [];
}

/// <summary>
/// Lays a template and a document out into pages.
///
/// <b>Which segment goes where, and how often.</b> The fixed header flows on
/// page one and is stamped as a band on every page after it. The header and
/// details flow. The footer appears <i>once</i>, on the last page, and when it
/// is pinned its height is reserved on that page alone rather than on all of
/// them — reserving it everywhere would silently shorten every page in a long
/// document. The fixed footer appears on every page.
///
/// <b>Breaks fall between whole rows.</b> A table is chunked into its rows and
/// packed row by row, so a page break can never land inside one. Where a table
/// is cut, both halves carry a hairline rule so a split table still reads as
/// bounded.
///
/// <b>A thermal roll is not paginated at all</b> — it is one continuous page of
/// the roll's width, with no repeated bands, because there are no page edges to
/// repeat anything at.
/// </summary>
public sealed class PrintRenderer
{
    private readonly ILogger<PrintRenderer>? _log;

    public PrintRenderer(ILogger<PrintRenderer>? log = null) => _log = log;

    public PrintRenderResult Render(PrintRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sanitizer = new SegmentSanitizer(request.AllowedImageHosts);
        PrintSettings settings = request.Settings;
        PrintMetrics metrics = request.Metrics;

        var unknown = new List<string>();
        Dictionary<PrintSegment, string> resolved = [];

        foreach (PrintSegment segment in PrintSegments.InOrder)
        {
            SubstitutionResult result = PrintSubstitution.Resolve(
                PrintSegments.Html(request.Content, segment),
                request.DocumentTypeCode,
                request.Payload,
                request.Format,
                sanitizer);

            resolved[segment] = result.Html;
            unknown.AddRange(result.UnknownTags);
        }

        foreach (string tag in unknown.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _log?.LogWarning(
                "Print template for {DocumentType} references {Tag}, which resolves to nothing.",
                request.DocumentTypeCode,
                tag);
        }

        (double paperWidthMm, double paperHeightMm) = PrintGeometry.Paper(settings);
        double contentWidthMm = paperWidthMm - settings.MarginLeftMm - settings.MarginRightMm;

        (string html, int pageCount) = settings.PrinterType == PrinterType.Thermal
            ? (RenderContinuous(resolved, settings, paperWidthMm, contentWidthMm), 1)
            : RenderPaged(resolved, settings, metrics, paperWidthMm, paperHeightMm, contentWidthMm);

        return new PrintRenderResult
        {
            Html = html,
            PageCount = pageCount,
            UnknownTags = [.. unknown.Distinct(StringComparer.OrdinalIgnoreCase)],
        };
    }

    /// <summary>A thermal roll: one page, everything in segment order, no repeats.</summary>
    private static string RenderContinuous(
        Dictionary<PrintSegment, string> resolved,
        PrintSettings settings,
        double paperWidthMm,
        double contentWidthMm)
    {
        var body = new StringBuilder();

        foreach (PrintSegment segment in PrintSegments.InOrder)
        {
            SegmentMargin margin = PrintSegments.Margin(settings.SegMargins, segment);
            body.Append(SegmentDiv(segment, resolved[segment], margin));
        }

        var html = new StringBuilder();
        html.Append(Styles());
        html.Append(CultureInfo.InvariantCulture, $"<div class=\"pt-doc pt-continuous\" style=\"width:{Px(paperWidthMm)}\">");
        html.Append(CultureInfo.InvariantCulture, $"<section class=\"pt-page\" style=\"width:{Px(paperWidthMm)};padding:{Padding(settings)}\">");
        html.Append(CultureInfo.InvariantCulture, $"<div class=\"pt-flow\" style=\"width:{Px(contentWidthMm)}\">");
        html.Append(body);
        html.Append("</div></section></div>");

        return html.ToString();
    }

    private static (string Html, int PageCount) RenderPaged(
        Dictionary<PrintSegment, string> resolved,
        PrintSettings settings,
        PrintMetrics metrics,
        double paperWidthMm,
        double paperHeightMm,
        double contentWidthMm)
    {
        double fixedHeaderMm = BandHeight(resolved[PrintSegment.FixedHeader], PrintSegment.FixedHeader, settings, metrics, contentWidthMm);
        double fixedFooterMm = BandHeight(resolved[PrintSegment.FixedFooter], PrintSegment.FixedFooter, settings, metrics, contentWidthMm);
        double footerMm = BandHeight(resolved[PrintSegment.Footer], PrintSegment.Footer, settings, metrics, contentWidthMm);

        bool footerPinned = settings.FooterPos == SegmentPosition.Bottom;
        bool fixedFooterPinned = settings.FixedFooterPos == SegmentPosition.Bottom;

        // The flow, in segment order. The fixed header is part of it on page one;
        // pages after that get a band instead.
        var flow = new List<Chunk>();
        flow.AddRange(Chunks(resolved[PrintSegment.FixedHeader], PrintSegment.FixedHeader, settings, metrics, contentWidthMm));
        flow.AddRange(Chunks(resolved[PrintSegment.Header], PrintSegment.Header, settings, metrics, contentWidthMm));
        flow.AddRange(Chunks(resolved[PrintSegment.Details], PrintSegment.Details, settings, metrics, contentWidthMm));

        if (!footerPinned)
        {
            flow.AddRange(Chunks(resolved[PrintSegment.Footer], PrintSegment.Footer, settings, metrics, contentWidthMm));
        }

        if (!fixedFooterPinned)
        {
            flow.AddRange(Chunks(resolved[PrintSegment.FixedFooter], PrintSegment.FixedFooter, settings, metrics, contentWidthMm));
        }

        double usableBase = paperHeightMm - settings.MarginTopMm - settings.MarginBottomMm - fixedFooterMm;

        // The fixed footer band is reserved on every page even when it is
        // inline, where it is only drawn on pages before the last. Reserving it
        // uniformly costs a few millimetres on the final page and keeps the
        // packing a single pass, which is what makes the page count identical
        // across two runs of the same document.
        List<List<Chunk>> pages = Pack(flow, usableBase, fixedHeaderMm);

        if (footerPinned)
        {
            double used = pages[^1].Sum(c => c.HeightMm);
            double usable = usableBase - (pages.Count > 1 ? fixedHeaderMm : 0);

            if (used + footerMm > usable)
            {
                pages.Add([]);
            }
        }

        string html = Emit(
            pages, resolved, settings, paperWidthMm, paperHeightMm, contentWidthMm, fixedFooterPinned, footerPinned);

        return (html, pages.Count);
    }

    /// <summary>
    /// Greedy first-fit down the flow. A chunk taller than a whole page gets a
    /// page of its own rather than being split — a table row is indivisible, and
    /// a row too tall to fit is a template problem, not a pagination one.
    /// </summary>
    private static List<List<Chunk>> Pack(List<Chunk> flow, double usableBase, double fixedHeaderMm)
    {
        List<List<Chunk>> pages = [[]];
        double used = 0;

        foreach (Chunk chunk in flow)
        {
            double usable = usableBase - (pages.Count > 1 ? fixedHeaderMm : 0);

            if (pages[^1].Count > 0 && used + chunk.HeightMm > usable)
            {
                pages.Add([]);
                used = 0;
            }

            pages[^1].Add(chunk);
            used += chunk.HeightMm;
        }

        return pages;
    }

    private static string Emit(
        List<List<Chunk>> pages,
        Dictionary<PrintSegment, string> resolved,
        PrintSettings settings,
        double paperWidthMm,
        double paperHeightMm,
        double contentWidthMm,
        bool fixedFooterPinned,
        bool footerPinned)
    {
        var html = new StringBuilder();
        html.Append(Styles());
        html.Append(CultureInfo.InvariantCulture, $"<div class=\"pt-doc\" style=\"width:{Px(paperWidthMm)}\">");

        for (int index = 0; index < pages.Count; index++)
        {
            bool isLast = index == pages.Count - 1;
            bool isFirst = index == 0;

            html.Append(CultureInfo.InvariantCulture,
                $"<section class=\"pt-page\" style=\"width:{Px(paperWidthMm)};height:{Px(paperHeightMm)};padding:{Padding(settings)}\">");

            // The fixed header flows on page one and repeats as a band after it.
            if (!isFirst)
            {
                html.Append(Band("pt-fh", resolved[PrintSegment.FixedHeader], PrintSegment.FixedHeader, settings));
            }

            html.Append(CultureInfo.InvariantCulture, $"<div class=\"pt-flow\" style=\"width:{Px(contentWidthMm)}\">");
            html.Append(Body(pages[index]));
            html.Append("</div>");

            // Both pinned segments go into one block at the bottom, in segment
            // order, so a pinned footer sits directly above a pinned fixed
            // footer rather than on top of it. Positioning them separately at
            // bottom:0 would overlap them, and the order the format guarantees
            // would be unreadable rather than merely wrong.
            var pinned = new StringBuilder();

            // The footer appears once, on the last page.
            if (footerPinned && isLast)
            {
                pinned.Append(Band("pt-f", resolved[PrintSegment.Footer], PrintSegment.Footer, settings));
            }

            // The fixed footer is on every page. Pinned, it sits at the page
            // edge; inline, it flowed onto the last page already, so only the
            // pages before it need the band.
            if (fixedFooterPinned || !isLast)
            {
                pinned.Append(Band("pt-ff", resolved[PrintSegment.FixedFooter], PrintSegment.FixedFooter, settings));
            }

            if (pinned.Length > 0)
            {
                html.Append(CultureInfo.InvariantCulture, $"<div class=\"pt-pinned\">{pinned}</div>");
            }

            html.Append("</section>");
        }

        return html.Append("</div>").ToString();
    }

    /// <summary>
    /// Re-assembles a page's chunks, merging consecutive rows of one table back
    /// into a table and marking the cuts.
    /// </summary>
    private static string Body(List<Chunk> chunks)
    {
        var html = new StringBuilder();
        string? openTable = null;

        for (int i = 0; i < chunks.Count; i++)
        {
            Chunk chunk = chunks[i];

            if (chunk.TableKey is null)
            {
                if (openTable is not null)
                {
                    html.Append("</tbody></table>");
                    openTable = null;
                }

                html.Append(chunk.Html);
                continue;
            }

            bool continues = openTable == chunk.TableKey;
            if (!continues)
            {
                if (openTable is not null)
                {
                    html.Append("</tbody></table>");
                }

                // A table that did not start on this page is a continuation, and
                // says so with a rule along its top edge.
                bool isContinuation = !chunk.IsFirstRowOfTable;
                html.Append(chunk.TableOpen(isContinuation));
                openTable = chunk.TableKey;
            }

            html.Append(chunk.Html);

            bool lastOnPage = i == chunks.Count - 1;
            bool tableEndsHere = lastOnPage || chunks[i + 1].TableKey != chunk.TableKey;

            if (tableEndsHere)
            {
                html.Append("</tbody></table>");
                openTable = null;

                // Cut short by the page edge rather than finished: rule the foot
                // of it so the reader can see the table carries on.
                if (lastOnPage && !chunk.IsLastRowOfTable)
                {
                    html.Append("<div class=\"pt-cut\"></div>");
                }
            }
        }

        if (openTable is not null)
        {
            html.Append("</tbody></table>");
        }

        return html.ToString();
    }

    private static string Band(string cssClass, string html, PrintSegment segment, PrintSettings settings)
    {
        SegmentMargin margin = PrintSegments.Margin(settings.SegMargins, segment);
        return string.Format(
            CultureInfo.InvariantCulture,
            "<div class=\"pt-band {0}\" style=\"margin-top:{1};margin-bottom:{2}\">{3}</div>",
            cssClass,
            Px(margin.AboveMm),
            Px(margin.BelowMm),
            html);
    }

    private static string SegmentDiv(PrintSegment segment, string html, SegmentMargin margin) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "<div class=\"pt-seg pt-{0}\" style=\"margin-top:{1};margin-bottom:{2}\">{3}</div>",
            PrintSegments.Code(segment),
            Px(margin.AboveMm),
            Px(margin.BelowMm),
            html);

    private static double BandHeight(
        string html, PrintSegment segment, PrintSettings settings, PrintMetrics metrics, double contentWidthMm)
    {
        SegmentMargin margin = PrintSegments.Margin(settings.SegMargins, segment);
        return Chunks(html, segment, settings, metrics, contentWidthMm).Sum(c => c.HeightMm)
            + margin.AboveMm + margin.BelowMm;
    }

    /// <summary>
    /// Breaks one segment into the smallest pieces a page break may fall
    /// between: a table becomes one chunk per row, everything else one chunk per
    /// top-level block.
    /// </summary>
    private static List<Chunk> Chunks(
        string html, PrintSegment segment, PrintSettings settings, PrintMetrics metrics, double contentWidthMm)
    {
        var chunks = new List<Chunk>();

        if (string.IsNullOrWhiteSpace(html))
        {
            return chunks;
        }

        SegmentMargin margin = PrintSegments.Margin(settings.SegMargins, segment);
        IHtmlDocument document = new HtmlParser().ParseDocument($"<body>{html}</body>");
        IElement? body = document.Body;

        if (body is null)
        {
            return chunks;
        }

        string code = PrintSegments.Code(segment);
        int tableIndex = 0;

        foreach (IElement element in body.Children)
        {
            if (!string.Equals(element.TagName, "TABLE", StringComparison.OrdinalIgnoreCase))
            {
                chunks.Add(new Chunk
                {
                    Html = element.OuterHtml,
                    HeightMm = metrics.TextHeightMm(element.TextContent, contentWidthMm),
                });
                continue;
            }

            string key = $"{code}-{tableIndex++}";
            string attributes = Attributes(element);
            string head = element.QuerySelector("thead")?.OuterHtml ?? string.Empty;
            // This table's own rows, not every row beneath it. A descendant
            // selector reaches into nested tables, and a nested row picked up
            // here would be emitted twice — once inside its wrapper row's markup
            // and again as a chunk of its own. Closest("table") is the same test
            // the substitution rule uses to decide which row a chip belongs to.
            List<IElement> rows = [.. element.QuerySelectorAll("tr").Where(r => r.Closest("table") == element)];

            for (int i = 0; i < rows.Count; i++)
            {
                chunks.Add(new Chunk
                {
                    Html = rows[i].OuterHtml,
                    HeightMm = metrics.TextHeightMm(rows[i].TextContent, contentWidthMm),
                    TableKey = key,
                    TableAttributes = attributes,
                    TableHead = head,
                    IsFirstRowOfTable = i == 0,
                    IsLastRowOfTable = i == rows.Count - 1,
                });
            }
        }

        // Segment spacing counts toward the flow, so it is charged to the first
        // and last chunk rather than added invisibly outside the packing.
        if (chunks.Count > 0)
        {
            chunks[0].HeightMm += margin.AboveMm;
            chunks[^1].HeightMm += margin.BelowMm;
        }

        return chunks;
    }

    private static string Attributes(IElement element) =>
        string.Concat(element.Attributes.Select(a => $" {a.Name}=\"{a.Value}\""));

    private static string Px(double millimetres) =>
        string.Create(CultureInfo.InvariantCulture, $"{PrintGeometry.ToPx(millimetres):0.##}px");

    private static string Padding(PrintSettings settings) =>
        string.Join(' ', new[]
        {
            Px(settings.MarginTopMm), Px(settings.MarginRightMm),
            Px(settings.MarginBottomMm), Px(settings.MarginLeftMm),
        });

    /// <summary>
    /// The page frame. Everything positional lives here rather than on elements,
    /// so a template's own styles cannot fight it.
    /// </summary>
    private static string Styles() =>
        "<style>"
        + ".pt-doc{margin:0 auto}"
        + ".pt-page{position:relative;box-sizing:border-box;overflow:hidden;background:#fff}"
        + ".pt-continuous .pt-page{height:auto}"
        + ".pt-flow{position:relative}"
        + ".pt-pinned{position:absolute;left:0;right:0;bottom:0}"
        + ".pt-cut{border-top:0.5pt solid currentColor;opacity:0.5}"
        + ".pt-page table{width:100%;border-collapse:collapse}"
        + "</style>";

    /// <summary>One indivisible piece of the flow.</summary>
    private sealed class Chunk
    {
        public string Html { get; init; } = string.Empty;

        public double HeightMm { get; set; }

        /// <summary>Non-null when this chunk is one row of a table, identifying which.</summary>
        public string? TableKey { get; init; }

        public string TableAttributes { get; init; } = string.Empty;

        public string TableHead { get; init; } = string.Empty;

        public bool IsFirstRowOfTable { get; init; }

        public bool IsLastRowOfTable { get; init; }

        /// <summary>
        /// The opening markup for the table this row belongs to. A continuation
        /// gets a rule along its top edge and no repeated head — the rule is
        /// what tells the reader the table was cut rather than started here.
        /// </summary>
        public string TableOpen(bool isContinuation) =>
            (isContinuation ? "<div class=\"pt-cut\"></div>" : string.Empty)
            + $"<table{TableAttributes}>"
            + (isContinuation ? string.Empty : TableHead)
            + "<tbody>";
    }
}
