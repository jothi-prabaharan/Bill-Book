using Ganss.Xss;

namespace Shared.Kernel.Printing;

/// <summary>What the sanitiser took out of one segment, and why it counts as a refusal.</summary>
public sealed class SegmentViolation
{
    /// <summary>"element", "attribute" or "url".</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>The offending name or value — <c>script</c>, <c>onclick</c>, the blocked URL.</summary>
    public string Detail { get; init; } = string.Empty;

    public override string ToString() => $"{Kind}: {Detail}";
}

/// <summary>Clean markup, plus everything that had to be removed to get there.</summary>
public sealed class SegmentSanitizeResult
{
    public string Html { get; init; } = string.Empty;

    public IReadOnlyList<SegmentViolation> Violations { get; init; } = [];
}

/// <summary>
/// Allow-list sanitiser for segment HTML, applied on <b>write</b>.
///
/// <b>Parsed, not pattern-matched.</b> This wraps Ganss.Xss, which builds a real
/// DOM through AngleSharp and re-serialises what survives the allow-list. A
/// regex over markup cannot do this job: it has no way to know that
/// <c>&lt;img src="x" onerror=alert(1)&gt;</c> and
/// <c>&lt;img/src="x"/onerror=alert(1)&gt;</c> are the same document, and the
/// list of encodings separating them is not finite.
///
/// <b>The part that breaks quietly is the chips.</b> A placeholder is stored as
/// <c>&lt;span contenteditable="false" class="pt-chip"&gt;«Tag»&lt;/span&gt;</c>,
/// list chips carrying a leading ↻. A stock configuration drops both
/// <c>contenteditable</c> and <c>class</c>, and the editor then loads a template
/// whose merge fields have become ordinary text — no error anywhere, just a
/// document that silently stops merging. Both are on the allow-list on purpose,
/// and a test watches them.
///
/// <b>A refusal means something was removed, never that something was
/// reformatted.</b> AngleSharp re-serialises CSS canonically, so
/// <c>font-weight:bold</c> comes back as <c>font-weight: bold</c> — a difference
/// with no meaning that every browser-authored save would produce. Judging
/// INVALID_SEGMENT_HTML by comparing strings would therefore reject every
/// legitimate save in the product. <see cref="Inspect"/> reports what the
/// sanitiser actually took out, which is the thing worth refusing over.
/// </summary>
public sealed class SegmentSanitizer
{
    /// <summary>
    /// The markup the editor's own toolbar can produce, and nothing else.
    /// <c>font</c> is here because execCommand still emits it for colour and
    /// face; <c>img</c> because a letterhead is an image.
    /// </summary>
    private static readonly string[] Tags =
    [
        "table", "thead", "tbody", "tr", "td", "th",
        "div", "span", "p", "ul", "ol", "li",
        "b", "i", "u", "strike", "font", "br", "img",
    ];

    /// <summary>
    /// The specification names style, colspan, rowspan, align and width. Seven
    /// more are here, each because an allowed tag is inert without it, and each
    /// called out rather than folded in silently:
    ///
    /// <list type="bullet">
    /// <item><c>contenteditable</c> and <c>class</c> — the placeholder chip is defined by them.</item>
    /// <item><c>src</c> and <c>alt</c> — an img with no src draws nothing, and the approved-host rule presupposes a src to check.</item>
    /// <item><c>color</c>, <c>face</c>, <c>size</c> — the font tag carries no meaning without them.</item>
    /// </list>
    /// </summary>
    private static readonly string[] Attributes =
    [
        "style", "colspan", "rowspan", "align", "width",
        "contenteditable", "class",
        "src", "alt",
        "color", "face", "size",
    ];

    private readonly HashSet<string> _allowedImageHosts;

    /// <param name="allowedImageHosts">
    /// Hosts an <c>img src</c> may point at. Empty means relative URLs only,
    /// which is the safe default: the product serves its own uploads.
    /// </param>
    public SegmentSanitizer(IEnumerable<string>? allowedImageHosts = null) =>
        _allowedImageHosts = new HashSet<string>(allowedImageHosts ?? [], StringComparer.OrdinalIgnoreCase);

