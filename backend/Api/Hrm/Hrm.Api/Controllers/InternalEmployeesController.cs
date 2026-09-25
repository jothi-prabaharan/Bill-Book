using Hrm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Approvals;
using Shared.Kernel.Employees;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Hrm.Api.Controllers;

/// <summary>
/// What other services ask Hrm about employees (TK-49): Master for the
/// employee approvers of a chain, TimeLeave for the profile a policy and a
/// chain are chosen by. The branch comes in the body, as on every internal
/// route, and is set before the context is resolved.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal")]
public sealed class InternalEmployeesController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalEmployeesController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost("approval-chains/resolve-employees")]
    public async Task<IActionResult> ResolveEmployees([FromBody] ResolveEmployeesRequest request, CancellationToken ct)
    {
        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;
        return Ok(await _services.GetRequiredService<EmployeeApproverService>().ResolveAsync(request, ct));
    }

    [HttpPost("employees/lookup")]
    public async Task<IActionResult> Lookup([FromBody] EmployeeLookupRequest request, CancellationToken ct)
    {
        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;
        return Ok(await _services.GetRequiredService<EmployeeProfileService>().FindAsync(request, ct));
    }

    [HttpPost("employees/onboard")]
    public async Task<IActionResult> Onboard([FromBody] OnboardEmployeeRequest request, CancellationToken ct)
    {
        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;
        return Ok(await _services.GetRequiredService<EmployeeProfileService>().OnboardAsync(request, ct));
    }
}
