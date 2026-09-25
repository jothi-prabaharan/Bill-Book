using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Api.Services.Pdf;
using Sales.Api.Services;
using Sales.Entity.Models;
using Shared.Kernel.Internal;
using Shared.Kernel.Apps;

namespace Sales.Api.Controllers;

/// <summary>
/// Sales › Delivery challans. <c>DLC</c> — the document the goods actually leave on.
///
/// <b>Stock moves here, not on the invoice.</b> The challan takes the goods out
/// and moves its order's delivered and reserved quantities; the invoice that
/// follows bills what was delivered. Skipping it is normal — an invoice raised
/// directly still issues its own stock.
///
/// <b>Posting writes nothing to the ledger from here.</b> The stock issue reaches
/// it through Inventory's costing worker, like every other issue. See
/// <see cref="DeliveryChallanService.PostAsync"/> for why the challan's own
/// GDNI post was withdrawn.
///
/// <b>Every refusal is an outcome, never an exception.</b> A challan outside
/// the caller's branch is <c>NotFound</c>, the same as an id that is nobody's:
/// row-level security hides other branches' rows from this service, so there is
/// nothing to tell the two apart, and a 403 would confirm the id exists in
/// someone else's books (decided 23 September 2026, TK-71). A rule about the
/// document comes back with the service's own sentence.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("sales")]
[Route("api/sales/delivery-challans")]
[RequireApp(App.RetailErp)]
public sealed class DeliveryChallansController : ControllerBase
{
    private readonly DeliveryChallanService _service;

    public DeliveryChallansController(DeliveryChallanService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var list = await _service.ListAsync(from, to, ct);
        return Ok(list);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var view = await _service.GetAsync(id, ct);
        return view is null ? NotFound() : Ok(view);
    }

