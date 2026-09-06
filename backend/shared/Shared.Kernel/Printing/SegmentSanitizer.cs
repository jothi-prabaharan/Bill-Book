using Ganss.Xss;

namespace Shared.Kernel.Printing;

/// <summary>
/// Allow-list sanitiser for segment HTML, applied on <b>write</b>.
///
/// <b>Parsed, not pattern-matched.</b> This wraps Ganss.Xss, which builds a real
/// DOM through AngleSharp and re-serialises what survives the allow-list. A
/// regex over markup cannot do this job: it has no way to know that
/// <c>&lt;img src="x" onerror=alert(1)&gt;</c> and
/// <c>&lt;img/src="x"/onerror=alert(1)&gt;</c> are the same document, and the
/// list of encodings that separate them is not finite.
///
/// <b>The part that breaks quietly is the chips.</b> A placeholder is stored as
/// <c>&lt;span contenteditable="false" class="pt-chip"&gt;«Tag»&gt;/span&gt;</c>,
/// list chips carrying a leading ↻. A stock sanitiser configuration drops both
/// <c>contenteditable</c> and <c>class</c>, and the editor then loads a template
/// whose merge fields have become ordinary text — no error anywhere, just a
/// document that silently stops merging. Both are on the allow-list below on
/// purpose, and a test watches them.
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
    /// The specification names style, colspan, rowspan, align and width. Four
    /// more are here, each because an allowed tag is inert without it, and each
    /// is called out rather than folded in silently:
    ///
    /// <list type="bullet">
    /// <item><c>contenteditable</c> and <c>class</c> — the placeholder chip is defined by them.</item>
    /// <item><c>src</c> and <c>alt</c> — an img tag with no src draws nothing, and the rule about approved image hosts presupposes a src to check.</item>
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

    private readonly HtmlSanitizer _sanitizer;

    /// <param name="allowedImageHosts">
    /// Hosts an <c>img src</c> may point at. Empty means relative URLs only,
    /// which is the safe default: the product serves its own uploads.
    /// </param>
    public SegmentSanitizer(IEnumerable<string>? allowedImageHosts = null)
    {
        HashSet<string> hosts = new(allowedImageHosts ?? [], StringComparer.OrdinalIgnoreCase);

        _sanitizer = new HtmlSanitizer();

        _sanitizer.AllowedTags.Clear();
        foreach (string tag in Tags)
        {
            _sanitizer.AllowedTags.Add(tag);
        }

        _sanitizer.AllowedAttributes.Clear();
        foreach (string attribute in Attributes)
        {
            _sanitizer.AllowedAttributes.Add(attribute);
        }

        // No data: URIs. An <img src="data:image/svg+xml,..."> is a document
        // with script in it, not a picture, and permitting the scheme would
        // undo the tag allow-list above.
        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("https");

        // Ganss.Xss keeps its own CSS property allow-list, which is what stops
        // expression() and url() smuggling a resource back in through style.
        _sanitizer.AllowedCssProperties.Remove("position");

        _sanitizer.AllowDataAttributes = false;

        _sanitizer.FilterUrl += (_, e) =>
        {
            e.SanitizedUrl = SanitizeUrl(e.OriginalUrl, hosts);
        };
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

    public string Sanitize(string? html) =>
        string.IsNullOrEmpty(html) ? string.Empty : _sanitizer.Sanitize(html);

    /// <summary>
    /// Sanitises all five segments in place. Returns the segments whose markup
    /// the sanitiser actually changed, so a caller can refuse the save with
    /// <c>INVALID_SEGMENT_HTML</c> rather than storing a quietly different
    /// document than the one that was sent.
    /// </summary>
    public IReadOnlyList<PrintSegment> SanitizeContent(PrintContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var changed = new List<PrintSegment>();

        foreach (PrintSegment segment in PrintSegments.InOrder)
        {
            string original = PrintSegments.Html(content, segment);
            string clean = Sanitize(original);

            if (!string.Equals(original, clean, StringComparison.Ordinal))
            {
                changed.Add(segment);
            }

            PrintSegments.SetHtml(content, segment, clean);
        }

        return changed;
    }
}
