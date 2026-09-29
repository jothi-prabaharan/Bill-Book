using Master.Repository.Clients;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.CustomFields;
using Shared.Kernel.Persistence;

namespace Master.Repository.Repositories;

/// <summary>
/// Settings › Custom fields (TK-110): one list of every kind's fields for the
/// branch. A field's key, kind and type are fixed once it exists; its label,
/// options, order and flags may change; removing it deactivates it and keeps
/// every value already stored.
/// </summary>
public sealed class CustomFieldRepository
{
    /// <summary>More than this many active fields on one kind is a sign it needs a real column.</summary>
    public const int MaxActivePerKind = 50;

    private readonly ContactsDbContext _db;

    public CustomFieldRepository(ContactsDbContext db) => _db = db;

    public async Task<List<CustomFieldDefinitionDto>> ListAsync(CustomFieldEntityKind? kind, CancellationToken ct) =>
        await _db.CustomFieldDefinitions.AsNoTracking()
            .Where(f => kind == null || f.EntityKind == kind)
            .OrderBy(f => f.EntityKind).ThenBy(f => f.DisplayOrder).ThenBy(f => f.Label).ThenBy(f => f.CustomFieldDefinitionId)
            .Select(f => ToDto(f))
            .ToListAsync(ct);

    /// <summary>One kind's fields for an owning service, with a version that moves whenever any of them changes.</summary>
    public async Task<CustomFieldDefinitionsResponse> ForKindAsync(CustomFieldEntityKind kind, CancellationToken ct)
    {
        List<CustomFieldDefinition> rows = await _db.CustomFieldDefinitions.AsNoTracking()
            .Where(f => f.EntityKind == kind)
            .OrderBy(f => f.DisplayOrder).ThenBy(f => f.Label).ThenBy(f => f.CustomFieldDefinitionId)
            .ToListAsync(ct);

        return new CustomFieldDefinitionsResponse
        {
            Version = rows.Count == 0 ? 0 : rows.Max(f => (f.ModifiedAt ?? f.CreatedAt)?.UtcTicks ?? 0) + rows.Count,
            Fields = [.. rows.Select(ToDto)],
        };
    }