    [HttpPost]
    public async Task<IActionResult> Save(
        [FromBody] SaveDeliveryChallanRequest request, CancellationToken ct)
    {
        DeliveryChallanResult result = await _service.SaveAsync(null, request, ct);

        return result.Outcome == DeliveryChallanOutcome.Ok
            ? CreatedAtAction(nameof(Get), new { id = result.DeliveryChallanId }, result)
            : Respond(result);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(
        long id, [FromBody] SaveDeliveryChallanRequest request, CancellationToken ct)
    {
        DeliveryChallanResult result = await _service.SaveAsync(id, request, ct);
        return result.Outcome == DeliveryChallanOutcome.Ok ? Ok(result) : Respond(result);
    }

    /// <summary>Dispatches: issues the stock and moves the order's quantities.</summary>
    [HttpPost("{id:long}/post")]
    [PermissionAction("approve")]
    public async Task<IActionResult> Post(long id, CancellationToken ct)
    {
        DeliveryChallanResult result = await _service.PostAsync(id, ct);
        return result.Outcome == DeliveryChallanOutcome.Ok ? Ok(result) : Respond(result);
    }

    /// <summary>
    /// Withdraws a draft. The reason is required by the database, not just by
    /// convention — see <see cref="VoidDeliveryChallanRequest"/>.
    /// </summary>
    [HttpPost("{id:long}/void")]
    [PermissionAction("void")]
    public async Task<IActionResult> Void(
        long id, [FromBody] VoidDeliveryChallanRequest request, CancellationToken ct)
    {
        DeliveryChallanResult result = await _service.VoidAsync(id, request.Reason, ct);
        return result.Outcome == DeliveryChallanOutcome.Ok ? Ok(result) : Respond(result);
    }

    private IActionResult Respond(DeliveryChallanResult result) =>
        result.Outcome switch
        {
            DeliveryChallanOutcome.NotFound => NotFound(),
            DeliveryChallanOutcome.LifecycleRefused or DeliveryChallanOutcome.OverDelivered
                or DeliveryChallanOutcome.StockRefused => Conflict(Message(result)),
            DeliveryChallanOutcome.RatesUnavailable => StatusCode(
                StatusCodes.Status503ServiceUnavailable, Message(result)),
            _ => UnprocessableEntity(Message(result)),
        };

    private static MessageResponse Message(DeliveryChallanResult result) =>
        new() { Message = result.Detail ?? "This delivery challan was refused." };

    /// <summary>
    /// The PDF archived when this challan was posted (TK-22). Not found for a
    /// draft, for a challan in another branch, or when the file is missing — the
    /// three are not told apart. Needs <c>sales.print</c>, as printing does: both
    /// hand the document to someone outside the business.
    /// </summary>
    [HttpGet("{id:long}/pdf")]
    [PermissionAction("print")]
    public async Task<IActionResult> DownloadPdf(
        long id, [FromServices] SalesDocumentArchive archive, CancellationToken ct)
    {
        ArchivedPdf? pdf = await archive.OpenAsync(ArchivedSalesDocument.DeliveryChallan, id, ct);
        return pdf is null ? NotFound() : File(pdf.Content, "application/pdf", pdf.FileName);
    }

    /// <summary>The challan's latest e-way bill (TK-93). Not found when it has none.</summary>
    [HttpGet("{id:long}/eway-bill")]
    [PermissionAction("view")]
    public async Task<IActionResult> EwayBill(
        long id, [FromServices] Sales.Api.Services.EInvoicing.EwayBillService ewayBills, CancellationToken ct)
    {
        EwayBillView? view = await ewayBills.GetAsync(Sales.Entity.Enums.EwayBillSource.DeliveryChallan, id, ct);
        return view is null ? NotFound() : Ok(view);
    }

    /// <summary>Generates the challan's e-way bill with its Part B (TK-93). Needs sales.einvoice.</summary>
    [HttpPost("{id:long}/eway-bill")]
    [PermissionAction("einvoice")]
    public async Task<IActionResult> GenerateEwayBill(
        long id, [FromBody] GenerateEwayBillRequest request,
        [FromServices] Sales.Api.Services.EInvoicing.EwayBillService ewayBills, CancellationToken ct) =>
        EwayBillAnswer(await ewayBills.GenerateAsync(Sales.Entity.Enums.EwayBillSource.DeliveryChallan, id, request, ct));

    /// <summary>Changes the vehicle on the challan's live e-way bill (TK-93).</summary>
    [HttpPost("{id:long}/eway-bill/part-b")]
    [PermissionAction("einvoice")]
    public async Task<IActionResult> UpdateEwayBillPartB(
        long id, [FromBody] UpdatePartBRequest request,
        [FromServices] Sales.Api.Services.EInvoicing.EwayBillService ewayBills, CancellationToken ct) =>
        EwayBillAnswer(await ewayBills.UpdatePartBAsync(Sales.Entity.Enums.EwayBillSource.DeliveryChallan, id, request, ct));

    /// <summary>Cancels the challan's live e-way bill, within 24 hours of generating it (TK-93).</summary>
    [HttpPost("{id:long}/eway-bill/cancel")]
    [PermissionAction("einvoice")]
    public async Task<IActionResult> CancelEwayBill(
        long id, [FromBody] CancelEwayBillRequest request,
        [FromServices] Sales.Api.Services.EInvoicing.EwayBillService ewayBills, CancellationToken ct) =>
        EwayBillAnswer(await ewayBills.CancelAsync(Sales.Entity.Enums.EwayBillSource.DeliveryChallan, id, request, ct));

    private IActionResult EwayBillAnswer(EwayBillResult result) => result.Outcome switch
    {
        EwayBillOutcome.Ok => Ok(result.EwayBill),
        EwayBillOutcome.NotFound => NotFound(),
        EwayBillOutcome.Invalid => UnprocessableEntity(new MessageResponse { Message = result.Detail ?? "The e-way bill details are not valid." }),
        _ => Conflict(new MessageResponse { Message = result.Detail ?? "The e-way bill was refused." }),
    };
}
