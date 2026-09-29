using System.Globalization;
using System.Text.Json;
using Shared.Kernel.Printing;

namespace Shared.Kernel.CustomFields;

/// <summary>
/// Puts a document's custom field values into its print payload as
/// <c>custom.key</c> singles (TK-112), as text a reader expects: option labels
/// for choices, Yes or No, a list joined by commas, a date as the template's
/// other dates are. Only fields marked <c>ShowOnPrint</c> are added when the
/// definitions are known; without them every stored value is.
/// </summary>
public static class CustomFieldPrint
{
    public static void Add(PrintPayload payload, JsonDocument? values, IReadOnlyList<CustomFieldDefinitionDto>? definitions)
    {
        Dictionary<string, CustomFieldDefinitionDto>? byKey = definitions?.ToDictionary(d => d.Key, StringComparer.Ordinal);

        foreach ((string key, JsonElement value) in CustomFieldStore.Read(values))
        {
            CustomFieldDefinitionDto? field = null;
            if (byKey is not null && (!byKey.TryGetValue(key, out field) || !field.ShowOnPrint))
            {
                continue;
            }

            payload.Singles[PlaceholderCatalog.CustomPrefix + key] = Text(value, field);
        }
    }

    public static string Text(JsonElement value, CustomFieldDefinitionDto? field)
    {
        string Label(string v) => field?.Options.FirstOrDefault(o => o.Value == v)?.Label ?? v;

        return value.ValueKind switch
        {
            JsonValueKind.True => "Yes",
            JsonValueKind.False => "No",
            JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.String ? Label(v.GetString()!) : v.GetRawText())),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.String when field?.DataType == CustomFieldDataType.Date
                && DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)
                => date.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture),
            JsonValueKind.String => Label(value.GetString()!),
            _ => string.Empty,
        };
    }
}
