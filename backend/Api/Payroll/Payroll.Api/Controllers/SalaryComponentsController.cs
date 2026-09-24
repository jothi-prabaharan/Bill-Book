using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payroll.Api.Services;
using Payroll.Entity.Models;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Payroll.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("payroll")]
[RequireApp(App.Payroll)]
[Route("api/payroll/salary-components")]
public sealed class SalaryComponentsController : ControllerBase
{
    private readonly SalarySetupService _setup;

    public SalaryComponentsController(SalarySetupService setup) => _setup = setup;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _setup.ListComponentsAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        SalaryComponentView? component = await _setup.GetComponentAsync(id, ct);
        return component is null ? NotFound() : Ok(component);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveSalaryComponentRequest request, CancellationToken ct)
    {
        long id = await _setup.SaveComponentAsync(null, request, ct);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveSalaryComponentRequest request, CancellationToken ct)
    {
        await _setup.SaveComponentAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        bool deleted = await _setup.DeleteComponentAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }
}
