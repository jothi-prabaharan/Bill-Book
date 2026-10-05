using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Reporting.Repository;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;
using Shared.Kernel.Apps;
using Reporting.Api.Services;
using Reporting.Repository.Repositories;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;

namespace Reporting.Api.Controllers;

/// <summary>
/// GSTR-1, GSTR-2 and GSTR-3B — the branch's whole outward and inward supply
/// position, filed from sal.SalesRegister.
///
/// It had a credential and no permission: Reporting's FallbackPolicy asked for
/// an authenticated user and nothing asked which one, so every signed-in user
/// could read the returns regardless of their role. The Sales role is the sharp
/// case — it is granted sales, contacts and crm and holds no reports permission
/// at all, yet this was the one reports route it could open. The other three
/// controllers in this service have asked for reports.* from the start; this is
/// the same guard, not a new rule.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("reports")]
[Route("api/reports/gst")]
[RequireBusinessType(BusinessType.General, BusinessType.Pharma, BusinessType.Jewellery, BusinessType.Restaurant)]
public class GstController : ControllerBase
{
    private readonly ReportingDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly BatchedNameResolverRepository _names;
    private readonly IOrgIdentityProvider _identity;

    public GstController(ReportingDbContext db, ITenantContext tenant, BatchedNameResolverRepository names, IOrgIdentityProvider identity)
    {
        _db = db;
        _tenant = tenant;
        _names = names;
        _identity = identity;
    }

