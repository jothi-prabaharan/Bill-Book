using Accounting.Api.Services;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;

namespace Accounting.Api.Controllers;

/// <summary>
/// Accounting › Budgets (TK-108, design "Budgeting"). Reading takes
/// <c>accounting.view</c>; entering and importing a draft take
/// <c>accounting.edit</c>; approving, revising and choosing the branch's
/// budget take <c>accounting.approve</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("accounting")]
[Route("api/budgets")]
[RequireApp(App.RetailErp)]
public sealed class BudgetsController : ControllerBase
{
    private const long MaxUploadBytes = 5 * 1024 * 1024;

    private readonly BudgetService _budgets;
    private readonly ICurrentUser _user;

    public BudgetsController(BudgetService budgets, ICurrentUser user)
    {
        _budgets = budgets;
        _user = user;
    }

    /// <summary>Every budget, newest year first; <paramref name="year"/> narrows to the years starting in it.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? year, CancellationToken ct) =>
        Ok(await _budgets.ListAsync(year, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) =>
        await _budgets.GetAsync(id, ct) is { } budget ? Ok(budget) : NotFound();

    /// <summary>Last financial year's income and expense actuals, month by month, for spreading by shape.</summary>
    [HttpGet("{id:long}/last-year")]
    public async Task<IActionResult> LastYear(long id, CancellationToken ct) =>
        await _budgets.LastYearAsync(id, ct) is { } lines ? Ok(lines) : NotFound();

    [HttpPost]
    [PermissionAction("edit")]
    public async Task<IActionResult> Create([FromBody] CreateBudgetRequest request, CancellationToken ct)
    {
        BudgetResult result = await _budgets.CreateAsync(request, ct);
        return result.Outcome == BudgetOutcome.Ok
            ? CreatedAtAction(nameof(Get), new { id = result.BudgetId }, new { budgetId = result.BudgetId })
            : Respond(result);
    }

    [HttpPut("{id:long}")]
    [PermissionAction("edit")]
    public async Task<IActionResult> Save(long id, [FromBody] SaveBudgetRequest request, CancellationToken ct) =>
        Respond(await _budgets.SaveAsync(id, request, ct));

    [HttpDelete("{id:long}")]
    [PermissionAction("edit")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct) =>
        Respond(await _budgets.DeleteAsync(id, ct));

    [HttpPost("{id:long}/approve")]
    [PermissionAction("approve")]
    public async Task<IActionResult> Approve(long id, [FromBody] ApproveBudgetRequest request, CancellationToken ct) =>
        _user.UserId is Guid actor
            ? Respond(await _budgets.ApproveAsync(id, request.MakeDefault, actor, ct))
            : Forbid();

    [HttpPost("{id:long}/make-default")]
    [PermissionAction("approve")]
    public async Task<IActionResult> MakeDefault(long id, CancellationToken ct) =>
        Respond(await _budgets.MakeDefaultAsync(id, ct));

    /// <summary>Copies the approved budget into a new draft, which supersedes it once approved.</summary>
    [HttpPost("{id:long}/revise")]
    [PermissionAction("approve")]
    public async Task<IActionResult> Revise(long id, [FromBody] ReviseBudgetRequest request, CancellationToken ct)
    {
        BudgetResult result = await _budgets.ReviseAsync(id, request.Reason, ct);
        return result.Outcome == BudgetOutcome.Ok
            ? Ok(new { budgetId = result.BudgetId })
            : Respond(result);
    }

    /// <summary>The budget as a sheet, <c>csv</c> or <c>xlsx</c>, in the layout the import reads back.</summary>
    [HttpGet("{id:long}/export")]
    public async Task<IActionResult> Export(long id, [FromQuery] string? format, CancellationToken ct)
    {
        StatementExport? export = await _budgets.ExportAsync(id, ct);
        if (export is null)
        {
            return NotFound();
        }

        bool excel = string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase);
        return File(
            excel ? StatementExportWriter.ToExcel(export) : StatementExportWriter.ToCsv(export),
            excel ? StatementExportWriter.ExcelContentType : StatementExportWriter.CsvContentType,
            $"{export.Name}.{(excel ? "xlsx" : "csv")}");
    }

    /// <summary>Replaces a draft's lines with a CSV or XLSX sheet in the export layout.</summary>
    [HttpPost("{id:long}/import")]
    [PermissionAction("edit")]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IActionResult> Import(long id, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new MessageResponse { Message = "Choose a file to import." });
        }

        if (file.Length > MaxUploadBytes)
        {
            return BadRequest(new MessageResponse { Message = "That file is too large for a budget. Check it is the right file." });
        }

        StatementGrid grid;
        await using (Stream content = file.OpenReadStream())
        {
            try
            {
                grid = file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
                    ? ExcelStatementReader.Read(content, 0, hasHeaderRow: true)
                    : CsvStatementReader.Read(content, 0, hasHeaderRow: true);
            }
            catch (StatementReadException ex)
            {
                // The reader's own curated sentence, as the statement import uses it.
                return UnprocessableEntity(new MessageResponse { Message = ex.Message });
            }
        }

        return Respond(await _budgets.ImportAsync(id, grid.Rows, ct));
    }

    private IActionResult Respond(BudgetResult result) => result.Outcome switch
    {
        BudgetOutcome.Ok => NoContent(),
        BudgetOutcome.NotFound => NotFound(),
        BudgetOutcome.Locked => Conflict(new MessageResponse { Message = result.Detail! }),
        BudgetOutcome.UnknownCodes => UnprocessableEntity(new { message = result.Detail, unknownCodes = result.UnknownCodes }),
        _ => UnprocessableEntity(new MessageResponse { Message = result.Detail ?? "The budget was refused." }),
    };
}
