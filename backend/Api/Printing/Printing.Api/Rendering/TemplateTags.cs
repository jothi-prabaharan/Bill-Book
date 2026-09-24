using Printing.Entity.Models;
using Shared.Kernel.Printing;

namespace Printing.Api.Rendering;

/// <summary>
/// The merge tags in a whole template. The text-level reading — the pattern,
/// one segment's tags, a tag's list group — is <see cref="MergeTags"/> in the
/// shared contract; these read a stored template, which only Printing holds.
/// </summary>
public static class TemplateTags
{
    /// <summary>Every distinct tag across all five segments.</summary>
    public static IReadOnlyList<string> Extract(PrintContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return [.. PrintSegments.InOrder
            .SelectMany(segment => MergeTags.Extract(PrintSegments.Html(content, segment)))
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Tags a document type cannot resolve. The editor shows these as a status
    /// pill; a template carrying any of them is refused activation, because a
    /// tag nothing resolves prints as a blank space on a customer's document.
    /// </summary>
    public static IReadOnlyList<string> Unknown(PrintContent content, string documentTypeCode) =>
        [.. Extract(content).Where(tag => PlaceholderCatalog.Find(documentTypeCode, tag) is null)];
}
