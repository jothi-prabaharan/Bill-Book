using Fee.Api.Services;
using Fee.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Fee.Api.Controllers;

/// <summary>
/// Fee demands and receipts (S4, TK-64). Generating demands and taking a
/// receipt take <c>fee.create</c>; posting demands takes <c>fee.approve</c>;
/// voiding either takes <c>fee.void</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("fee")]
[RequireApp(App.School)]
[Route("api/fee")]
public sealed class FeeDocumentsController : ControllerBase
{
    private readonly DemandService _demands;
    private readonly ReceiptService _receipts;

    public FeeDocumentsController(DemandService demands, ReceiptService receipts)
    {
        _demands = demands;
        _receipts = receipts;
    }

    [HttpGet("demands")]
    public async Task<IActionResult> Demands(
        [FromQuery] long? contactId, [FromQuery] long? feeStructureId, [FromQuery] string? periodKey, [FromQuery] bool open, CancellationToken ct) =>
        Ok(await _demands.ListAsync(contactId, feeStructureId, periodKey, open, ct));

    [HttpPost("demands/generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateDemandsRequest request, CancellationToken ct) =>
        this.Answer(await _demands.GenerateAsync(request, ct));

    [HttpPost("demands/post")]
    [PermissionAction("approve")]
    public async Task<IActionResult> Post([FromBody] PostDemandsRequest request, CancellationToken ct) =>
        this.Answer(await _demands.PostAsync(request, ct));

    [HttpPost("demands/{id:long}/void")]
    [PermissionAction("void")]
    public async Task<IActionResult> VoidDemand(long id, [FromBody] VoidRequest request, CancellationToken ct) =>
        this.Answer(await _demands.VoidAsync(id, request.Reason, ct));

    [HttpGet("receipts")]
    public async Task<IActionResult> Receipts([FromQuery] long? contactId, CancellationToken ct) =>
        Ok(await _receipts.ListAsync(contactId, ct));

    [HttpPost("receipts")]
    public async Task<IActionResult> CreateReceipt([FromBody] SaveReceiptRequest request, CancellationToken ct) =>
        this.Answer(await _receipts.CreateAsync(request, ct));

    [HttpPost("receipts/{id:long}/void")]
    [PermissionAction("void")]
    public async Task<IActionResult> VoidReceipt(long id, [FromBody] VoidRequest request, CancellationToken ct) =>
        this.Answer(await _receipts.VoidAsync(id, request.Reason, ct));
}
