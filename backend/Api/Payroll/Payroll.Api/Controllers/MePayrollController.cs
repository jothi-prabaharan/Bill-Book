using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Payroll.Api.Services;
using Payroll.Entity.Enums;
using Payroll.Entity.Models;
using Payroll.Entity.TableEntities;
using Payroll.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Payroll.Api.Controllers;

/// <summary>
/// Self-service for payslips, Form 16, and tax declarations (H8, TK-55).
/// Resolves the employee strictly from the caller's JWT user claim ('sub'), never an ID in the URL.
/// Accessible to any signed-in user without requiring administrative module permissions.
/// </summary>
[ApiController]
[Authorize]
[RequireApp(App.Payroll | App.Hrms)]
[Route("api/payroll/me")]
[Route("api/me")]
public sealed class MePayrollController : ControllerBase
{
    private readonly PayrollDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenant;
    private readonly IHrmClient _hrm;
    private readonly TaxCalculationService _taxService;

    public MePayrollController(
        PayrollDbContext db,
        ICurrentUser currentUser,
        ITenantContext tenant,
        IHrmClient hrm,
        TaxCalculationService taxService)
    {
        _db = db;
        _currentUser = currentUser;
        _tenant = tenant;
        _hrm = hrm;
        _taxService = taxService;
    }

    private async Task<long?> ResolveCurrentEmployeeIdAsync(CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId || _tenant.CustomerId is not Guid customerId || _tenant.OrgId is not Guid orgId)
        {
            return null;
        }

