using Accounting.Api.Services;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Controllers;

/// <summary>
/// The user-facing public door for document-to-document allocations (e.g. Credit Notes to Invoices).
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("accounting")]
[Route("api/allocations")]
public sealed class AllocationsController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly AllocationService _allocations;

    public AllocationsController(TenantContext tenant, AllocationService allocations)
    {
        _tenant = tenant;
        _allocations = allocations;
    }

    /// <summary>
    /// Allocates money from one document to another.
    /// The user must be authorized, and the operation is scoped to their tenant.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Allocate(
        [FromBody] CreateAllocationDto dto, CancellationToken ct)
    {
        (Guid customerId, Guid orgId) = _tenant.Require();

        AllocationResult result = await _allocations.AllocateAsync(
            new AllocateTransactionRequest
            {
                CustomerId = customerId,
                OrgId = orgId,
                SourceTransactionTypeCode = dto.SourceTransactionTypeCode,
                SourceTransactionId = dto.SourceTransactionId,
                TargetTransactionTypeCode = dto.TargetTransactionTypeCode,
                TargetTransactionId = dto.TargetTransactionId,
                Amount = dto.Amount,
            },
            ct);

        return result.Outcome switch
        {
            AllocationOutcome.Ok => Ok(),

            AllocationOutcome.Retry => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new MessageResponse
                {
                    Message = result.Message ?? "The allocation raced another write. Please retry.",
                }),

            _ => StatusCode(StatusCodes.Status409Conflict, new MessageResponse
            {
                Message = result.Message ?? $"The allocation was refused.",
            }),
        };
    }

    /// <summary>
    /// Removes every allocation a source document made.
    /// </summary>
    [HttpDelete("{sourceTypeCode}/{sourceId:long}")]
    public async Task<IActionResult> Remove(
        string sourceTypeCode, long sourceId, CancellationToken ct)
    {
        // Require() ensures the tenant context is present and valid for the current user.
        _tenant.Require();

        await _allocations.RemoveAllocationsAsync(sourceTypeCode, sourceId, ct);
        return Ok();
    }
}
