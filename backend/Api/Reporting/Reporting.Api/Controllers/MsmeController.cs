using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Reporting.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Documents;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Reporting.Api.Controllers;

public class MsmeSummaryDto
{
    public DateOnly AsOf { get; set; }
    public decimal TotalMsmePayables { get; set; }
    public decimal Within15Days { get; set; }
    public decimal Days16To45 { get; set; }
    public decimal OverduePast45Days { get; set; }
    public decimal DisallowedUnder43Bh { get; set; }
    public int MsmeVendorCount { get; set; }
    public int OverdueBillCount { get; set; }
}

public class McaMsmeForm1Item
{
    public long BillId { get; set; }
    public string VendorName { get; set; } = null!;
    public string? Pan { get; set; }
    public string? UdyamNumber { get; set; }
    public string BillNo { get; set; } = null!;
    public DateOnly BillDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal AmountDue { get; set; }
    public int DaysDelayed { get; set; }
    public string ReasonForDelay { get; set; } = "Pending reconciliation";
}

/// <summary>
/// MSME statutory reports and half-yearly MCA Form 1 export (TK-119).
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("reports")]
[Route("api/reports/msme")]
[RequireApp(App.RetailErp)]
public sealed class MsmeController : ControllerBase
{
    private readonly ReportingDbContext _db;
    private readonly ITenantContext _tenant;

    public MsmeController(ReportingDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    /// <summary>
    /// Summary KPIs for MSME payables and Section 43B(h) disallowance.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] DateOnly? asOf = null, CancellationToken ct = default)
    {
        DateOnly date = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var query = from b in _db.Bills
                    where b.Status == DocumentStatus.Posted && b.DocumentDate <= date
                    join c in _db.Contacts on b.ContactId equals c.ContactId
                    where c.IsMsme
                    select new
                    {
                        b.BillId,
                        b.TotalAmount,
                        b.DocumentDate,
                        c.ContactId,
                        DaysAge = date.DayNumber - b.DocumentDate.DayNumber,
                    };

        var items = await query.ToListAsync(ct);

        var summary = new MsmeSummaryDto
        {
            AsOf = date,
            TotalMsmePayables = items.Sum(x => x.TotalAmount),
            Within15Days = items.Where(x => x.DaysAge <= 15).Sum(x => x.TotalAmount),
            Days16To45 = items.Where(x => x.DaysAge > 15 && x.DaysAge <= 45).Sum(x => x.TotalAmount),
            OverduePast45Days = items.Where(x => x.DaysAge > 45).Sum(x => x.TotalAmount),
            DisallowedUnder43Bh = items.Where(x => x.DaysAge > 45).Sum(x => x.TotalAmount),
            MsmeVendorCount = items.Select(x => x.ContactId).Distinct().Count(),
            OverdueBillCount = items.Count(x => x.DaysAge > 45),
        };

        return Ok(summary);
    }

    /// <summary>
    /// Half-yearly return for MCA MSME Form 1 (outstanding payments to MSME suppliers exceeding 45 days).
    /// </summary>
    [HttpGet("mca-form1")]
    public async Task<IActionResult> GetMcaForm1(
        [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, CancellationToken ct = default)
    {
        DateOnly endDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly startDate = from ?? endDate.AddMonths(-6);

        var query = from b in _db.Bills
                    where b.Status == DocumentStatus.Posted && b.DocumentDate >= startDate && b.DocumentDate <= endDate
                    join c in _db.Contacts on b.ContactId equals c.ContactId
                    where c.IsMsme
                    let daysDelayed = endDate.DayNumber - b.DocumentDate.DayNumber - 45
                    where daysDelayed > 0
                    select new McaMsmeForm1Item
                    {
                        BillId = b.BillId,
                        VendorName = c.DisplayName,
                        Pan = c.Pan,
                        UdyamNumber = c.UdyamNumber,
                        BillNo = b.VendorBillNo ?? b.DocumentNo,
                        BillDate = b.DocumentDate,
                        DueDate = b.DueDate,
                        AmountDue = b.TotalAmount,
                        DaysDelayed = daysDelayed,
                        ReasonForDelay = "Verification of service quality / invoice settlement process",
                    };

        var list = await query.OrderByDescending(x => x.DaysDelayed).ToListAsync(ct);
        return Ok(list);
    }

    /// <summary>
    /// CSV export for MCA MSME Form 1 upload.
    /// </summary>
    [HttpGet("mca-form1/csv")]
    public async Task<IActionResult> ExportMcaForm1Csv(
        [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, CancellationToken ct = default)
    {
        DateOnly endDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly startDate = from ?? endDate.AddMonths(-6);

        var query = from b in _db.Bills
                    where b.Status == DocumentStatus.Posted && b.DocumentDate >= startDate && b.DocumentDate <= endDate
                    join c in _db.Contacts on b.ContactId equals c.ContactId
                    where c.IsMsme
                    let daysDelayed = endDate.DayNumber - b.DocumentDate.DayNumber - 45
                    where daysDelayed > 0
                    select new McaMsmeForm1Item
                    {
                        BillId = b.BillId,
                        VendorName = c.DisplayName,
                        Pan = c.Pan,
                        UdyamNumber = c.UdyamNumber,
                        BillNo = b.VendorBillNo ?? b.DocumentNo,
                        BillDate = b.DocumentDate,
                        DueDate = b.DueDate,
                        AmountDue = b.TotalAmount,
                        DaysDelayed = daysDelayed,
                        ReasonForDelay = "Under verification",
                    };

        var list = await query.OrderByDescending(x => x.DaysDelayed).ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("Vendor Name,PAN,Udyam Number,Bill No,Bill Date,Due Date,Amount Due,Days Delayed,Reason For Delay");
        foreach (var item in list)
        {
            sb.AppendLine($"\"{EscapeCsv(item.VendorName)}\",\"{item.Pan}\",\"{item.UdyamNumber}\",\"{EscapeCsv(item.BillNo)}\",{item.BillDate:yyyy-MM-dd},{item.DueDate:yyyy-MM-dd},{item.AmountDue},{item.DaysDelayed},\"{EscapeCsv(item.ReasonForDelay)}\"");
        }

        byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"mca-msme-form1-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}.csv");
    }

    private static string EscapeCsv(string s) => s.Replace("\"", "\"\"");
}
