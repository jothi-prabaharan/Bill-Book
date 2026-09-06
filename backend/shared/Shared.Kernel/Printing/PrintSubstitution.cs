using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace Shared.Kernel.Printing;

/// <summary>Resolved segment markup, plus the tags nothing could answer.</summary>
public sealed class SubstitutionResult
{
    public string Html { get; init; } = string.Empty;

    public IReadOnlyList<string> UnknownTags { get; init; } = [];
}

/// <summary>
/// Replaces merge chips with values, repeating the rows that carry list chips.
///
/// <b>The repeat rule, stated once so it cannot drift:</b> a row repeats for
/// exactly those list chips whose <i>closest</i> ancestor row is that row. A
/// chip sitting inside a nested table belongs to the inner row, not the outer
/// one, so a row that merely wraps other tables has no chips of its own and
/// never repeats.
///
/// That is not a special case bolted on: it is what "only the innermost row
/// repeats" means, expressed the way the DOM already models it. Reading it any
/// other way is what once printed a totals block once per item line — 42 copies
/// on a 42-line invoice.
/// </summary>
public static class PrintSubstitution
{
    private const string ChipSelector = "span.pt-chip";

    public static SubstitutionResult Resolve(
        string? html,
        string documentTypeCode,
        PrintPayload payload,
        PrintFormatContext format,
        SegmentSanitizer sanitizer)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(sanitizer);

        if (string.IsNullOrWhiteSpace(html))
        {
            return new SubstitutionResult();
        }

        var parser = new HtmlParser();
        IHtmlDocument document = parser.ParseDocument($"<body>{html}</body>");
        var unknown = new List<string>();

        ExpandRepeatingRows(document, documentTypeCode, payload, format, sanitizer, unknown);

        // Whatever chips remain are singles, wherever they sit.
        foreach (IElement chip in document.QuerySelectorAll(ChipSelector).ToList())
        {
            ReplaceChip(chip, documentTypeCode, payload.Singles, format, sanitizer, unknown);
        }

        return new SubstitutionResult
        {
            Html = document.Body?.InnerHtml ?? string.Empty,
            UnknownTags = unknown,
        };
    }

    private static void ExpandRepeatingRows(
        IHtmlDocument document,
        string documentTypeCode,
        PrintPayload payload,
        PrintFormatContext format,
        SegmentSanitizer sanitizer,
        List<string> unknown)
    {
        // Deepest rows first, so an inner table is expanded before any outer row
        // that contains it is cloned.
        List<IElement> rows = [.. document.QuerySelectorAll("tr").OrderByDescending(Depth)];

        foreach (IElement row in rows)
        {
            if (row.Parent is null)
            {
                // Removed already, as part of a row expanded above it.
                continue;
            }

            string? group = OwnListGroup(row);
            if (group is null)
            {
                continue;
            }

            IReadOnlyList<IReadOnlyDictionary<string, object?>> data = payload.Rows(group);
            INode parent = row.Parent;

            // No rows means no line. Printing one blank row instead would look
            // like a line item nobody can account for.
            foreach (IReadOnlyDictionary<string, object?> values in data)
            {
                var clone = (IElement)row.Clone(deep: true);

                foreach (IElement chip in clone.QuerySelectorAll(ChipSelector).ToList())
                {
                    // Inside the clone, this row's own chips take the row's
                    // values; anything belonging to a nested row is left for the
                    // singles pass, which is where it was already resolved.
                    if (ClosestRow(chip) == clone)
                    {
                        ReplaceChip(chip, documentTypeCode, values, format, sanitizer, unknown, fallbackToShortName: true);
                    }
                }

                parent.InsertBefore(clone, row);
            }

            parent.RemoveChild(row);
        }
    }

    /// <summary>
    /// The list this row repeats for, or null. Only chips whose closest row is
    /// this one count — see the type comment.
    /// </summary>
    private static string? OwnListGroup(IElement row)
    {
        foreach (IElement chip in row.QuerySelectorAll(ChipSelector))
        {
            if (ClosestRow(chip) != row)
            {
                continue;
            }

            string? group = MergeTags.ListGroup(TagOf(chip));
            if (group is not null)
            {
                return group;
            }
        }

        return null;
    }

    private static IElement? ClosestRow(IElement element) => element.Closest("tr");

    private static int Depth(IElement element)
    {
        int depth = 0;
        for (IElement? node = element.ParentElement; node is not null; node = node.ParentElement)
        {
            depth++;
        }

        return depth;
    }

    private static string TagOf(IElement chip) =>
        chip.TextContent.Trim().Trim('«', '»').TrimStart(MergeTags.ListMarker).Trim('«', '»').Trim();

    private static void ReplaceChip(
        IElement chip,
        string documentTypeCode,
        IReadOnlyDictionary<string, object?> values,
        PrintFormatContext format,
        SegmentSanitizer sanitizer,
        List<string> unknown,
        bool fallbackToShortName = false)
    {
        string tag = TagOf(chip);
        PlaceholderDefinition? placeholder = PlaceholderCatalog.Find(documentTypeCode, tag);

        if (placeholder is null)
        {
            // Never print the raw tag. A customer's document showing
            // «Something.Unknown» is worse than one showing nothing there.
            unknown.Add(tag);
            Replace(chip, string.Empty);
            return;
        }

        object? value = Lookup(values, tag, fallbackToShortName);

        switch (placeholder.Type)
        {
            case PlaceholderType.Image:
                string? url = value as string;
                Replace(
                    chip,
                    string.IsNullOrWhiteSpace(url)
                        ? string.Empty
                        : sanitizer.Sanitize($"<img src=\"{Escape(url)}\" alt=\"\">"));
                break;

            case PlaceholderType.RichText:
                Replace(chip, sanitizer.Sanitize(value as string ?? string.Empty));
                break;

            default:
                Replace(chip, Escape(MaskFormatter.Format(value, placeholder, format)));
                break;
        }
    }

    /// <summary>
    /// A row may key its values by the whole tag or by the field alone —
    /// <c>Item.Rate</c> or <c>Rate</c>. Both are accepted so a payload builder
    /// cannot get this subtly wrong and produce a document of blanks.
    /// </summary>
    private static object? Lookup(IReadOnlyDictionary<string, object?> values, string tag, bool allowShortName)
    {
        if (values.TryGetValue(tag, out object? value))
        {
            return value;
        }

        if (!allowShortName)
        {
            return null;
        }

        int dot = tag.IndexOf('.', StringComparison.Ordinal);
        return dot >= 0 && values.TryGetValue(tag[(dot + 1)..], out object? shortValue) ? shortValue : null;
    }

    private static void Replace(IElement chip, string html)
    {
        if (html.Length == 0)
        {
            chip.Remove();
            return;
        }

        chip.OuterHtml = html;
    }

    private static string Escape(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (char character in text)
        {
            _ = character switch
            {
                '&' => builder.Append("&amp;"),
                '<' => builder.Append("&lt;"),
                '>' => builder.Append("&gt;"),
                '"' => builder.Append("&quot;"),
                _ => builder.Append(character),
            };
        }

        return builder.ToString();
    }
}
