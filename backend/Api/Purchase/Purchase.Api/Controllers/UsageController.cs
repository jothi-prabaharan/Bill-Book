using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Purchase.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Shared.Kernel.Licensing;

namespace Purchase.Api.Controllers;

/// <summary>
/// How many Purchase documents the signed-in branch has created this month, against
/// what its plan allows (D-69, TK-210). Read by the badge on the Purchase screens and
/// by the Plan screen. The branch comes from the token, so there is no id to point
/// at somebody else's.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("purchase")]
[Route("api/purchase/usage")]
[RequireBusinessType(BusinessType.Retail, BusinessType.Pharma, BusinessType.Jewellery, BusinessType.Restaurant, BusinessType.Service)]
public sealed class UsageController : ControllerBase
{
    private readonly UsageRepository _usage;

    public UsageController(UsageRepository usage) => _usage = usage;

    [HttpGet]
    public async Task<ActionResult<MonthlyUsageResponse>> Get(CancellationToken ct) =>
        Ok(await _usage.ReadAsync(ct));
}