    /// <summary>Clean markup only. Use <see cref="Inspect"/> when the caller needs to refuse.</summary>
    public string Sanitize(string? html) => Inspect(html).Html;

    /// <summary>
    /// Sanitises and reports what was removed.
    ///
    /// A fresh <see cref="HtmlSanitizer"/> per call, because the removal
    /// callbacks are per-call state and a shared instance would leak one
    /// request's violations into another's.
    /// </summary>
    public SegmentSanitizeResult Inspect(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return new SegmentSanitizeResult { Html = string.Empty };
        }

        var violations = new List<SegmentViolation>();
        HtmlSanitizer sanitizer = Build(violations);

        return new SegmentSanitizeResult
        {
            Html = sanitizer.Sanitize(html),
            Violations = violations,
        };
    }

    /// <summary>
    /// Sanitises all five segments in place and returns everything removed
    /// across them, so a caller can refuse the save with INVALID_SEGMENT_HTML
    /// naming what was wrong rather than just that something was.
    /// </summary>
    public IReadOnlyList<SegmentViolation> SanitizeContent(PrintContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var violations = new List<SegmentViolation>();

        foreach (PrintSegment segment in PrintSegments.InOrder)
        {
            SegmentSanitizeResult result = Inspect(PrintSegments.Html(content, segment));
            PrintSegments.SetHtml(content, segment, result.Html);

            foreach (SegmentViolation violation in result.Violations)
            {
                violations.Add(new SegmentViolation
                {
                    Kind = violation.Kind,
                    Detail = $"{PrintSegments.Code(segment)}: {violation.Detail}",
                });
            }
        }

        return violations;
    }

    private HtmlSanitizer Build(List<SegmentViolation> violations)
    {
        var sanitizer = new HtmlSanitizer();

        sanitizer.AllowedTags.Clear();
        foreach (string tag in Tags)
        {
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Clear();
        foreach (string attribute in Attributes)
        {
            sanitizer.AllowedAttributes.Add(attribute);
        }

        // No data: URIs. An <img src="data:image/svg+xml,..."> is a document
        // with script in it, not a picture, and permitting the scheme would
        // undo the tag allow-list above.
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("https");

        // Ganss.Xss keeps its own CSS property allow-list, which is what stops
        // expression() and url() smuggling a resource back in through style.
        sanitizer.AllowedCssProperties.Remove("position");

        sanitizer.AllowDataAttributes = false;

        sanitizer.RemovingTag += (_, e) =>
            violations.Add(new SegmentViolation { Kind = "element", Detail = e.Tag.NodeName.ToLowerInvariant() });

        sanitizer.RemovingAttribute += (_, e) =>
            violations.Add(new SegmentViolation { Kind = "attribute", Detail = e.Attribute.Name.ToLowerInvariant() });

        sanitizer.RemovingStyle += (_, e) =>
            violations.Add(new SegmentViolation { Kind = "style", Detail = e.Style.Name.ToLowerInvariant() });

        sanitizer.FilterUrl += (_, e) =>
        {
            string? cleaned = SanitizeUrl(e.OriginalUrl, _allowedImageHosts);
            if (cleaned is null && !string.IsNullOrWhiteSpace(e.OriginalUrl))
            {
                violations.Add(new SegmentViolation { Kind = "url", Detail = e.OriginalUrl });
            }

            e.SanitizedUrl = cleaned;
        };

        return sanitizer;
    }

    /// <summary>
    /// Relative URLs pass; absolute ones must be https and name an approved
    /// host. Anything else resolves to null, which drops the attribute.
    /// </summary>
    private static string? SanitizeUrl(string? url, HashSet<string> allowedHosts)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out Uri? parsed))
        {
            return null;
        }

        if (!parsed.IsAbsoluteUri)
        {
            // A protocol-relative "//evil.example/x" is relative to the parser
            // but absolute to a browser, which is exactly the gap worth closing.
            return url.StartsWith("//", StringComparison.Ordinal) ? null : url;
        }

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return allowedHosts.Contains(parsed.Host) ? url : null;
    }
}
