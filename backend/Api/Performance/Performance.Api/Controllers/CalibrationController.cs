using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Performance.Api.Services;
using Performance.Entity.Models;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Performance.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("performance")]
[RequireApp(App.Hrms)]
[Route("api/prf/calibration")]
public sealed class CalibrationController : ControllerBase
{
    private readonly PerformanceService _service;
    private readonly IEmployeeClient _hrm;
    private readonly ITenantContext _tenant;
    private readonly ICallerPermissions _caller;

    public CalibrationController(
        PerformanceService service,
        IEmployeeClient hrm,
        ITenantContext tenant,
        ICallerPermissions caller)
    {
        _service = service;
        _hrm = hrm;
        _tenant = tenant;
        _caller = caller;
    }

    [HttpGet("distribution/{cycleId:long}")]
    public async Task<IActionResult> GetDistribution(long cycleId, CancellationToken ct) =>
        Ok(await _service.GetCalibrationDistributionAsync(cycleId, ct));

    [HttpPost("adjust")]
    public async Task<IActionResult> Adjust([FromBody] CalibrationAdjustmentRequest req, CancellationToken ct)
    {
        long hrEmployeeId = 0;
        if (_caller.UserId.HasValue)
        {
            var emp = await _hrm.FindByUserIdAsync(_tenant.CustomerId ?? Guid.Empty, _tenant.OrgId ?? Guid.Empty, _caller.UserId.Value, ct);
            if (emp is not null) hrEmployeeId = emp.EmployeeId;
        }

        bool success = await _service.AdjustCalibrationAsync(req, hrEmployeeId, ct);
        return success ? NoContent() : NotFound();
    }
}