    /// <summary>
    /// GSTR-1: Outward Supplies (Sales).
    /// Extracts B2B, B2C, Credit Notes for filing.
    /// </summary>
    [HttpGet("gstr1")]
    public async Task<IActionResult> GetGstr1([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
    {
        var sales = await _db.SalesRegisters
            .Where(s => s.DocumentDate >= from && s.DocumentDate <= to)
            .OrderBy(s => s.DocumentDate)
            .AsNoTracking()
            .ToListAsync(ct);

        var contacts = await _db.Contacts
            .Where(c => sales.Select(s => s.ContactId).Distinct().Contains(c.ContactId))
            .ToDictionaryAsync(c => c.ContactId, ct);

        // Group into B2B and B2C based on Contact GSTIN
        var b2b = new List<object>();
        var b2c = new List<object>();

        foreach (var sale in sales)
        {
            var contact = contacts.GetValueOrDefault(sale.ContactId);
            var isB2b = contact != null && !string.IsNullOrWhiteSpace(sale.ContactGstin);

            var entry = new
            {
                Type = sale.TransactionTypeCode,
                DocumentNo = sale.DocumentNo,
                Date = sale.DocumentDate.ToString("O"),
                Customer = contact?.DisplayName ?? "Unknown",
                Gstin = sale.ContactGstin,
                State = sale.PlaceOfSupplyStateId.ToString(),
                Taxable = sale.TaxableAmount,
                Cgst = sale.CgstAmount,
                Sgst = sale.SgstAmount,
                Igst = sale.IgstAmount,
                Total = sale.TotalAmount
            };

            if (isB2b) b2b.Add(entry);
            else b2c.Add(entry);
        }

        return Ok(new
        {
            Period = new { From = from, To = to },
            Summary = new
            {
                B2B_Count = b2b.Count,
                B2C_Count = b2c.Count,
                TotalTaxable = sales.Sum(s => s.TaxableAmount),
                TotalTax = sales.Sum(s => s.CgstAmount + s.SgstAmount + s.IgstAmount + s.CessAmount)
            },
            B2B = b2b,
            B2C = b2c
        });
    }

    /// <summary>
    /// GSTR-2: Inward Supplies (Purchases).
    /// Currently Not Implemented as Purchase Register is not yet mapped.
    /// </summary>
    [HttpGet("gstr2")]
    public IActionResult GetGstr2([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        return StatusCode(501, new { Message = "GSTR-2 not yet implemented (Purchase Register pending)" });
    }

    /// <summary>
    /// GSTR-3B for the period (TK-115): outward supplies and reverse-charge
    /// inward supplies in table 3.1, claimable credit in 4A, blocked and
    /// ineligible credit apart in 4D, and the tax payable and net of credit.
    /// From the sales and purchase registers; see <see cref="Gstr3bRepository"/> for each rule.
    /// </summary>
    [HttpGet("gstr3b")]
    public async Task<IActionResult> GetGstr3b([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
    {
        List<SalesRegisterRead> sales = await _db.SalesRegisters.AsNoTracking()
            .Where(s => s.DocumentDate >= from && s.DocumentDate <= to)
            .ToListAsync(ct);

        List<PurchaseRegisterRead> purchases = await _db.PurchaseRegisters.AsNoTracking()
            .Where(p => p.DocumentDate >= from && p.DocumentDate <= to)
            .ToListAsync(ct);

        return Ok(Gstr3bRepository.Build(from, to, sales, purchases, await _names.GetStateCodesAsync(ct)));
    }

    /// <summary>
    /// GSTR-1 as the GST offline tool's JSON (TK-115), refused with the list of
    /// problems when the branch GSTIN, a buyer's GSTIN, an HSN code or the HSN
    /// summary would make the portal refuse it. An export, so it needs
    /// <c>reports.export</c>.
    /// </summary>
    [HttpGet("gstr1/export")]
    [PermissionAction("export")]
    public async Task<IActionResult> ExportGstr1([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
    {
        if (to < from)
        {
            return BadRequest(new { message = "The period ends before it starts." });
        }

        List<SalesRegisterRead> rows = await _db.SalesRegisters.AsNoTracking()
            .Where(s => s.DocumentDate >= from && s.DocumentDate <= to)
            .ToListAsync(ct);

        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.DocumentDate >= from && i.DocumentDate <= to
                && (i.Status == DocumentStatus.Posted || i.Status == DocumentStatus.Void))
            .Select(i => new { i.DocumentNo, Void = i.Status == DocumentStatus.Void })
            .ToListAsync(ct);

        var creditNotes = await _db.CreditNotes.AsNoTracking()
            .Where(c => c.DocumentDate >= from && c.DocumentDate <= to
                && (c.Status == DocumentStatus.Posted || c.Status == DocumentStatus.Void))
            .Select(c => new { c.DocumentNo, Void = c.Status == DocumentStatus.Void })
            .ToListAsync(ct);

        List<IssuedDocuments> issued = [];
        if (invoices.Count > 0)
        {
            issued.Add(new IssuedDocuments(
                Gstr1ExportRepository.InvoicesNature,
                invoices.Select(i => i.DocumentNo).Min(StringComparer.Ordinal)!,
                invoices.Select(i => i.DocumentNo).Max(StringComparer.Ordinal)!,
                invoices.Count,
                invoices.Count(i => i.Void)));
        }

        if (creditNotes.Count > 0)
        {
            issued.Add(new IssuedDocuments(
                Gstr1ExportRepository.CreditNotesNature,
                creditNotes.Select(c => c.DocumentNo).Min(StringComparer.Ordinal)!,
                creditNotes.Select(c => c.DocumentNo).Max(StringComparer.Ordinal)!,
                creditNotes.Count,
                creditNotes.Count(c => c.Void)));
        }

        OrgIdentity? branch = await _identity.GetIdentityAsync(ct);
        Gstr1ExportResult result = Gstr1ExportRepository.Build(
            branch?.Gstin, from, to, rows, await _names.GetStateCodesAsync(ct), issued);

        if (!result.IsValid)
        {
            return UnprocessableEntity(new
            {
                message = "GSTR-1 cannot be exported until these are corrected.",
                problems = result.Problems,
            });
        }

        byte[] file = System.Text.Encoding.UTF8.GetBytes(result.Payload!.ToJsonString());
        return File(file, "application/json", $"GSTR1-{to:MMyyyy}.json");
    }
}
