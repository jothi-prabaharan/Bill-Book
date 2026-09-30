using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payroll.Api.Services;
using Payroll.Repository.Repositories;
using Payroll.Repository.Clients;
using Payroll.Entity.Models;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Payroll.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("payroll")]
[RequireAddOn(AddOn.Payroll)]
[Route("api/payroll/salary-structures")]
public sealed class SalaryStructuresController : ControllerBase
{
    private readonly SalarySetupRepository _setup;

    public SalaryStructuresController(SalarySetupRepository setup) => _setup = setup;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _setup.ListStructuresAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        SalaryStructureView? structure = await _setup.GetStructureAsync(id, ct);
        return structure is null ? NotFound() : Ok(structure);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveSalaryStructureRequest request, CancellationToken ct)
    {
        long id = await _setup.SaveStructureAsync(null, request, ct);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveSalaryStructureRequest request, CancellationToken ct)
    {
        await _setup.SaveStructureAsync(id, request, ct);
        return NoContent();
    }
}
