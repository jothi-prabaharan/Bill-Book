using System.Threading;
using System.Threading.Tasks;
using Accounting.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;

namespace Accounting.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("accounting")]
[Route("api/reconciliation")]
public class ReconciliationController : ControllerBase
{
    private readonly ReconciliationService _reconciliation;

    public ReconciliationController(ReconciliationService reconciliation)
    {
        _reconciliation = reconciliation;
    }

    [HttpGet("{bankStatementId:long}/suggestions")]
    public async Task<IActionResult> GetSuggestions(long bankStatementId, CancellationToken ct)
    {
        var suggestions = await _reconciliation.GetSuggestedMatchesAsync(bankStatementId, ct);
        return Ok(suggestions);
    }

    [HttpPost("reconcile")]
    public async Task<IActionResult> Reconcile([FromBody] ReconcileRequest request, CancellationToken ct)
    {
        await _reconciliation.ReconcileAsync(request.BankStatementLineId, request.JournalLedgerId, ct);
        return NoContent();
    }
}

public class ReconcileRequest
{
    public long BankStatementLineId { get; set; }
    public long JournalLedgerId { get; set; }
}
