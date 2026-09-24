using System.Text.Json;
using Printing.Entity.Models;
using Shared.Kernel.Printing;

namespace Printing.Api.Services;

/// <summary>
/// Turns a payload off the wire into the <see cref="PrintPayload"/> the renderer
/// reads.
///
/// <b>Every value becomes a CLR value, because the formatter matches on type.</b>
/// <c>MaskFormatter</c> formats a <c>decimal</c> against the placeholder's mask
/// and a <c>string</c> as itself; a <see cref="JsonElement"/> is neither, so an
/// amount would print as its raw digits with no grouping and an image URL would
/// print as nothing. Numbers become decimals — never doubles, which would put
/// binary rounding into a printed total — and dates stay strings, which the
/// formatter already parses.
///
/// Keys are matched case-insensitively, as <see cref="PrintPayload"/> matches
/// them, so a payload built in camelCase by a JSON serializer resolves the same
/// tags as one built by hand.
/// </summary>
public static class PrintPayloadReader
{
    public static PrintPayload Read(RenderPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var result = new PrintPayload();

        foreach ((string key, JsonElement value) in payload.Singles)
        {
            result.Singles[key] = Value(value);
        }

        foreach ((string group, List<Dictionary<string, JsonElement>> rows) in payload.Lists)
        {
            result.Lists[group] = [.. rows.Select(Row)];
        }

        return result;
    }

    private static IReadOnlyDictionary<string, object?> Row(Dictionary<string, JsonElement> row)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach ((string key, JsonElement value) in row)
        {
            values[key] = Value(value);
        }

        return values;
    }

    /// <summary>
    /// One JSON value as the formatter expects it. An object or array has no
    /// placeholder that could print it, so it is kept as its raw text rather
    /// than dropped — visible on the page, and so visibly wrong.
    /// </summary>
    public static object? Value(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.TryGetDecimal(out decimal number) ? number : value.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => value.GetRawText(),
    };
}
