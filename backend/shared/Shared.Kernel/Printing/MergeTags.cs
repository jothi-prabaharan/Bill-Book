using System.Text.RegularExpressions;

namespace Shared.Kernel.Printing;

/// <summary>
/// Reads the merge tags out of segment HTML.
///
/// <b>A placeholder is <c>{{Tag}}</c> in the stored markup, and nothing else.</b>
/// No wrapper element, no class, no attribute — the editor draws it as a chip
/// on load and writes it back as plain text on save. That is what makes
/// merging independent of the sanitiser: an allow-list can strip every
/// attribute in the document and the tags still resolve.
///
/// A regex is right for this and wrong for sanitising, and the difference is
/// worth being explicit about: a tag is delimited text with no markup meaning,
/// while deciding whether markup is <i>dangerous</i> is the opposite kind of
/// problem — which is why SegmentSanitizer parses instead.
/// </summary>
public static partial class MergeTags
{
    /// <summary>
    /// <c>{{Tag}}</c>. The inner pattern excludes braces, so an unclosed or
    /// nested brace fails to match rather than swallowing the rest of the
    /// document looking for a delimiter.
    /// </summary>
    [GeneratedRegex(@"\{\{([^{}]+)\}\}", RegexOptions.CultureInvariant)]
    public static partial Regex TagPattern { get; }

    /// <summary>Every tag in the order it appears. Duplicates kept.</summary>
    public static IReadOnlyList<string> Extract(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return [];
        }

        return [.. TagPattern.Matches(html).Select(m => m.Groups[1].Value.Trim())];
    }

    /// <summary>Every distinct tag across all five segments.</summary>
    public static IReadOnlyList<string> Extract(PrintContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return [.. PrintSegments.InOrder
            .SelectMany(segment => Extract(PrintSegments.Html(content, segment)))
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Tags a document type cannot resolve. The editor shows these as a status
    /// pill; a template carrying any of them is refused activation, because a
    /// tag nothing resolves prints as a blank space on a customer's document.
    /// </summary>
    public static IReadOnlyList<string> Unknown(PrintContent content, string documentTypeCode) =>
        [.. Extract(content).Where(tag => PlaceholderCatalog.Find(documentTypeCode, tag) is null)];

    /// <summary>The collection a list tag draws from — the part before the dot. Null for a single.</summary>
    public static string? ListGroup(string tag)
    {
        int dot = tag.IndexOf('.', StringComparison.Ordinal);
        if (dot <= 0)
        {
            return null;
        }

        string prefix = tag[..dot];
        return PlaceholderCatalog.ListGroups.Contains(prefix, StringComparer.OrdinalIgnoreCase) ? prefix : null;
    }
}
