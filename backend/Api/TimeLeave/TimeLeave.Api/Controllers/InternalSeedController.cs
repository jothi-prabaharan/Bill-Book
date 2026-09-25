using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;
using TimeLeave.Api.Services;

namespace TimeLeave.Api.Controllers;

[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/seed")]
public sealed class InternalSeedController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;
    private readonly ILogger<InternalSeedController> _log;

    public InternalSeedController(
        TenantContext tenant, IServiceProvider services, ILogger<InternalSeedController> log)
    {
        _tenant = tenant;
        _services = services;
        _log = log;
    }

    [HttpPost("organization")]
    public async Task<IActionResult> SeedOrganization(
        [FromBody] SeedOrganizationRequest request, CancellationToken ct)
    {
        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;

        var seeder = _services.GetRequiredService<TimeLeaveSeeder>();
        await seeder.SeedBranchAsync(request.OrgId, ct);

        var response = new SeedOrganizationResponse
        {
            Seeded = new Dictionary<string, int>
            {
                ["LeaveTypes"] = 5,
                ["LeavePolicies"] = 2,
                ["Shifts"] = 1,
                ["WeeklyOffPolicies"] = 1
            }
        };

        _log.LogInformation(
            "Seeded TimeLeave for organization {OrgId}: {@Seeded}", request.OrgId, response.Seeded);

        return Ok(response);
    }
}
