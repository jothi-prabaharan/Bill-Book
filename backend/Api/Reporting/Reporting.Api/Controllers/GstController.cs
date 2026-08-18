using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Reporting.Repository;
using Shared.Kernel.Tenancy;
using Reporting.Api.Services.Sources; // To reuse SalesRegisterRow if desired

namespace Reporting.Api.Controllers;

[ApiController]
[Route("api/reports/gst")]
public class GstController : ControllerBase
{
    private readonly ReportingDbContext _db;
    private readonly ITenantContext _tenant;

    public GstController(ReportingDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
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
    /// GSTR-3B: Monthly Summary.
    /// </summary>
    [HttpGet("gstr3b")]
    public async Task<IActionResult> GetGstr3b([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
    {
        // For now, only outward supplies are aggregated.
        var outward = await _db.SalesRegisters
            .Where(s => s.DocumentDate >= from && s.DocumentDate <= to)
            .GroupBy(s => s.IsInterState)
            .Select(g => new
            {
                InterState = g.Key,
                Taxable = g.Sum(s => s.TaxableAmount),
                Igst = g.Sum(s => s.IgstAmount),
                Cgst = g.Sum(s => s.CgstAmount),
                Sgst = g.Sum(s => s.SgstAmount),
                Total = g.Sum(s => s.TotalAmount)
            })
            .ToListAsync(ct);

        return Ok(new
        {
            Period = new { From = from, To = to },
            Table_3_1 = new
            {
                Description = "Outward supplies and inward supplies liable to reverse charge",
                Details = outward
            },
            Table_4 = new
            {
                Description = "Eligible ITC",
                Details = "Pending Purchase Register"
            }
        });
    }
}

