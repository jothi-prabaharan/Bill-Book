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
[Route("api/payroll/tax")]
public sealed class TaxController : ControllerBase
{
    private readonly TaxCalculationService _taxService;

    public TaxController(TaxCalculationService taxService) => _taxService = taxService;

    [HttpGet("declarations/{employeeId:long}")]
    public async Task<IActionResult> GetDeclaration(long employeeId, [FromQuery] string financialYear = "2026-2027", CancellationToken ct = default)
    {
        var decl = await _taxService.GetDeclarationAsync(employeeId, financialYear, ct);
        return decl is null ? NotFound() : Ok(decl);
    }

    [HttpPost("declarations")]
    public async Task<IActionResult> SaveDeclaration([FromBody] SaveTaxDeclarationRequest request, CancellationToken ct) =>
        Ok(new { id = await _taxService.SaveDeclarationAsync(request, ct) });

    [HttpPost("declarations/{id:long}/lock")]
    public async Task<IActionResult> LockDeclaration(long id, CancellationToken ct)
    {
        await _taxService.SetDeclarationLockAsync(id, true, ct);
        return NoContent();
    }

    [HttpPost("declarations/{id:long}/unlock")]
    public async Task<IActionResult> UnlockDeclaration(long id, CancellationToken ct)
    {
        await _taxService.SetDeclarationLockAsync(id, false, ct);
        return NoContent();
    }

    [HttpGet("previous-employer/{employeeId:long}")]
    public async Task<IActionResult> GetPreviousEmployerIncome(long employeeId, [FromQuery] string financialYear = "2026-2027", CancellationToken ct = default)
    {
        var income = await _taxService.GetPreviousEmployerIncomeAsync(employeeId, financialYear, ct);
        return income is null ? NotFound() : Ok(income);
    }

    [HttpPost("previous-employer")]
    public async Task<IActionResult> SavePreviousEmployerIncome([FromBody] SavePreviousEmployerIncomeRequest request, CancellationToken ct) =>
        Ok(new { id = await _taxService.SavePreviousEmployerIncomeAsync(request, ct) });

    [HttpGet("slabs")]
    public async Task<IActionResult> GetTaxSlabs([FromQuery] string financialYear = "2026-2027", [FromQuery] string regime = "New", CancellationToken ct = default) =>
        Ok(await _taxService.GetTaxSlabsAsync(financialYear, regime, ct));

    [HttpPost("compute")]
    public async Task<IActionResult> ComputeTax(
        [FromQuery] long employeeId,
        [FromQuery] string financialYear,
        [FromQuery] decimal annualGross,
        [FromQuery] int remainingMonths = 12,
        CancellationToken ct = default) =>
        Ok(await _taxService.ComputeTaxAsync(employeeId, financialYear, annualGross, remainingMonths, ct));

    [HttpGet("form16/{employeeId:long}")]
    public async Task<IActionResult> GetForm16PartB(long employeeId, [FromQuery] string financialYear = "2026-2027", CancellationToken ct = default) =>
        Ok(await _taxService.GenerateForm16PartBAsync(employeeId, financialYear, ct));

    [HttpGet("form12ba/{employeeId:long}")]
    public async Task<IActionResult> GetForm12Ba(long employeeId, [FromQuery] string financialYear = "2026-2027", CancellationToken ct = default) =>
        Ok(await _taxService.GenerateForm12BaAsync(employeeId, financialYear, ct));

    [HttpGet("24q")]
    public async Task<IActionResult> Get24QData([FromQuery] string financialYear = "2026-2027", [FromQuery] int quarter = 1, CancellationToken ct = default) =>
        Ok(await _taxService.Generate24QDataAsync(financialYear, quarter, ct));
}
