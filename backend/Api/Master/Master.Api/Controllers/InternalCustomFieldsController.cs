using Master.Api.Services;
using Master.Repository.Repositories;
using Master.Repository.Clients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.CustomFields;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Master.Api.Controllers;

/// <summary>
/// One kind's custom field definitions for an owning service to validate a
/// save against (TK-110), read through <c>Shared.Kernel.CustomFields.HttpCustomFieldDefinitions</c>.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/custom-fields")]
public sealed class InternalCustomFieldsController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalCustomFieldsController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpGet]
    public async Task<IActionResult> ForKind([FromQuery] Guid customerId, [FromQuery] Guid orgId, [FromQuery] string? kind, CancellationToken ct)
    {
        if (InternalTenant.Apply(_tenant, customerId, orgId) != InternalTenantOutcome.Applied)
        {
            return BadRequest(new CustomFieldDefinitionsResponse());
        }

        if (!Enum.TryParse(kind, ignoreCase: true, out CustomFieldEntityKind parsed) || !Enum.IsDefined(parsed))
        {
            return BadRequest(new CustomFieldDefinitionsResponse());
        }

        return Ok(await _services.GetRequiredService<CustomFieldRepository>().ForKindAsync(parsed, ct));
    }
}
