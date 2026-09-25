using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Shared.Kernel.Printing;

namespace Printing.Api.Rendering;

/// <summary>Resolved segment markup, plus the tags nothing could answer.</summary>
public sealed class SubstitutionResult
{
    public string Html { get; init; } = string.Empty;

    public IReadOnlyList<string> UnknownTags { get; init; } = [];
}

/// <summary>
/// Replaces <c>{{Tag}}</c> with values, repeating the rows that carry list tags.
///
/// <b>A placeholder is text, not an element.</b> It is written into the markup
/// as <c>{{Item.ItemName}}</c> and carries no wrapper, class or attribute, so
/// substitution walks text nodes rather than elements. The consequence worth
/// having: merging cannot be broken by a sanitiser, an editor, or anything else
/// that rewrites attributes — the only way to lose a tag is to delete its text.
///
/// <b>The repeat rule, stated once so it cannot drift:</b> a row repeats for
/// exactly those list tags whose <i>closest</i> ancestor row is that row. A tag
/// sitting inside a nested table belongs to the inner row, not the outer one,
/// so a row that merely wraps other tables has no tags of its own and never
/// repeats.
///
/// That is not a special case bolted on: it is what "only the innermost row
/// repeats" means, expressed the way the DOM already models it. Reading it any
/// other way is what once printed a totals block once per item line — 42 copies
/// on a 42-line invoice.
/// </summary>
public static class PrintSubstitution
{
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

        // Whatever tags remain are singles, wherever they sit.
        foreach (IText node in TextNodesWithTags(document.Body))
        {
            ReplaceIn(node, documentTypeCode, payload.Singles, format, sanitizer, unknown, fallbackToShortName: false);
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

            INode parent = row.Parent;

            // No rows means no line. Printing one blank row instead would look
            // like a line item nobody can account for.
            foreach (IReadOnlyDictionary<string, object?> values in payload.Rows(group))
            {
                var clone = (IElement)row.Clone(deep: true);

                foreach (IText node in TextNodesWithTags(clone))
                {
                    // Inside the clone, this row's own tags take the row's
                    // values; anything belonging to a nested row was resolved
                    // already, and anything else is left for the singles pass.
                    if (ClosestRow(node) == clone)
                    {
                        ReplaceIn(node, documentTypeCode, values, format, sanitizer, unknown, fallbackToShortName: true);
                    }
                }

                parent.InsertBefore(clone, row);
            }

            parent.RemoveChild(row);
        }
    }

    /// <summary>
    /// The list this row repeats for, or null. Only tags whose closest row is
    /// this one count — see the type comment.
    /// </summary>
    private static string? OwnListGroup(IElement row)
    {
        foreach (IText node in TextNodesWithTags(row))
        {
            if (ClosestRow(node) != row)
            {
                continue;
            }

            foreach (System.Text.RegularExpressions.Match match in MergeTags.TagPattern.Matches(node.Data))
            {
                string? group = MergeTags.ListGroup(match.Groups[1].Value.Trim());
                if (group is not null)
                {
                    return group;
                }
            }
        }

        return null;
    }

    /// <summary>Every text node under this node that contains at least one tag.</summary>
    private static List<IText> TextNodesWithTags(INode? root)
    {
        var found = new List<IText>();
        if (root is null)
        {
            return found;
        }

        Walk(root);
        return found;

        void Walk(INode node)
        {
            foreach (INode child in node.ChildNodes.ToList())
            {
                if (child is IText text)
                {
                    if (MergeTags.TagPattern.IsMatch(text.Data))
                    {
                        found.Add(text);
                    }
                }
                else
                {
                    Walk(child);
                }
            }
        }
    }

    private static IElement? ClosestRow(INode node) => node.ParentElement?.Closest("tr");

    private static int Depth(IElement element)
    {
        int depth = 0;
        for (IElement? node = element.ParentElement; node is not null; node = node.ParentElement)
        {
            depth++;
        }

        return depth;
    }

    /// <summary>
    /// Replaces every tag in one text node.
    ///
    /// Most values are text and the node's own data is simply rewritten. An
    /// image or a rich-text value is markup, so the node is replaced by the
    /// nodes that markup parses to — which is why this builds an HTML string
    /// and only re-parses when it has to.
    /// </summary>
    private static void ReplaceIn(
        IText node,
        string documentTypeCode,
        IReadOnlyDictionary<string, object?> values,
        PrintFormatContext format,
        SegmentSanitizer sanitizer,
        List<string> unknown,
        bool fallbackToShortName)
    {
        bool anyMarkup = false;

        string replaced = MergeTags.TagPattern.Replace(node.Data, match =>
        {
            string tag = match.Groups[1].Value.Trim();
            PlaceholderDefinition? placeholder = PlaceholderCatalog.Find(documentTypeCode, tag);

            if (placeholder is null)
            {
                // Never print the raw tag. A customer's document showing
                // {{Something.Unknown}} is worse than one showing nothing there.
                unknown.Add(tag);
                return string.Empty;
            }

            object? value = Lookup(values, tag, fallbackToShortName);

            switch (placeholder.Type)
            {
                case PlaceholderType.Image:
                    string? url = value as string;
                    if (string.IsNullOrWhiteSpace(url))
                    {
                        return string.Empty;
                    }

                    anyMarkup = true;
                    return sanitizer.Sanitize($"<img src=\"{Escape(url)}\" alt=\"\">");

                case PlaceholderType.QrCode:
                    // Built here from the text, never taken from the payload as
                    // markup, so it needs no sanitising (see QrImages).
                    string qr = QrImages.Img(value as string);
                    anyMarkup |= qr.Length > 0;
                    return qr;

                case PlaceholderType.RichText:
                    string markup = sanitizer.Sanitize(value as string ?? string.Empty);
                    anyMarkup |= markup.Length > 0;
                    return markup;

                default:
                    return Escape(MaskFormatter.Format(value, placeholder, format));
            }
        });

        if (!anyMarkup)
        {
            // The common case by a long way: no re-parse, no new nodes.
            node.TextContent = Unescape(replaced);
            return;
        }

        IDocument? document = node.Owner;
        INode? parent = node.Parent;
        if (document is null || parent is null)
        {
            return;
        }

        IElement holder = document.CreateElement("span");
        holder.InnerHtml = replaced;

        foreach (INode child in holder.ChildNodes.ToList())
        {
            parent.InsertBefore(child, node);
        }

        parent.RemoveChild(node);
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

    /// <summary>
    /// Undoes <see cref="Escape"/> for the text-only path, where the value goes
    /// back as a text node rather than as markup and would otherwise show its
    /// entities literally.
    /// </summary>
    private static string Unescape(string text) =>
        text.Replace("&quot;", "\"", StringComparison.Ordinal)
            .Replace("&gt;", ">", StringComparison.Ordinal)
            .Replace("&lt;", "<", StringComparison.Ordinal)
            .Replace("&amp;", "&", StringComparison.Ordinal);
}
