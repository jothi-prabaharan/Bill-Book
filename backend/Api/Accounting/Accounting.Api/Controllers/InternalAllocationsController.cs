using Accounting.Api.Services;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Controllers;

/// <summary>
/// Where every other service records a document-to-document allocation — a
/// credit note against the invoice it settles, a debit note against the bill it
/// corrects — and removes one when the document is voided.
///
/// Guarded by the shared internal key and carrying the tenant in the body, like
/// the ledger door: the callers are other services rather than a signed-in user,
/// and a posting that only worked while somebody was logged in would stop
/// settling outside office hours.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/allocations")]
public sealed class InternalAllocationsController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalAllocationsController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost]
    public async Task<IActionResult> Allocate(
        [FromBody] AllocateTransactionRequest request, CancellationToken ct)
    {
        if (request.CustomerId == Guid.Empty || request.OrgId == Guid.Empty)
        {
            return BadRequest(new MessageResponse
            {
                Message = "A customer and an organization are required to allocate.",
            });
        }

        // Set before anything resolves a DbContext: the context is built from
        // the tenant, so resolving the service first would bind it to no tenant.
        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;

        var allocations = _services.GetRequiredService<AllocationService>();

        AllocationResult result = await allocations.AllocateAsync(request, ct);

        return result.Outcome switch
        {
            AllocationOutcome.Ok => Ok(),

            // Transient. The caller can retry against the fresh state.
            AllocationOutcome.Retry => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new MessageResponse
                {
                    Message = result.Message
                        ?? "The allocation raced another write and was not applied. Retry.",
                }),

            _ => StatusCode(StatusCodes.Status409Conflict, new MessageResponse
            {
                Message = result.Message ?? $"The allocation was refused: {result.Outcome}.",
            }),
        };
    }

    /// <summary>
    /// Removes a source document's allocation rows — what a void does. A voided
    /// credit note takes its claims with it, or the invoices it named stay
    /// partially allocated to a document that no longer exists.
    ///
    /// A POST rather than a DELETE because the tenant travels in the body, the
    /// same way it does on every other internal call.
    /// </summary>
    [HttpPost("remove")]
    public async Task<IActionResult> Remove(
        [FromBody] RemoveAllocationsRequest request, CancellationToken ct)
    {
        if (request.CustomerId == Guid.Empty || request.OrgId == Guid.Empty)
        {
            return BadRequest(new MessageResponse
            {
                Message = "A customer and an organization are required to allocate.",
            });
        }

        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;

        var allocations = _services.GetRequiredService<AllocationService>();

        await allocations.RemoveAllocationsAsync(
            request.SourceTransactionTypeCode.ToUpperInvariant(),
            request.SourceTransactionId,
            ct);

        return Ok();
    }
}