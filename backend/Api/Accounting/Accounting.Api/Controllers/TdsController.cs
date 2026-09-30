using System.Text;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Shared.Kernel.Tax;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Controllers;

/// <summary>
/// Accounting â€º TDS (TK-117): the section master, the register, what is due
/// to be deposited, the 26Q export and the Form 26AS reconciliation. Reading
/// takes <c>accounting.view</c>, the section master <c>accounting.create</c>
/// and <c>accounting.edit</c>, the export <c>accounting.export</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("accounting")]
[Route("api/tds")]
[RequireBusinessType(BusinessType.General, BusinessType.Pharma, BusinessType.Jewellery, BusinessType.School)]
public sealed class TdsController : ControllerBase
{
    private const long MaxUploadBytes = 5 * 1024 * 1024;

    private readonly IServiceProvider _services;

    public TdsController(IServiceProvider services) => _services = services;

    private TdsSectionRepository Sections => _services.GetRequiredService<TdsSectionRepository>();

    private TdsRepository Register => _services.GetRequiredService<TdsRepository>();

    private Form26asRepository Statements => _services.GetRequiredService<Form26asRepository>();

    [HttpGet("sections")]
    public async Task<IActionResult> ListSections(CancellationToken ct) => Ok(await Sections.ListAsync(ct));

    [HttpPost("sections")]
    public Task<IActionResult> CreateSection([FromBody] SaveTdsSectionRequest request, CancellationToken ct) =>
        SaveSection(null, request, ct);

    [HttpPut("sections/{id:long}")]
    public Task<IActionResult> UpdateSection(long id, [FromBody] SaveTdsSectionRequest request, CancellationToken ct) =>
        SaveSection(id, request, ct);

    private async Task<IActionResult> SaveSection(long? id, SaveTdsSectionRequest request, CancellationToken ct)
    {
        SaveTdsSectionResult result = await Sections.SaveAsync(id, request, ct);

        return result.Outcome switch
        {
            SaveTdsSectionOutcome.Ok => Ok(new { tdsSectionId = result.TdsSectionId }),
            SaveTdsSectionOutcome.NotFound => NotFound(),
            SaveTdsSectionOutcome.Duplicate => Conflict(new MessageResponse
            {
                Message = "That section already has a row starting on this date. Edit that row instead.",
            }),
            SaveTdsSectionOutcome.BadPeriod => BadRequest(new MessageResponse
            {
                Message = "The period must end on or after it starts, and must not overlap another row of the same section. "
                    + "To revise a rate, end the current row the day before the new one starts.",
            }),
            SaveTdsSectionOutcome.CodeInUse => Conflict(new MessageResponse
            {
                Message = "Deductions have been recorded under this section, so its code cannot change. Add a new section instead.",
            }),
            _ => BadRequest(),
        };
    }

    /// <summary>What a document would owe, for the form to show before posting.</summary>
    [HttpPost("quote")]
    [PermissionAction("view")]
    public async Task<IActionResult> Quote([FromBody] TdsPreviewRequest request, CancellationToken ct)
    {
        TenantContext tenant = _services.GetRequiredService<TenantContext>();
        TdsQuoteResult result = await Register.QuoteAsync(new TdsQuoteRequest
        {
            CustomerId = tenant.CustomerId ?? Guid.Empty,
            OrgId = tenant.OrgId ?? Guid.Empty,
            ContactId = request.ContactId,
            TransactionTypeCode = string.IsNullOrWhiteSpace(request.TransactionTypeCode) ? "NEW" : request.TransactionTypeCode,
            TransactionId = request.TransactionId ?? 0,
            Date = request.Date,
            Amount = request.Amount,
            Kind = request.Kind,
        }, ct);

        return result.Outcome switch
        {
            TdsQuoteOutcome.Ok => Ok(result.Quote),
            TdsQuoteOutcome.Refused => UnprocessableEntity(new MessageResponse { Message = result.Detail ?? "TDS could not be worked out." }),
            _ => StatusCode(StatusCodes.Status503ServiceUnavailable, new MessageResponse
            {
                Message = "The contact's TDS details could not be read, so the TDS could not be worked out. Try again.",
            }),
        };
    }

    [HttpGet("register")]
    public async Task<IActionResult> ListRegister(
        [FromQuery] int financialYear, [FromQuery] TdsDirection direction = TdsDirection.Deducted, CancellationToken ct = default)
    {
        List<TdsEntryView> rows = await Register.ListAsync(financialYear, direction, ct);

        var names = await _services.GetRequiredService<Shared.Kernel.Documents.IContactNameLookup>()
            .ResolveAsync([.. rows.Select(r => r.ContactId).Distinct()], ct);
        foreach (TdsEntryView row in rows)
        {
            row.ContactName = names.TryGetValue(row.ContactId, out var named) ? named.Name : null;
        }

        return Ok(rows);
    }

