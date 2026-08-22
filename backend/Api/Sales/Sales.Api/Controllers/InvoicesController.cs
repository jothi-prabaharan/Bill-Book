using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Api.Services;
using Sales.Entity.Models;
using Shared.Kernel.Internal;

namespace Sales.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("sales")]
[Route("api/sales/invoices")]
public sealed class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _service;

    /// <summary>
    /// One constructor, taking the interface.
    ///
    /// There were two — one on the concrete <c>InvoiceService</c> and one on the
    /// interface — which worked only because <c>IInvoiceService</c> was never
    /// registered, so exactly one was resolvable. Registering it later, as this
    /// commit does, would have made both resolvable and
    /// <c>ActivatorUtilities</c> throws on an ambiguous constructor: every
    /// invoice request would have 500'd, at runtime, with a message about
    /// constructors rather than about invoices.
    /// </summary>
    public InvoicesController(IInvoiceService service)
    {
        _service = service;
    }

    /// <summary>What a page asks for when it does not say. Clamped again in the service.</summary>
    private const int DefaultPageSize = 50;

    /// <summary>
    /// One page of invoices, newest first.
    ///
    /// <c>skip</c> and <c>take</c> arrive off a query string and are clamped in
    /// the service rather than trusted — a negative skip is a hand-edited URL
    /// that either throws or silently serves page one while the pager claims
    /// otherwise.
    /// </summary>
    [HttpGet]
    [PermissionAction("view")]
    public async Task<IActionResult> List(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct,
        [FromQuery] int skip = 0,
        [FromQuery] int take = DefaultPageSize,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] bool overdueOnly = false)
    {
        InvoiceListPage page = await _service.ListPageAsync(
            skip, take, status, search, from, to, overdueOnly, ct);

        return Ok(page);
    }

    [HttpGet("{id:long}")]
    [PermissionAction("view")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        InvoiceView? invoice = await _service.GetAsync(id, ct);
        if (invoice is null)
        {
            if (await _service.ExistsInOtherOrgAsync(id, ct))
            {
                return Forbid();
            }
            return NotFound();
        }

        return Ok(invoice);
    }

    [HttpGet("{id:long}/gl-preview")]
    [PermissionAction("view")]
    public async Task<IActionResult> PreviewGl(long id, CancellationToken ct)
    {
        GlPreviewResult? preview = await _service.PreviewGlAsync(id, ct);
        if (preview is null)
        {
            if (await _service.ExistsInOtherOrgAsync(id, ct))
            {
                return Forbid();
            }
            return NotFound();
        }

        return Ok(preview);
    }

    [HttpPost]
    [PermissionAction("create")]
    public async Task<IActionResult> Create(
        [FromBody] SaveInvoiceRequest request, CancellationToken ct)
    {
        InvoiceResult result = await _service.CreateAsync(request, ct);

        return result.Outcome == InvoiceOutcome.Ok
            ? CreatedAtAction(nameof(Get), new { id = result.InvoiceId }, result)
            : Respond(result);
    }

    /// <summary>
    /// A confirmed sales order, turned into an invoice. The lines come from the
    /// order rather than from the caller.
    /// </summary>
    [HttpPost("from-sales-order/{salesOrderId:long}")]
    [PermissionAction("create")]
    public async Task<IActionResult> CreateFromSalesOrder(
        long salesOrderId, [FromBody] CreateInvoiceFromOrderRequest request, CancellationToken ct)
    {
        InvoiceResult result = await _service.CreateFromSalesOrderAsync(salesOrderId, request, ct);

        return result.Outcome == InvoiceOutcome.Ok
            ? CreatedAtAction(nameof(Get), new { id = result.InvoiceId }, result)
            : Respond(result);
    }

    [HttpPut("{id:long}")]
    [PermissionAction("edit")]
    public async Task<IActionResult> Update(
        long id, [FromBody] SaveInvoiceRequest request, CancellationToken ct)
    {
        InvoiceResult result = await _service.UpdateAsync(id, request, ct);
        if (result.Outcome == InvoiceOutcome.NotFound && await _service.ExistsInOtherOrgAsync(id, ct))
        {
            return Forbid();
        }

        return result.Outcome == InvoiceOutcome.Ok
            ? Ok(result)
            : Respond(result);
    }

    [HttpPost("{id:long}/post")]
    [PermissionAction("approve")]
    public async Task<IActionResult> Post(long id, CancellationToken ct)
    {
        InvoiceResult result = await _service.PostAsync(id, ct);
        if (result.Outcome == InvoiceOutcome.NotFound && await _service.ExistsInOtherOrgAsync(id, ct))
        {
            return Forbid();
        }

        return result.Outcome == InvoiceOutcome.Ok
            ? Ok(result)
            : Respond(result);
    }

    [HttpPost("{id:long}/void")]
    [PermissionAction("void")]
    public async Task<IActionResult> Void(
        long id, [FromBody] VoidInvoiceRequest request, CancellationToken ct)
    {
        InvoiceResult result = await _service.VoidAsync(id, request, ct);
        if (result.Outcome == InvoiceOutcome.NotFound && await _service.ExistsInOtherOrgAsync(id, ct))
        {
            return Forbid();
        }

        return result.Outcome == InvoiceOutcome.Ok
            ? Ok(result)
            : Respond(result);
    }

    private IActionResult Respond(InvoiceResult result) =>
        result.Outcome switch
        {
            InvoiceOutcome.NotFound => NotFound(),
            InvoiceOutcome.LifecycleRefused => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "Action refused by document lifecycle."
            }),
            InvoiceOutcome.LineInvalid => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "One or more lines are invalid."
            }),
            InvoiceOutcome.ValidityInvalid => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "Validity date is invalid."
            }),
            InvoiceOutcome.DueDateMissing => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "An invoice requires a due date."
            }),
            InvoiceOutcome.PlaceOfSupplyRefused => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "Place of supply could not be determined."
            }),
            InvoiceOutcome.RatesUnavailable => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new MessageResponse { Message = result.Detail ?? "Tax rates or base currency are temporarily unavailable." }),
            InvoiceOutcome.AlreadyFulfilled => Conflict(new MessageResponse
            {
                Message = result.Detail ?? "This order has already been fully invoiced."
            }),
            InvoiceOutcome.InsufficientStock => Conflict(new MessageResponse
            {
                Message = result.Detail ?? "Insufficient stock to issue."
            }),
            InvoiceOutcome.CreditLimitExceeded => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "Credit limit exceeded or account on hold."
            }),
            InvoiceOutcome.SourceInvalid => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "Source document is invalid or not in expected state."
            }),
            InvoiceOutcome.PostingRefused => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "Ledger posting failed."
            }),
            InvoiceOutcome.StockRefused => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "Stock issue failed."
            }),
            InvoiceOutcome.AlreadyCredited => Conflict(new MessageResponse
            {
                Message = result.Detail ?? "Downstream credit note prevents voiding."
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
}
