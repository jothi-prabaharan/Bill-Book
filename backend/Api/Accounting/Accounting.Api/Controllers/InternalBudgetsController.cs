using Accounting.Api.Services;
using Accounting.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Budgets;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Controllers;

/// <summary>
/// Purchase asks here whether a bill it has just saved passes the branch's
/// approved budget (TK-109). A read only; the answer is warnings, never a refusal.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/budgets")]
public sealed class InternalBudgetsController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalBudgetsController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost("check")]
    public async Task<IActionResult> Check([FromBody] BudgetCheckRequest request, CancellationToken ct)
    {
        if (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId) != InternalTenantOutcome.Applied)
        {
            return BadRequest(new BudgetCheckResponse());
        }

        List<string> warnings = await _services.GetRequiredService<BudgetCheckRepository>()
            .WarningsAsync(request.Date, request.Lines, ct);

        return Ok(new BudgetCheckResponse { Warnings = warnings });
    }
}
