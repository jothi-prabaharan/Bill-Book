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
[Route("api/payroll/adjustments")]
public sealed class PayrollAdjustmentsController : ControllerBase
{
    private readonly PayrollAdjustmentService _adjustments;

    public PayrollAdjustmentsController(PayrollAdjustmentService adjustments) => _adjustments = adjustments;

    // ---- One Time Payments --------------------------------------------------

    [HttpGet("one-time-payments")]
    public async Task<IActionResult> ListOneTimePayments([FromQuery] DateOnly month, CancellationToken ct) =>
        Ok(await _adjustments.ListOneTimePaymentsAsync(month, ct));

    [HttpPost("one-time-payments")]
    public async Task<IActionResult> CreateOneTimePayment([FromBody] SaveOneTimePaymentRequest request, CancellationToken ct) =>
        Ok(new { id = await _adjustments.CreateOneTimePaymentAsync(request, ct) });

    // ---- Salary Holds -------------------------------------------------------

    [HttpGet("salary-holds")]
    public async Task<IActionResult> ListSalaryHolds(CancellationToken ct) =>
        Ok(await _adjustments.ListSalaryHoldsAsync(ct));

    [HttpPost("salary-holds")]
    public async Task<IActionResult> CreateSalaryHold([FromBody] SaveSalaryHoldRequest request, CancellationToken ct) =>
        Ok(new { id = await _adjustments.CreateSalaryHoldAsync(request, ct) });

    [HttpPost("salary-holds/{id:long}/release")]
    public async Task<IActionResult> ReleaseSalaryHold(long id, CancellationToken ct)
    {
        await _adjustments.ReleaseSalaryHoldAsync(id, ct);
        return NoContent();
    }

    // ---- Loans --------------------------------------------------------------

    [HttpGet("loans")]
    public async Task<IActionResult> ListLoans([FromQuery] long? employeeId, CancellationToken ct) =>
        Ok(await _adjustments.ListEmployeeLoansAsync(employeeId, ct));

    [HttpPost("loans/disburse")]
    public async Task<IActionResult> DisburseLoan([FromBody] DisburseLoanRequest request, CancellationToken ct) =>
        Ok(new { id = await _adjustments.DisburseLoanAsync(request, ct) });

    // ---- Attendance Input ---------------------------------------------------

    [HttpGet("attendance-input")]
    public async Task<IActionResult> ListAttendance([FromQuery] DateOnly month, CancellationToken ct) =>
        Ok(await _adjustments.ListMonthlyAttendanceAsync(month, ct));

    [HttpPost("attendance-input")]
    public async Task<IActionResult> SaveAttendance([FromBody] SaveMonthlyAttendanceRequest request, CancellationToken ct)
    {
        await _adjustments.SaveMonthlyAttendanceAsync(request, ct);
        return NoContent();
    }
}
