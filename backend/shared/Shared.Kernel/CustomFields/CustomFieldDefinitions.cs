using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.CustomFields;

/// <summary>One option of a dropdown or multi-select field. An option in use is deactivated, never removed.</summary>
public sealed class CustomFieldOptionDto
{
    public string Value { get; set; } = null!;

    public string Label { get; set; } = null!;

    public bool Active { get; set; } = true;
}

/// <summary>A custom field as every service reads it (TK-110).</summary>
public sealed class CustomFieldDefinitionDto
{
    public long CustomFieldDefinitionId { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<CustomFieldEntityKind>))]
    public CustomFieldEntityKind EntityKind { get; set; }

    /// <summary><c>[a-z][a-z0-9_]*</c>, permanent: the key values are stored under.</summary>
    public string Key { get; set; } = null!;

    public string Label { get; set; } = null!;

    [JsonConverter(typeof(JsonStringEnumConverter<CustomFieldDataType>))]
    public CustomFieldDataType DataType { get; set; }

    public List<CustomFieldOptionDto> Options { get; set; } = [];

    /// <summary>Enforced on new saves only: existing rows are not invalidated by a field made required later.</summary>
    public bool IsRequired { get; set; }

    public string? DefaultValue { get; set; }

    public int? MaxLength { get; set; }

    public bool ShowInList { get; set; }

    public bool ShowOnPrint { get; set; }

    public bool IsSearchable { get; set; }

    public bool CarryForward { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>A deactivated field keeps every stored value but takes no new ones.</summary>
    public bool IsActive { get; set; } = true;
}

/// <summary><c>GET internal/custom-fields</c>: one kind's fields, active and not, and a version that changes when any does.</summary>
public sealed class CustomFieldDefinitionsResponse
{
    public long Version { get; set; }

    public List<CustomFieldDefinitionDto> Fields { get; set; } = [];
}

/// <summary>
/// A branch's custom field definitions for one kind, as an owning service reads
/// them to validate a save. Cached for five minutes per branch and kind.
/// </summary>
public interface ICustomFieldDefinitions
{
    /// <summary>Every field of the kind, inactive ones included; null when Master could not be asked.</summary>
    Task<IReadOnlyList<CustomFieldDefinitionDto>?> GetAsync(CustomFieldEntityKind kind, CancellationToken ct = default);
}

public sealed class HttpCustomFieldDefinitions : ICustomFieldDefinitions
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    private readonly HttpClient _master;
    private readonly IMemoryCache _cache;
    private readonly ITenantContext _tenant;
    private readonly ILogger<HttpCustomFieldDefinitions> _log;

    public HttpCustomFieldDefinitions(HttpClient master, IMemoryCache cache, ITenantContext tenant, ILogger<HttpCustomFieldDefinitions> log)
    {
        _master = master;
        _cache = cache;
        _tenant = tenant;
        _log = log;
    }

    public async Task<IReadOnlyList<CustomFieldDefinitionDto>?> GetAsync(CustomFieldEntityKind kind, CancellationToken ct = default)
    {
        if (_tenant.CustomerId is not Guid customerId || _tenant.OrgId is not Guid orgId)
        {
            return null;
        }

        string key = $"custom-fields:{orgId}:{kind}";
        if (_cache.TryGetValue(key, out IReadOnlyList<CustomFieldDefinitionDto>? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            CustomFieldDefinitionsResponse? response = await _master.GetFromJsonAsync<CustomFieldDefinitionsResponse>(
                $"internal/custom-fields?customerId={customerId}&orgId={orgId}&kind={kind}", ct);

            if (response is null)
            {
                return null;
            }

            _cache.Set(key, (IReadOnlyList<CustomFieldDefinitionDto>)response.Fields, CacheFor);
            return response.Fields;
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning(ex, "Could not read the {Kind} custom fields for organization {OrgId}", kind, orgId);
            return null;
        }
    }
}