    public async Task<CustomFieldResult> SaveAsync(long? id, SaveCustomFieldRequest request, CancellationToken ct)
    {
        CustomFieldDefinition? existing = null;
        if (id is long existingId)
        {
            existing = await _db.CustomFieldDefinitions.FirstOrDefaultAsync(f => f.CustomFieldDefinitionId == existingId, ct);
            if (existing is null)
            {
                return new CustomFieldResult(CustomFieldOutcome.NotFound);
            }
        }

        if (CustomFieldRules.Refusal(request, existing) is string refusal)
        {
            return new CustomFieldResult(CustomFieldOutcome.Refused, id ?? 0, refusal);
        }

        CustomFieldEntityKind kind = Enum.Parse<CustomFieldEntityKind>(request.EntityKind, ignoreCase: true);
        CustomFieldDataType type = Enum.Parse<CustomFieldDataType>(request.DataType, ignoreCase: true);
        string key = request.Key.Trim();

        if (existing is null && await _db.CustomFieldDefinitions.AnyAsync(f => f.EntityKind == kind && f.Key == key, ct))
        {
            return new CustomFieldResult(CustomFieldOutcome.Duplicate, 0,
                "Another field on this kind of record already uses that key. Keys are permanent, so choose another.");
        }

        if (request.IsActive && (existing is null || !existing.IsActive))
        {
            int active = await _db.CustomFieldDefinitions.CountAsync(f => f.EntityKind == kind && f.IsActive, ct);
            if (active >= MaxActivePerKind)
            {
                return new CustomFieldResult(CustomFieldOutcome.LimitReached, id ?? 0,
                    $"This kind of record already has {MaxActivePerKind} active custom fields. Deactivate one first.");
            }
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        CustomFieldDefinition field = existing ?? new CustomFieldDefinition { EntityKind = kind, Key = key, DataType = type };
        field.Label = request.Label.Trim();
        field.Options = CustomFieldRules.HasOptions(type)
            ? [.. request.Options.Select(o => new CustomFieldOption { Value = o.Value.Trim(), Label = o.Label.Trim(), Active = o.Active })]
            : [];
        field.IsRequired = request.IsRequired;
        field.DefaultValue = string.IsNullOrWhiteSpace(request.DefaultValue) ? null : request.DefaultValue.Trim();
        field.MaxLength = CustomFieldRules.IsText(type) ? request.MaxLength : null;
        field.ShowInList = request.ShowInList;
        field.ShowOnPrint = request.ShowOnPrint;
        field.IsSearchable = request.IsSearchable;
        field.CarryForward = request.CarryForward;
        field.DisplayOrder = request.DisplayOrder;
        field.IsActive = request.IsActive;

        if (existing is null)
        {
            _db.CustomFieldDefinitions.Add(field);
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new CustomFieldResult(CustomFieldOutcome.Ok, field.CustomFieldDefinitionId);
    }

    public static CustomFieldDefinitionDto ToDto(CustomFieldDefinition f) => new()
    {
        CustomFieldDefinitionId = f.CustomFieldDefinitionId,
        EntityKind = f.EntityKind,
        Key = f.Key,
        Label = f.Label,
        DataType = f.DataType,
        Options = [.. f.Options.Select(o => new CustomFieldOptionDto { Value = o.Value, Label = o.Label, Active = o.Active })],
        IsRequired = f.IsRequired,
        DefaultValue = f.DefaultValue,
        MaxLength = f.MaxLength,
        ShowInList = f.ShowInList,
        ShowOnPrint = f.ShowOnPrint,
        IsSearchable = f.IsSearchable,
        CarryForward = f.CarryForward,
        DisplayOrder = f.DisplayOrder,
        IsActive = f.IsActive,
    };
}

/// <summary>The rules a definition must meet, pure so they can be tested without a database.</summary>
public static partial class CustomFieldRules
{
    public static bool HasOptions(CustomFieldDataType type) =>
        type is CustomFieldDataType.Dropdown or CustomFieldDataType.MultiSelect;

    public static bool IsText(CustomFieldDataType type) =>
        type is CustomFieldDataType.Text or CustomFieldDataType.LongText;

    /// <summary>Why a definition cannot be saved, or null when it can. <paramref name="existing"/> is the field being edited.</summary>
    public static string? Refusal(SaveCustomFieldRequest request, CustomFieldDefinition? existing)
    {
        if (!Enum.TryParse(request.EntityKind, ignoreCase: true, out CustomFieldEntityKind kind) || !Enum.IsDefined(kind))
        {
            return "Choose what the field belongs to.";
        }

        if (!Enum.TryParse(request.DataType, ignoreCase: true, out CustomFieldDataType type) || !Enum.IsDefined(type))
        {
            return "Choose the field's type.";
        }

        string key = request.Key?.Trim() ?? string.Empty;
        if (key.Length is 0 or > 40 || !KeyPattern().IsMatch(key))
        {
            return "A key starts with a lowercase letter and holds only lowercase letters, digits and underscores, up to 40 characters.";
        }

        if (string.IsNullOrWhiteSpace(request.Label))
        {
            return "Give the field a label.";
        }

        if (existing is not null)
        {
            if (!string.Equals(existing.Key, key, StringComparison.Ordinal))
            {
                return "A field's key cannot be changed: every value already stored is filed under it. Add a new field instead.";
            }

            if (existing.EntityKind != kind)
            {
                return "A field cannot move to another kind of record.";
            }

            if (existing.DataType != type)
            {
                return "A field's type cannot be changed once it exists: the values already stored were checked against the old one.";
            }

            List<string> removed = [.. existing.Options.Select(o => o.Value).Except(request.Options.Select(o => o.Value.Trim()), StringComparer.Ordinal)];
            if (removed.Count > 0)
            {
                return $"An option cannot be removed once it exists, because records may hold it: {string.Join(", ", removed)}. Deactivate it instead.";
            }
        }

        if (HasOptions(type))
        {
            if (!request.Options.Any(o => o.Active))
            {
                return "A dropdown or multi-select field needs at least one active option.";
            }

            if (request.Options.Any(o => string.IsNullOrWhiteSpace(o.Value) || string.IsNullOrWhiteSpace(o.Label)))
            {
                return "Every option needs a value and a label.";
            }

            if (request.Options.Select(o => o.Value.Trim()).Distinct(StringComparer.Ordinal).Count() != request.Options.Count)
            {
                return "Two options have the same value.";
            }
        }
        else if (request.Options.Count > 0)
        {
            return "Only a dropdown or multi-select field has options.";
        }

        if (request.MaxLength is not null && !IsText(type))
        {
            return "Only a text field has a maximum length.";
        }

        if (!string.IsNullOrWhiteSpace(request.DefaultValue))
        {
            CustomFieldDefinitionDto probe = new()
            {
                Key = key,
                Label = "The default value",
                DataType = type,
                MaxLength = request.MaxLength,
                Options = [.. request.Options.Select(o => new CustomFieldOptionDto { Value = o.Value.Trim(), Label = o.Label, Active = o.Active })],
            };

            if (DefaultAsJson(type, request.DefaultValue.Trim()) is not JsonElement value
                || CustomFieldValidator.ValueProblem(probe, value) is not null)
            {
                return "The default value does not fit the field's type or options.";
            }
        }

        return null;
    }

    /// <summary>A default value, typed as a stored value would be. A multi-select default is its values separated by commas.</summary>
    public static JsonElement? DefaultAsJson(CustomFieldDataType type, string text) => type switch
    {
        CustomFieldDataType.Integer => long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long i) ? JsonSerializer.SerializeToElement(i) : null,
        CustomFieldDataType.Decimal => decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal d) ? JsonSerializer.SerializeToElement(d) : null,
        CustomFieldDataType.Boolean => bool.TryParse(text, out bool b) ? JsonSerializer.SerializeToElement(b) : null,
        CustomFieldDataType.MultiSelect => JsonSerializer.SerializeToElement(text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)),
        _ => JsonSerializer.SerializeToElement(text),
    };

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex KeyPattern();
}
