using System.Text.Json;
using Shared.Kernel.Documents;

namespace Shared.Kernel.CustomFields;

/// <summary>The document a new one is made from, whose values may carry forward (TK-112).</summary>
public sealed record CustomFieldSource(CustomFieldEntityKind Kind, JsonDocument? Values);

/// <summary>
/// Resolves a sales or purchase document's custom field values on save
/// (TK-112), the one place every document service goes through:
/// <list type="bullet">
/// <item>An update that leaves the values out keeps what is stored.</item>
/// <item>A new document made from another takes the source's values for keys
/// the source marks <c>CarryForward</c> and the new kind also has, unless the
/// request gives its own. A carried value the new field would refuse is left
/// behind rather than failing the conversion.</item>
/// <item>The result is validated against the document kind's definitions, with
/// required fields enforced on new documents only.</item>
/// </list>
/// </summary>
public sealed class DocumentCustomFieldGate
{
    private readonly ICustomFieldDefinitions _definitions;

    public DocumentCustomFieldGate(ICustomFieldDefinitions definitions) => _definitions = definitions;

    /// <summary>
    /// Sets <paramref name="document"/>'s values, or returns why they cannot be
    /// saved. With no gate (a service built without one, as in tests) the
    /// requested values are stored as given.
    /// </summary>
    public static Task<string?> ApplyAsync(
        DocumentCustomFieldGate? gate,
        DocumentHeaderBase document,
        CustomFieldEntityKind kind,
        IReadOnlyDictionary<string, JsonElement>? requested,
        bool isNew,
        CustomFieldSource? source,
        CancellationToken ct)
    {
        if (gate is not null)
        {
            return gate.ApplyAsync(document, kind, requested, isNew, source, ct);
        }

        if (requested is not null)
        {
            document.CustomFields = CustomFieldStore.Write(requested);
        }

        return Task.FromResult<string?>(null);
    }

    public async Task<string?> ApplyAsync(
        DocumentHeaderBase document,
        CustomFieldEntityKind kind,
        IReadOnlyDictionary<string, JsonElement>? requested,
        bool isNew,
        CustomFieldSource? source,
        CancellationToken ct)
    {
        if (!isNew && requested is null)
        {
            return null;
        }

        IReadOnlyList<CustomFieldDefinitionDto>? definitions = await _definitions.GetAsync(kind, ct);
        if (definitions is null)
        {
            return "Custom fields could not be checked just now, so nothing was saved. Try again.";
        }

        Dictionary<string, JsonElement> values = requested is null
            ? new(StringComparer.Ordinal)
            : new(requested, StringComparer.Ordinal);

        if (isNew && source?.Values is not null)
        {
            IReadOnlyList<CustomFieldDefinitionDto> sourceDefinitions = source.Kind == kind
                ? definitions
                : await _definitions.GetAsync(source.Kind, ct) ?? [];

            foreach ((string key, JsonElement value) in Carry(CustomFieldStore.Read(source.Values), sourceDefinitions, definitions))
            {
                if (!values.TryGetValue(key, out JsonElement given) || CustomFieldValidator.IsEmpty(given))
                {
                    values[key] = value;
                }
            }
        }

        IReadOnlyList<string> errors = CustomFieldValidator.Validate(
            definitions, values, isNew ? null : CustomFieldStore.Read(document.CustomFields));
        if (errors.Count > 0)
        {
            return string.Join(" ", errors);
        }

        document.CustomFields = CustomFieldStore.Write(values);
        return null;
    }

    /// <summary>
    /// The source values that carry to the target kind: the source field is
    /// marked <c>CarryForward</c>, the target has an active field with the same
    /// key, and the value fits that target field.
    /// </summary>
    public static Dictionary<string, JsonElement> Carry(
        IReadOnlyDictionary<string, JsonElement> sourceValues,
        IReadOnlyList<CustomFieldDefinitionDto> sourceDefinitions,
        IReadOnlyList<CustomFieldDefinitionDto> targetDefinitions)
    {
        HashSet<string> carried = [.. sourceDefinitions.Where(d => d.CarryForward).Select(d => d.Key)];
        Dictionary<string, CustomFieldDefinitionDto> targets = targetDefinitions
            .Where(d => d.IsActive)
            .ToDictionary(d => d.Key, StringComparer.Ordinal);

        Dictionary<string, JsonElement> result = new(StringComparer.Ordinal);
        foreach ((string key, JsonElement value) in sourceValues)
        {
            if (carried.Contains(key)
                && targets.TryGetValue(key, out CustomFieldDefinitionDto? target)
                && !CustomFieldValidator.IsEmpty(value)
                && CustomFieldValidator.ValueProblem(target, value) is null)
            {
                result[key] = value;
            }
        }

        return result;
    }
}
