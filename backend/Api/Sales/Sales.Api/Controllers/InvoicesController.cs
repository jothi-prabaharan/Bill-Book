using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Api.Services.Pdf;
using Sales.Api.Services;
using Sales.Api.Services.Printing;
using Sales.Entity.Models;
using Shared.Kernel.Internal;
using Shared.Kernel.Apps;

namespace Sales.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("sales")]
[Route("api/sales/invoices")]
[RequireApp(App.RetailErp)]
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
        [FromQuery] bool overdueOnly = false,
        [FromQuery] bool eInvoiceAttention = false)
    {
        InvoiceListPage page = await _service.ListPageAsync(
            skip, take, status, search, from, to, overdueOnly, eInvoiceAttention, ct);

        return Ok(page);
    }

    [HttpGet("{id:long}")]
    [PermissionAction("view")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        InvoiceView? invoice = await _service.GetAsync(id, ct);
        // Another branch's invoice is 404, not 403: RLS hides it from this
        // service too, so "not yours" and "no such invoice" are one answer
        // (decided 23 September 2026, TK-71; applied here by TK-03).
        return invoice is null ? NotFound() : Ok(invoice);
    }

    /// <summary>
    /// The invoice laid out by its print template, as HTML for the browser to
    /// print. Sales builds the invoice's data and Printing lays it out under the
    /// caller's own token, so the branch's template is the one used.
    ///
    /// <c>sales.print</c> rather than <c>sales.view</c>: printing hands a
    /// document to somebody outside the business, which is the separation the
    /// print permission was seeded for.
    /// </summary>
    [HttpGet("{id:long}/print")]
    [PermissionAction("print")]
    public async Task<IActionResult> Print(
        long id, [FromServices] InvoicePrintService printing, CancellationToken ct)
    {
        PrintedDocument? printed = await printing.PrintAsync(id, ct);
        return printed is null ? NotFound() : Ok(printed);
    }

    /// <summary>
    /// The PDF archived when this invoice was posted (TK-22). Not found for a
    /// draft, for a invoice in another branch, or when the file is missing — the
    /// three are not told apart. Needs <c>sales.print</c>, as printing does: both
    /// hand the document to someone outside the business.
    /// </summary>
    [HttpGet("{id:long}/pdf")]
    [PermissionAction("print")]
    public async Task<IActionResult> DownloadPdf(
        long id, [FromServices] SalesDocumentArchive archive, CancellationToken ct)
    {
        ArchivedPdf? pdf = await archive.OpenAsync(ArchivedSalesDocument.Invoice, id, ct);
        return pdf is null ? NotFound() : File(pdf.Content, "application/pdf", pdf.FileName);
    }

    [HttpGet("{id:long}/gl-preview")]
    [PermissionAction("view")]
    public async Task<IActionResult> PreviewGl(long id, CancellationToken ct)
    {
        GlPreviewResult? preview = await _service.PreviewGlAsync(id, ct);
        return preview is null ? NotFound() : Ok(preview);
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
        return result.Outcome == InvoiceOutcome.Ok
            ? Ok(result)
            : Respond(result);
    }

    [HttpPost("{id:long}/post")]
    [PermissionAction("approve")]
    public async Task<IActionResult> Post(long id, CancellationToken ct)
    {
        InvoiceResult result = await _service.PostAsync(id, ct);
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
        return result.Outcome == InvoiceOutcome.Ok
            ? Ok(result)
            : Respond(result);
    }

    /// <summary>The invoice's registration at the IRP (TK-92). Not found when it has none.</summary>
    [HttpGet("{id:long}/e-invoice")]
    [PermissionAction("view")]
    public async Task<IActionResult> EInvoice(
        long id, [FromServices] Sales.Api.Services.EInvoicing.EInvoiceActions actions, CancellationToken ct)
    {
        EInvoiceStateView? state = await actions.GetAsync(Sales.Entity.Enums.EInvoiceSource.Invoice, id, ct);
        return state is null ? NotFound() : Ok(state);
    }

    /// <summary>
    /// Registers the invoice at the IRP again now (TK-92): after a refusal was
    /// fixed, or instead of waiting for the retry worker. Needs sales.einvoice.
    /// </summary>
    [HttpPost("{id:long}/e-invoice/retry")]
    [PermissionAction("einvoice")]
    public async Task<IActionResult> RetryEInvoice(
        long id, [FromServices] Sales.Api.Services.EInvoicing.EInvoiceActions actions, CancellationToken ct)
    {
        EInvoiceStateView? state = await actions.RetryAsync(Sales.Entity.Enums.EInvoiceSource.Invoice, id, ct);
        return state is null ? NotFound() : Ok(state);
    }

    /// <summary>The invoice's latest e-way bill (TK-93). Not found when it has none.</summary>
    [HttpGet("{id:long}/eway-bill")]
    [PermissionAction("view")]
    public async Task<IActionResult> EwayBill(
        long id, [FromServices] Sales.Api.Services.EInvoicing.EwayBillService ewayBills, CancellationToken ct)
    {
        EwayBillView? view = await ewayBills.GetAsync(Sales.Entity.Enums.EwayBillSource.Invoice, id, ct);
        return view is null ? NotFound() : Ok(view);
    }

    /// <summary>Generates the invoice's e-way bill with its Part B (TK-93). Needs sales.einvoice.</summary>
    [HttpPost("{id:long}/eway-bill")]
    [PermissionAction("einvoice")]
    public async Task<IActionResult> GenerateEwayBill(
        long id, [FromBody] GenerateEwayBillRequest request,
        [FromServices] Sales.Api.Services.EInvoicing.EwayBillService ewayBills, CancellationToken ct) =>
        EwayBillAnswer(await ewayBills.GenerateAsync(Sales.Entity.Enums.EwayBillSource.Invoice, id, request, ct));

    /// <summary>Changes the vehicle on the invoice's live e-way bill (TK-93).</summary>
    [HttpPost("{id:long}/eway-bill/part-b")]
    [PermissionAction("einvoice")]
    public async Task<IActionResult> UpdateEwayBillPartB(
        long id, [FromBody] UpdatePartBRequest request,
        [FromServices] Sales.Api.Services.EInvoicing.EwayBillService ewayBills, CancellationToken ct) =>
        EwayBillAnswer(await ewayBills.UpdatePartBAsync(Sales.Entity.Enums.EwayBillSource.Invoice, id, request, ct));

    /// <summary>Cancels the invoice's live e-way bill, within 24 hours of generating it (TK-93).</summary>
    [HttpPost("{id:long}/eway-bill/cancel")]
    [PermissionAction("einvoice")]
    public async Task<IActionResult> CancelEwayBill(
        long id, [FromBody] CancelEwayBillRequest request,
        [FromServices] Sales.Api.Services.EInvoicing.EwayBillService ewayBills, CancellationToken ct) =>
        EwayBillAnswer(await ewayBills.CancelAsync(Sales.Entity.Enums.EwayBillSource.Invoice, id, request, ct));

    private IActionResult EwayBillAnswer(EwayBillResult result) => result.Outcome switch
    {
        EwayBillOutcome.Ok => Ok(result.EwayBill),
        EwayBillOutcome.NotFound => NotFound(),
        EwayBillOutcome.Invalid => UnprocessableEntity(new MessageResponse { Message = result.Detail ?? "The e-way bill details are not valid." }),
        _ => Conflict(new MessageResponse { Message = result.Detail ?? "The e-way bill was refused." }),
    };

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
            // A limit refusal says approval can be asked for; the screen offers it (TK-102).
            InvoiceOutcome.CreditLimitExceeded or InvoiceOutcome.DiscountLimitExceeded => BadRequest(new LimitRefusalResponse
            {
                Message = result.Detail ?? "Credit limit exceeded or account on hold."
            }),
            InvoiceOutcome.AwaitingApproval or InvoiceOutcome.OverrideRefused => Conflict(new MessageResponse
            {
                Message = result.Detail ?? "This invoice waits on an approval."
            }),
            InvoiceOutcome.LimitsUnavailable => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new MessageResponse { Message = result.Detail ?? "Please try again in a moment." }),
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
            InvoiceOutcome.EInvoiceRefused => Conflict(new MessageResponse
            {
                Message = result.Detail ?? "The invoice's IRN stops it being voided."
            }),
            InvoiceOutcome.AlreadyCredited => Conflict(new MessageResponse
            {
                Message = result.Detail ?? "Downstream credit note prevents voiding."
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
}
