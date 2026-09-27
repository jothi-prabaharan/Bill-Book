using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Shared.Kernel.CustomFields;

/// <summary>
/// Checks a record's custom field values against its kind's definitions
/// (TK-110). Pure, and the one implementation every owning service's save
/// path uses, so a contact and an invoice answer alike.
///
/// <list type="bullet">
/// <item>A key with no definition is refused. A jsonb column with no validation becomes a junk drawer reports cannot trust.</item>
/// <item>A required field is enforced on <b>new</b> saves only: making a field required later does not invalidate every existing row.</item>
/// <item>A deactivated field, or a deactivated option, keeps the value already stored but takes no new one.</item>
/// </list>
/// </summary>
public static partial class CustomFieldValidator
{
    public const int DefaultTextLength = 255;
    public const int DefaultLongTextLength = 4000;
    public const int MaxEmailLength = 254;
    public const int MaxUrlLength = 2000;

    /// <summary>
    /// The problems with <paramref name="values"/>, each a sentence for the
    /// person saving, or none. <paramref name="previous"/> is what the record
    /// held before this save, or null for a new record.
    /// </summary>
    public static IReadOnlyList<string> Validate(
        IReadOnlyList<CustomFieldDefinitionDto> definitions,
        IReadOnlyDictionary<string, JsonElement>? values,
        IReadOnlyDictionary<string, JsonElement>? previous = null)
    {
        values ??= new Dictionary<string, JsonElement>();
        bool isNew = previous is null;
        Dictionary<string, CustomFieldDefinitionDto> byKey = definitions.ToDictionary(d => d.Key, StringComparer.Ordinal);
        List<string> errors = [];

        foreach ((string key, JsonElement value) in values)
        {
            if (!byKey.TryGetValue(key, out CustomFieldDefinitionDto? field))
            {
                errors.Add($"\"{key}\" is not a custom field here.");
                continue;
            }

            JsonElement? before = previous is not null && previous.TryGetValue(key, out JsonElement old) ? old : null;
            bool unchanged = before is JsonElement b && Same(b, value);

            if (IsEmpty(value))
            {
                continue;
            }

            if (!field.IsActive)
            {
                if (!unchanged)
                {
                    errors.Add($"{field.Label} is no longer in use, so it cannot be set.");
                }

                continue;
            }

            if (ValueProblem(field, value, before) is string problem)
            {
                errors.Add(problem);
            }
        }

        if (isNew)
        {
            foreach (CustomFieldDefinitionDto field in definitions.Where(d => d.IsActive && d.IsRequired))
            {
                if (!values.TryGetValue(field.Key, out JsonElement value) || IsEmpty(value))
                {
                    errors.Add($"{field.Label} is required.");
                }
            }
        }

        return errors;
    }

    /// <summary>Why one value does not fit its field, or null when it does. <paramref name="before"/> lets a deactivated option stand where it already was.</summary>
    public static string? ValueProblem(CustomFieldDefinitionDto field, JsonElement value, JsonElement? before = null)
    {
        string label = field.Label;

        switch (field.DataType)
        {
            case CustomFieldDataType.Text:
            case CustomFieldDataType.LongText:
            {
                if (value.ValueKind != JsonValueKind.String)
                {
                    return $"{label} must be text.";
                }

                int max = field.MaxLength ?? (field.DataType == CustomFieldDataType.Text ? DefaultTextLength : DefaultLongTextLength);
                return value.GetString()!.Length > max ? $"{label} cannot exceed {max} characters." : null;
            }

            case CustomFieldDataType.Integer:
                return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _) ? null : $"{label} must be a whole number.";

            case CustomFieldDataType.Decimal:
                return value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _) ? null : $"{label} must be a number.";

            case CustomFieldDataType.Date:
                return value.ValueKind == JsonValueKind.String
                    && DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                    ? null
                    : $"{label} must be a date.";

            case CustomFieldDataType.Boolean:
                return value.ValueKind is JsonValueKind.True or JsonValueKind.False ? null : $"{label} must be yes or no.";

            case CustomFieldDataType.Dropdown:
            {
                if (value.ValueKind != JsonValueKind.String)
                {
                    return $"{label} must be one of its options.";
                }

                string chosen = value.GetString()!;
                bool kept = before is JsonElement b && b.ValueKind == JsonValueKind.String && b.GetString() == chosen;
                return Allowed(field, chosen) || (kept && Known(field, chosen)) ? null : $"{label} must be one of its options.";
            }

            case CustomFieldDataType.MultiSelect:
            {
                if (value.ValueKind != JsonValueKind.Array || value.EnumerateArray().Any(v => v.ValueKind != JsonValueKind.String))
                {
                    return $"{label} must be a list of its options.";
                }

                List<string> chosen = [.. value.EnumerateArray().Select(v => v.GetString()!)];
                HashSet<string> kept = before is JsonElement b && b.ValueKind == JsonValueKind.Array
                    ? [.. b.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()!)]
                    : [];

                if (chosen.Distinct(StringComparer.Ordinal).Count() != chosen.Count)
                {
                    return $"{label} lists an option twice.";
                }

                return chosen.All(c => Allowed(field, c) || (kept.Contains(c) && Known(field, c)))
                    ? null
                    : $"{label} must be a list of its options.";
            }

            case CustomFieldDataType.Email:
                return value.ValueKind == JsonValueKind.String
                    && value.GetString()!.Length <= MaxEmailLength
                    && EmailPattern().IsMatch(value.GetString()!)
                    ? null
                    : $"{label} must be an email address.";

            case CustomFieldDataType.Url:
                return value.ValueKind == JsonValueKind.String
                    && value.GetString()!.Length <= MaxUrlLength
                    && Uri.TryCreate(value.GetString(), UriKind.Absolute, out Uri? uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                    ? null
                    : $"{label} must be a web address starting http:// or https://.";

            case CustomFieldDataType.Phone:
                return value.ValueKind == JsonValueKind.String && PhonePattern().IsMatch(value.GetString()!)
                    ? null
                    : $"{label} must be a phone number.";

            default:
                return $"{label} has a type this version does not know.";
        }
    }

    /// <summary>A value the field treats as not given: null, an empty string, or an empty list.</summary>
    public static bool IsEmpty(JsonElement value) =>
        value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
        || (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()))
        || (value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 0);

    private static bool Allowed(CustomFieldDefinitionDto field, string value) =>
        field.Options.Any(o => o.Active && o.Value == value);

    private static bool Known(CustomFieldDefinitionDto field, string value) =>
        field.Options.Any(o => o.Value == value);

    private static bool Same(JsonElement a, JsonElement b) =>
        a.ValueKind == b.ValueKind && a.GetRawText() == b.GetRawText();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    // Local numbers are stored without a prefix and foreign ones with a leading
    // +, as everywhere else in the product.
    [GeneratedRegex(@"^\+?[0-9][0-9 \-]{2,19}$")]
    private static partial Regex PhonePattern();
}
