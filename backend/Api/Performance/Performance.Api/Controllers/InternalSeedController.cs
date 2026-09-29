using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Performance.Api.Services;
using Performance.Repository.Repositories;
using Performance.Repository.Clients;
using Shared.Kernel.Internal;

namespace Performance.Api.Controllers;

[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/seed")]
public sealed class InternalSeedController : ControllerBase
{
    private readonly PerformanceSeederRepository _seeder;

    public InternalSeedController(PerformanceSeederRepository seeder) => _seeder = seeder;

    [HttpPost]
    public async Task<IActionResult> Seed([FromBody] SeedRequest request, CancellationToken ct)
    {
        var result = await _seeder.SeedAsync(request.CustomerId, request.OrgId, ct);
        return Ok(result);
    }
}

public sealed class SeedRequest
{
    public Guid CustomerId { get; set; }
    public Guid OrgId { get; set; }
}
