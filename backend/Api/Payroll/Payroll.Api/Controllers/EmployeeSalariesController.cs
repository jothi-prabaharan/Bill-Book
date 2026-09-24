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
[Route("api/payroll/employee-salaries")]
public sealed class EmployeeSalariesController : ControllerBase
{
    private readonly SalarySetupService _setup;

    public EmployeeSalariesController(SalarySetupService setup) => _setup = setup;

    [HttpGet("employee/{employeeId:long}")]
    public async Task<IActionResult> GetByEmployee(long employeeId, CancellationToken ct)
    {
        EmployeeSalaryView? salary = await _setup.GetEmployeeSalaryAsync(employeeId, ct);
        return salary is null ? NotFound() : Ok(salary);
    }

    [HttpPost]
    public async Task<IActionResult> Assign([FromBody] SaveEmployeeSalaryRequest request, CancellationToken ct)
    {
        long id = await _setup.AssignEmployeeSalaryAsync(request, ct);
        return Ok(new { id });
    }

    [HttpPost("revisions")]
    public async Task<IActionResult> Revise([FromBody] SaveSalaryRevisionRequest request, CancellationToken ct)
    {
        long id = await _setup.ReviseSalaryAsync(request, ct);
        return Ok(new { id });
    }
}
