using Banking.Api.Services;
using Banking.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;

namespace Banking.Api.Controllers;

/// <summary>
/// Banking › Statements. Importing what the bank says happened, and reconciling
/// it against what was recorded.
///
/// <b>Nothing here posts.</b> Every route on this controller reads a file, reads
/// the books, or records a decision about which line corresponds to which
/// document. The general ledger is untouched by all of it — reconciliation
/// compares two accounts of the same events rather than producing a third.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("banking")]
[Route("api/statements")]
public sealed class BankStatementsController : ControllerBase
{
    /// <summary>
    /// Statements are a few hundred rows of text. Ten megabytes is far more than
    /// any of them and far less than a file somebody uploaded by mistake — and
    /// the limit exists because the whole file is read into memory to be parsed.
    /// </summary>
    private const long MaxUploadBytes = 10 * 1024 * 1024;

    private readonly BankStatementService _statements;

    public BankStatementsController(BankStatementService statements) => _statements = statements;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] long? bankAccountId, CancellationToken ct) =>
        Ok(await _statements.ListAsync(bankAccountId, ct));

    [HttpGet("{bankStatementId:long}/lines")]
    public async Task<IActionResult> Lines(long bankStatementId, CancellationToken ct)
    {
        IReadOnlyList<StatementLineView>? lines =
            await _statements.LinesAsync(bankStatementId, ct);

        return lines is null ? NotFound() : Ok(lines);
    }

    [HttpGet("profiles/{bankAccountId:long}")]
    public async Task<IActionResult> Profile(long bankAccountId, CancellationToken ct)
    {
        ImportProfileView? profile = await _statements.GetProfileAsync(bankAccountId, ct);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut("profiles")]
    public async Task<IActionResult> SaveProfile(
        [FromBody] SaveImportProfileRequest request, CancellationToken ct) =>
        Respond(await _statements.SaveProfileAsync(request, ct), NoContent);

    /// <summary>
    /// Uploads a statement file. Multipart rather than a JSON body of base64:
    /// these are spreadsheets straight out of a bank portal, and asking a browser
    /// to encode one to send it is work with nothing to show for it.
    /// </summary>
    [HttpPost("import")]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IActionResult> Import(
        [FromForm] long bankAccountId, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new MessageResponse { Message = "Choose a file to import." });
        }

        if (file.Length > MaxUploadBytes)
        {
            return BadRequest(new MessageResponse
            {
                Message = $"That file is larger than {MaxUploadBytes / (1024 * 1024)} MB. "
                    + "A bank statement should be a fraction of that — check it is the right file.",
            });
        }

        await using Stream content = file.OpenReadStream();

        StatementResult result = await _statements.ImportAsync(
            bankAccountId, file.FileName, content, ct);

        return Respond(result, () => Ok(new MessageResponse
        {
            Message = result.Detail ?? "Imported.",
        }));
    }

    [HttpPost("lines/{bankStatementLineId:long}/match")]
    public async Task<IActionResult> Match(
        long bankStatementLineId,
        [FromBody] MatchStatementLineRequest request,
        CancellationToken ct) =>
        Respond(await _statements.MatchAsync(bankStatementLineId, request, ct), NoContent);

    [HttpPost("lines/{bankStatementLineId:long}/unmatch")]
    public async Task<IActionResult> Unmatch(long bankStatementLineId, CancellationToken ct) =>
        Respond(await _statements.UnmatchAsync(bankStatementLineId, ct), NoContent);

    [HttpPost("lines/{bankStatementLineId:long}/ignore")]
    public async Task<IActionResult> Ignore(
        long bankStatementLineId,
        [FromBody] IgnoreStatementLineRequest request,
        CancellationToken ct) =>
        Respond(await _statements.IgnoreAsync(bankStatementLineId, request, ct), NoContent);

    /// <summary>
    /// Downloads the statement with its reconciliation beside it. <c>format</c> is
    /// <c>csv</c> or <c>xlsx</c>.
    /// </summary>
    [HttpGet("{bankStatementId:long}/export")]
    public async Task<IActionResult> Export(
        long bankStatementId, [FromQuery] string format, CancellationToken ct)
    {
        StatementExport? export = await _statements.ExportAsync(bankStatementId, ct);

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

    private IActionResult Respond(StatementResult result, Func<IActionResult> onOk) =>
        result.Outcome switch
        {
            StatementOutcome.Ok => onOk(),
            StatementOutcome.NotFound => NotFound(),

            // 409, not 400: nothing about the request is malformed. The state it
            // is aimed at is what refuses it, and no amount of editing the
            // request changes that.
            StatementOutcome.AlreadyDecided or StatementOutcome.CandidateUnusable =>
                Conflict(new MessageResponse
                {
                    Message = result.Detail ?? "That line cannot be matched to that document.",
                }),

            // The account has no mapping yet, which is a step the user has to
            // take rather than an error in what they sent.
            StatementOutcome.ProfileMissing => UnprocessableEntity(new MessageResponse
            {
                Message = result.Detail ?? "Set up the import mapping for this account first.",
            }),

            _ => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? $"The statement was refused: {result.Outcome}.",
            }),
        };
}