        var profile = await _hrm.FindByUserIdAsync(customerId, orgId, userId, ct);
        return profile?.EmployeeId;
    }

    [HttpGet("payslips")]
    public async Task<IActionResult> GetPayslips(CancellationToken ct)
    {
        long? empId = await ResolveCurrentEmployeeIdAsync(ct);
        if (empId is null) return NotFound(new { message = "No active employee profile linked to your user account." });

        var list = await _db.Payslips
            .AsNoTracking()
            .Include(p => p.Run)
            .Where(p => p.EmployeeId == empId.Value && (p.Run.Status == PayrollRunStatus.Approved || p.Run.Status == PayrollRunStatus.Posted || p.Run.Status == PayrollRunStatus.MarkedPaid))
            .OrderByDescending(p => p.Run.Month)
            .Select(p => new MyPayslipSummaryDto
            {
                PayslipId = p.PayslipId,
                PayrollRunId = p.PayrollRunId,
                Month = p.Run.Month,
                MonthName = p.Run.Month.ToString("MMMM yyyy"),
                PaidDays = p.PaidDays,
                GrossEarnings = p.GrossEarnings,
                GrossDeductions = p.GrossDeductions,
                NetPay = p.NetPay,
                Status = p.Run.Status.ToString()
            })
            .ToListAsync(ct);

        return Ok(list);
    }

    [HttpGet("payslips/{id:long}")]
    public async Task<IActionResult> GetPayslipDetail(long id, CancellationToken ct)
    {
        long? empId = await ResolveCurrentEmployeeIdAsync(ct);
        if (empId is null) return NotFound(new { message = "No active employee profile linked to your user account." });

        var payslip = await _db.Payslips
            .AsNoTracking()
            .Include(p => p.Run)
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.PayslipId == id && p.EmployeeId == empId.Value, ct);

        if (payslip is null) return NotFound(new { message = $"Payslip {id} not found." });

        var components = await _db.SalaryComponents.AsNoTracking().ToDictionaryAsync(c => c.SalaryComponentId, c => c.Name, ct);

        string? empCode = null;
        string? empName = null;
        if (_tenant.CustomerId is Guid cid && _tenant.OrgId is Guid oid)
        {
            var p = await _hrm.FindByIdAsync(cid, oid, empId.Value, ct);
            empCode = p?.EmployeeCode;
            empName = p?.FullName;
        }

        var detail = new PayslipView
        {
            PayslipId = payslip.PayslipId,
            PayrollRunId = payslip.PayrollRunId,
            EmployeeId = payslip.EmployeeId,
            EmployeeCode = empCode,
            EmployeeName = empName,
            PaidDays = payslip.PaidDays,
            GrossEarnings = payslip.GrossEarnings,
            GrossDeductions = payslip.GrossDeductions,
            NetPay = payslip.NetPay,
            Lines = payslip.Lines.Select(l => new PayslipLineView
            {
                PayslipLineId = l.PayslipLineId,
                SalaryComponentId = l.SalaryComponentId,
                ComponentName = components.TryGetValue(l.SalaryComponentId, out var name) ? name : l.Kind.ToString(),
                Kind = l.Kind,
                Amount = l.Amount
            }).ToList()
        };

        return Ok(detail);
    }

    [HttpGet("payslips/{id:long}/download")]
    public async Task<IActionResult> DownloadPayslip(long id, CancellationToken ct)
    {
        long? empId = await ResolveCurrentEmployeeIdAsync(ct);
        if (empId is null) return NotFound(new { message = "No active employee profile linked to your user account." });

        var payslip = await _db.Payslips
            .AsNoTracking()
            .Include(p => p.Run)
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.PayslipId == id && p.EmployeeId == empId.Value, ct);

        if (payslip is null) return NotFound(new { message = $"Payslip {id} not found." });

        var components = await _db.SalaryComponents.AsNoTracking().ToDictionaryAsync(c => c.SalaryComponentId, c => c.Name, ct);

        string empCode = "";
        string empName = "Employee";
        if (_tenant.CustomerId is Guid cid && _tenant.OrgId is Guid oid)
        {
            var p = await _hrm.FindByIdAsync(cid, oid, empId.Value, ct);
            empCode = p?.EmployeeCode ?? "";
            empName = p?.FullName ?? "Employee";
        }

        string monthName = payslip.Run.Month.ToString("MMMM yyyy");
        var earnings = payslip.Lines.Where(l => l.Kind == ComponentKind.Earning).ToList();
        var deductions = payslip.Lines.Where(l => l.Kind == ComponentKind.Deduction).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset='utf-8'><title>Payslip - " + monthName + "</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: 'Segoe UI', Arial, sans-serif; margin: 40px; color: #1e293b; background: #fff; }");
        sb.AppendLine(".payslip-box { max-width: 800px; margin: 0 auto; border: 1px solid #cbd5e1; border-radius: 8px; padding: 24px; }");
        sb.AppendLine(".header { text-align: center; border-bottom: 2px solid #3b82f6; padding-bottom: 12px; margin-bottom: 20px; }");
        sb.AppendLine(".header h2 { margin: 0 0 4px 0; color: #1e3a8a; }");
        sb.AppendLine(".header p { margin: 0; color: #64748b; font-size: 14px; }");
        sb.AppendLine(".meta-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; font-size: 14px; margin-bottom: 20px; background: #f8fafc; padding: 12px; border-radius: 6px; }");
        sb.AppendLine(".meta-item { display: flex; justify-content: space-between; }");
        sb.AppendLine(".meta-label { color: #64748b; font-weight: 500; }");
        sb.AppendLine(".meta-val { font-weight: 600; color: #0f172a; }");
        sb.AppendLine(".table-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-bottom: 20px; }");
        sb.AppendLine("table { width: 100%; border-collapse: collapse; font-size: 13px; }");
        sb.AppendLine("th { background: #f1f5f9; padding: 8px; text-align: left; border-bottom: 1px solid #cbd5e1; }");
        sb.AppendLine("th:last-child { text-align: right; }");
        sb.AppendLine("td { padding: 8px; border-bottom: 1px solid #e2e8f0; }");
        sb.AppendLine("td:last-child { text-align: right; font-weight: 500; }");
        sb.AppendLine(".total-row td { font-weight: 700; background: #f8fafc; }");
        sb.AppendLine(".net-box { background: #eff6ff; border: 1px solid #bfdbfe; border-radius: 6px; padding: 16px; display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }");
        sb.AppendLine(".net-label { font-size: 16px; font-weight: 600; color: #1e40af; }");
        sb.AppendLine(".net-amount { font-size: 22px; font-weight: 800; color: #1d4ed8; }");
        sb.AppendLine(".footer { text-align: center; font-size: 12px; color: #94a3b8; border-top: 1px solid #e2e8f0; padding-top: 12px; }");
        sb.AppendLine("@media print { body { margin: 0; } .payslip-box { border: none; padding: 0; } }");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<div class='payslip-box'>");
        sb.AppendLine("  <div class='header'>");
        sb.AppendLine("    <h2>SALARY SLIP</h2>");
        sb.AppendLine($"    <p>For the month of {monthName}</p>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <div class='meta-grid'>");
        sb.AppendLine($"    <div class='meta-item'><span class='meta-label'>Employee Name:</span><span class='meta-val'>{empName}</span></div>");
        sb.AppendLine($"    <div class='meta-item'><span class='meta-label'>Employee Code:</span><span class='meta-val'>{empCode}</span></div>");
        sb.AppendLine($"    <div class='meta-item'><span class='meta-label'>Paid Days:</span><span class='meta-val'>{payslip.PaidDays:F1}</span></div>");
        sb.AppendLine($"    <div class='meta-item'><span class='meta-label'>Payment Mode:</span><span class='meta-val'>Bank Transfer</span></div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <div class='table-grid'>");
        sb.AppendLine("    <div>");
        sb.AppendLine("      <table><thead><tr><th>Earnings</th><th>Amount (₹)</th></tr></thead><tbody>");
        foreach (var e in earnings)
        {
            string name = components.TryGetValue(e.SalaryComponentId, out var n) ? n : e.Kind.ToString();
            sb.AppendLine($"        <tr><td>{name}</td><td>{e.Amount:N2}</td></tr>");
        }
        sb.AppendLine($"        <tr class='total-row'><td>Total Earnings</td><td>{payslip.GrossEarnings:N2}</td></tr>");
        sb.AppendLine("      </tbody></table>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div>");
        sb.AppendLine("      <table><thead><tr><th>Deductions</th><th>Amount (₹)</th></tr></thead><tbody>");
        foreach (var d in deductions)
        {
            string name = components.TryGetValue(d.SalaryComponentId, out var n) ? n : d.Kind.ToString();
            sb.AppendLine($"        <tr><td>{name}</td><td>{d.Amount:N2}</td></tr>");
        }
        sb.AppendLine($"        <tr class='total-row'><td>Total Deductions</td><td>{payslip.GrossDeductions:N2}</td></tr>");
        sb.AppendLine("      </tbody></table>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <div class='net-box'>");
        sb.AppendLine("    <div class='net-label'>NET SALARY PAYABLE:</div>");
        sb.AppendLine($"    <div class='net-amount'>₹ {payslip.NetPay:N2}</div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <div class='footer'>");
        sb.AppendLine("    <p>This is a computer generated document and does not require a physical signature.</p>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</div></body></html>");

        Response.Headers["Content-Disposition"] = $"inline; filename=payslip_{payslip.Run.Month:yyyy_MM}.html";
        return Content(sb.ToString(), "text/html", Encoding.UTF8);
    }

    [HttpGet("form16")]
    public async Task<IActionResult> GetForm16([FromQuery] string financialYear = "2026-2027", CancellationToken ct = default)
    {
        long? empId = await ResolveCurrentEmployeeIdAsync(ct);
        if (empId is null) return NotFound(new { message = "No active employee profile linked to your user account." });

        var form16 = await _taxService.GenerateForm16PartBAsync(empId.Value, financialYear, ct);
        return Ok(form16);
    }

    [HttpGet("tax-declarations")]
    public async Task<IActionResult> GetTaxDeclaration([FromQuery] string financialYear = "2026-2027", CancellationToken ct = default)
    {
        long? empId = await ResolveCurrentEmployeeIdAsync(ct);
        if (empId is null) return NotFound(new { message = "No active employee profile linked to your user account." });

        var decl = await _taxService.GetDeclarationAsync(empId.Value, financialYear, ct);
        return decl is null ? NotFound(new { message = "No tax declaration found for this financial year." }) : Ok(decl);
    }

    [HttpPut("tax-declarations")]
    public async Task<IActionResult> SaveTaxDeclaration([FromBody] SaveTaxDeclarationRequest request, CancellationToken ct = default)
    {
        long? empId = await ResolveCurrentEmployeeIdAsync(ct);
        if (empId is null) return NotFound(new { message = "No active employee profile linked to your user account." });

        request.EmployeeId = empId.Value;
        long id = await _taxService.SaveDeclarationAsync(request, ct);
        return Ok(new { id, message = "Tax declaration saved successfully." });
    }
}
