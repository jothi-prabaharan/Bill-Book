using System.Net;
using Shared.Kernel.Documents;

namespace Shared.Kernel.Printing;

/// <summary>
/// The small text rules every service's print payload shares: free text as rich-text markup, an address
/// built from the parts that are present, and the name a tax component prints under. Pure.
/// </summary>
public static class PrintText
{
    /// <summary>
    /// Free text as rich-text markup: escaped, with its line breaks kept. The address and terms placeholders
    /// are rich text, which the renderer sanitises; text typed into a form is not markup, so it is escaped
    /// first rather than trusted to mean what its angle brackets say. Null when nothing is present.
    /// </summary>
    public static string? Lines(params string?[] parts)
    {
        string[] present = [.. parts
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .SelectMany(p => p!.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .Select(p => WebUtility.HtmlEncode(p))];

        return present.Length == 0 ? null : string.Join("<br>", present);
    }

    /// <summary>The parts that are present, joined; null when none are.</summary>
    public static string? JoinNonEmpty(string separator, params string?[] parts)
    {
        string joined = string.Join(separator, parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
        return joined.Length == 0 ? null : joined;
    }

    /// <summary>The name a tax component prints under.</summary>
    public static string ComponentName(TaxComponent component) => component switch
    {
        TaxComponent.Cgst => "CGST",
        TaxComponent.Sgst => "SGST",
        TaxComponent.Igst => "IGST",
        TaxComponent.Utgst => "UTGST",
        TaxComponent.Cess => "Cess",
        _ => component.ToString(),
    };
}
