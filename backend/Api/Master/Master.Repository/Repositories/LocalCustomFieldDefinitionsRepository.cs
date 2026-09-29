using Master.Repository.Clients;
using Shared.Kernel.CustomFields;

namespace Master.Repository.Repositories;

/// <summary>
/// Master's own reader of the branch's custom field definitions (TK-111):
/// the definitions live in its tenant database, so it reads them directly
/// rather than calling itself over HTTP.
/// </summary>
public sealed class LocalCustomFieldDefinitionsRepository : ICustomFieldDefinitions
{
    private readonly CustomFieldRepository _fields;

    public LocalCustomFieldDefinitionsRepository(CustomFieldRepository fields) => _fields = fields;

    public async Task<IReadOnlyList<CustomFieldDefinitionDto>?> GetAsync(CustomFieldEntityKind kind, CancellationToken ct = default) =>
        (await _fields.ForKindAsync(kind, ct)).Fields;
}