    /// <summary>The register row for one document, or 404 when it has none.</summary>
    [HttpGet("documents/{typeCode}/{id:long}")]
    public async Task<IActionResult> ForDocument(string typeCode, long id, CancellationToken ct) =>
        await Register.ForDocumentAsync(typeCode, id, ct) is { } row ? Ok(row) : NotFound();

    [HttpGet("outstanding")]
    public async Task<IActionResult> Outstanding(CancellationToken ct) => Ok(await Register.OutstandingAsync(ct));

    /// <summary>The quarter's 26Q data as a zip of the challan and deductee sheets.</summary>
    [HttpGet("26q")]
    [PermissionAction("export")]
    public async Task<IActionResult> Export26q([FromQuery] int financialYear, [FromQuery] int quarter, CancellationToken ct)
    {
        if (quarter is < 1 or > 4 || financialYear is < 2000 or > 2100)
        {
            return BadRequest(new MessageResponse { Message = "Choose a financial year and a quarter from 1 to 4." });
        }

        Form26qExport export = await _services.GetRequiredService<Form26qRepository>().ExportAsync(financialYear, quarter, ct);
        return File(export.Zip, "application/zip", $"26Q-{financialYear}-{(financialYear + 1) % 100:00}-Q{quarter}.zip");
    }

    /// <summary>The same quarter's counts and problems, for the page to list before the download.</summary>
    [HttpGet("26q/check")]
    public async Task<IActionResult> Check26q([FromQuery] int financialYear, [FromQuery] int quarter, CancellationToken ct)
    {
        if (quarter is < 1 or > 4 || financialYear is < 2000 or > 2100)
        {
            return BadRequest(new MessageResponse { Message = "Choose a financial year and a quarter from 1 to 4." });
        }

        Form26qExport export = await _services.GetRequiredService<Form26qRepository>().ExportAsync(financialYear, quarter, ct);
        return Ok(new Form26qCheck { Challans = export.Challans, Deductions = export.Deductions, Warnings = [.. export.Warnings] });
    }

    [HttpGet("26as")]
    public async Task<IActionResult> ListStatements(CancellationToken ct) => Ok(await Statements.ListAsync(ct));

    [HttpGet("26as/{id:long}")]
    public async Task<IActionResult> GetStatement(long id, CancellationToken ct) =>
        await Statements.GetAsync(id, ct) is { } detail ? Ok(detail) : NotFound();

    [HttpPost("26as")]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IActionResult> ImportStatement(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new MessageResponse { Message = "Choose the Form 26AS text file to import." });
        }

        if (file.Length > MaxUploadBytes)
        {
            return BadRequest(new MessageResponse { Message = "That file is too large for a Form 26AS statement. Check it is the right file." });
        }

        string text;
        using (StreamReader reader = new(file.OpenReadStream(), Encoding.UTF8))
        {
            text = await reader.ReadToEndAsync(ct);
        }

        Form26asImportResult result = await Statements.ImportAsync(file.FileName, text, ct);

        return result.Outcome == Form26asImportOutcome.Ok
            ? Ok(new { form26asImportId = result.Form26asImportId })
            : UnprocessableEntity(new MessageResponse { Message = result.Detail ?? "The file could not be read." });
    }

    [HttpDelete("26as/{id:long}")]
    public async Task<IActionResult> DeleteStatement(long id, CancellationToken ct) =>
        await Statements.DeleteAsync(id, ct) ? NoContent() : NotFound();
}

/// <summary>A form's request to see the TDS a document would owe.</summary>
public sealed class TdsPreviewRequest
{
    public long ContactId { get; set; }

    public DateOnly Date { get; set; }

    public decimal Amount { get; set; }

    /// <summary>1 a bill, 2 an advance.</summary>
    public int Kind { get; set; } = 1;

    /// <summary>The document being edited, so its own earlier row is not counted; empty for a new one.</summary>
    public string? TransactionTypeCode { get; set; }

    public long? TransactionId { get; set; }
}

/// <summary>A quarter's 26Q counts and what the return utility would refuse.</summary>
public sealed class Form26qCheck
{
    public int Challans { get; set; }

    public int Deductions { get; set; }

    public List<string> Warnings { get; set; } = [];
}
