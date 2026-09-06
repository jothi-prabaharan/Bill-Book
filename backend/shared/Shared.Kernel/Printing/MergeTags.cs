using System.Text.RegularExpressions;

namespace Shared.Kernel.Printing;

/// <summary>
/// Reads the merge tags out of segment HTML.
///
/// A regex is right for this and wrong for sanitising, and the difference is
/// worth being explicit about: here the delimiters are guillemets, which are
/// literal text with no markup meaning and no alternative encoding that a
/// browser would still honour. Deciding whether markup is <i>dangerous</i> is
/// the opposite kind of problem, and that is why SegmentSanitizer parses.
/// </summary>
public static partial class MergeTags
{
    /// <summary>The repeat marker a list chip carries in front of its tag.</summary>
    public const char ListMarker = '↻';

    [GeneratedRegex("«([^«»]+)»", RegexOptions.CultureInvariant)]
    private static partial Regex TagPattern { get; }

    /// <summary>Every tag in the order it appears, with the list marker stripped. Duplicates kept.</summary>
    public static IReadOnlyList<string> Extract(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return [];
        }

        return [.. TagPattern.Matches(html).Select(m => m.Groups[1].Value.TrimStart(ListMarker).Trim())];
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
