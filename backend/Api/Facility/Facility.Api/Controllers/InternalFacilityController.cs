using Facility.Entity.Enums;
using Facility.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Internal;
using Shared.Kernel.School;
using Shared.Kernel.Tenancy;

namespace Facility.Api.Controllers;

/// <summary>
/// Which assets and spaces exist in a branch (S6–S8): work orders, preventive
/// plans and AMC contracts check the ones they name here. The branch comes in
/// the body; the query filter keeps the answer inside it.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/facility")]
public sealed class InternalFacilityController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalFacilityController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost("lookup")]
    public async Task<IActionResult> Lookup([FromBody] FacilityLookupRequest request, CancellationToken ct)
    {
        if (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId) != InternalTenantOutcome.Applied)
        {
            return BadRequest();
        }

        var db = _services.GetRequiredService<FacilityDbContext>();
        List<long> assetIds = [.. request.AssetIds.Take(500)];
        List<long> spaceIds = [.. request.SpaceIds.Take(500)];

        return Ok(new FacilityLookupResponse
        {
            Assets = await db.FacilityAssets.AsNoTracking().Where(a => assetIds.Contains(a.FacilityAssetId))
                .Select(a => new FacilityItem { Id = a.FacilityAssetId, Code = a.AssetTag, Name = a.Name, IsUsable = a.AssetStatus != AssetStatus.Disposed })
                .ToListAsync(ct),
            Spaces = await db.Spaces.AsNoTracking().Where(s => spaceIds.Contains(s.SpaceId))
                .Select(s => new FacilityItem { Id = s.SpaceId, Code = s.Code, Name = s.Name, IsUsable = s.IsActive })
                .ToListAsync(ct),
        });
    }
}
