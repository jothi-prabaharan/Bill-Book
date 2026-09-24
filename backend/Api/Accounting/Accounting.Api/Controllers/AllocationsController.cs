using System.Data;
using Accounting.Api.Services;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Persistence;
using Shared.Kernel.Tenancy;
using Shared.Kernel.Apps;

namespace Accounting.Api.Controllers;

/// <summary>
/// The user-facing public door for document-to-document allocations (e.g. Credit Notes to Invoices).
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("accounting")]
[Route("api/allocations")]
[RequireApp(App.RetailErp)]
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
    // Serializable, declared where the request-wide transaction filter can see
    // it. AllocationService reads what the target still owes and writes against
    // that in one act, so it asks for Serializable — and without this the filter
    // had already opened a Read Committed transaction, which cannot be raised,
    // so BeginScopeAsync refused every allocation made through the host (TK-08).
    [Transactional(IsolationLevel.Serializable)]
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

                // Both were accepted by the DTO, stored by the service and
                // dropped here, so an allocation booked to a named date landed
                // on today's and a reason typed by the user reached nothing.
                // The columns existed throughout; only this mapping was short.
                AllocationDate = dto.AllocationDate,
                Notes = dto.Notes,
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
    [PermissionAction("void")]
    public async Task<IActionResult> Remove(
        string sourceTypeCode, long sourceId, CancellationToken ct)
    {
        // Require() ensures the tenant context is present and valid for the current user.
        _tenant.Require();

        await _allocations.RemoveAllocationsAsync(sourceTypeCode, sourceId, ct);
        return Ok();
    }

    /// <summary>
    /// A page of allocations, newest first, optionally narrowed to one contact.
    /// Voided rows are left out unless asked for — the list is what is claimed
    /// now, not everything that ever was.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] long? contactId = null,
        [FromQuery] bool includeVoided = false)
    {
        _tenant.Require();

        return Ok(await _allocations.ListAsync(page, pageSize, contactId, includeVoided, ct));
    }

    /// <summary>
    /// One allocation with the target's live balances beside it.
    ///
    /// Another branch's allocation is not found, the same as one that does not
    /// exist. Row-level security hides it from this service entirely, so there
    /// is nothing to tell the two apart with, and a 403 would confirm to the
    /// caller that the id exists in someone else's books.
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        _tenant.Require();

        AllocationDetailDto? allocation = await _allocations.GetAsync(id, ct);

        if (allocation is not null)
        {
            return Ok(allocation);
        }

        return NotFound(new MessageResponse { Message = $"Allocation {id} was not found." });
    }

    /// <summary>
    /// What a contact has open on both sides: the credits available to apply and
    /// the balances waiting to be settled. This is what the settlement workspace
    /// is built from.
    /// </summary>
    [HttpGet("open-documents/{contactId:long}")]
    public async Task<IActionResult> GetOpenDocuments(long contactId, CancellationToken ct)
    {
        _tenant.Require();

        return Ok(await _allocations.GetOpenDocumentsAsync(contactId, ct));
    }

    /// <summary>
    /// Releases one allocation, returning what it claimed to both documents.
    ///
    /// <c>accounting.void</c> rather than <c>accounting.edit</c>: withdrawing a
    /// settlement is a different authority from making one, which is the whole
    /// point of <see cref="PermissionActionAttribute"/>.
    /// </summary>
    [HttpPost("{id:long}/void")]
    [PermissionAction("void")]
    public async Task<IActionResult> Void(
        long id, [FromBody] VoidAllocationDto dto, CancellationToken ct)
    {
        _tenant.Require();

        if (await _allocations.VoidAsync(id, dto.Reason, ct))
        {
            return Ok();
        }

        // Nothing live was updated. Either it does not exist here (another
        // branch's allocation included; RLS hides it) or it was voided already.
        AllocationDetailDto? existing = await _allocations.GetAsync(id, ct);

        return existing is null
            ? NotFound(new MessageResponse { Message = $"Allocation {id} was not found." })
            : Conflict(new MessageResponse
            {
                Message = $"Allocation {id} was already voided"
                    + (existing.VoidedAt is { } at ? $" on {at:yyyy-MM-dd}." : "."),
            });
    }
}
