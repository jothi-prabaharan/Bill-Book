using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sales.Api.Services;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Internal;

namespace Sales.Api.Controllers;

/// <summary>
/// Sales orders — <c>SOR</c>. The document that commits stock without selling it.
///
/// <b>Nothing here reaches <c>acc.JournalLedger</c>.</b> An order is a promise
/// and a promise is not a supply, so the double entry belongs to the invoice
/// raised from it. What confirming one does instead is reserve: see
/// <see cref="SalesOrderService.ConfirmAsync"/>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("sales")]
[Route("api/sales/sales-orders")]
public sealed class SalesOrdersController : ControllerBase
{
    private const int DefaultPageSize = 50;

    private readonly SalesOrderService _SalesOrders;
    private readonly SalesDbContext _db;
    private readonly IInvoiceService _invoices;

    public SalesOrdersController(
        SalesOrderService SalesOrders,
        SalesDbContext db,
        IInvoiceService invoices)
    {
        _SalesOrders = SalesOrders;
        _db = db;
        _invoices = invoices;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        CancellationToken ct,
        [FromQuery] int skip = 0,
        [FromQuery] int take = DefaultPageSize,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null)
    {
        SalesOrderListPage page = await _SalesOrders.ListAsync(skip, take, status, search, ct);
        return Ok(page);
    }

    [HttpGet("{SalesOrderId:long}")]
    public async Task<IActionResult> Get(long SalesOrderId, CancellationToken ct)
    {
        SalesOrderViewResult result = await _SalesOrders.GetAsync(SalesOrderId, ct);

        return result.Outcome == SalesOrderOutcome.Ok
            ? Ok(result.View)
            : Respond(result.Outcome, null);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveSalesOrderRequest request, CancellationToken ct)
    {
        SalesOrderResult result = await _SalesOrders.CreateAsync(request, ct);

        return result.Outcome == SalesOrderOutcome.Ok
            ? CreatedAtAction(nameof(Get), new { SalesOrderId = result.SalesOrderId }, result)
            : Respond(result.Outcome, result.Detail);
    }

    [HttpPost("from-quote/{QuoteId:long}")]
    public async Task<IActionResult> CreateFromQuote(
        long QuoteId, [FromBody] CreateOrderFromQuoteRequest request, CancellationToken ct)
    {
        SalesOrderResult result = await _SalesOrders.CreateFromQuoteAsync(QuoteId, request, ct);

        return result.Outcome == SalesOrderOutcome.Ok
            ? CreatedAtAction(nameof(Get), new { SalesOrderId = result.SalesOrderId }, result)
            : Respond(result.Outcome, result.Detail);
    }

    [HttpPost("availability")]
    [PermissionAction("view")]
    public async Task<IActionResult> Availability(
        [FromBody] SalesOrderAvailabilityRequest request, CancellationToken ct)
    {
        List<SalesOrderAvailabilityLine> lines =
            await _SalesOrders.GetAvailabilityAsync(request.ItemIds, ct);

        return Ok(lines);
    }

    /// <summary>
    /// Fulfill some or all remaining order quantities by creating and posting an invoice.
    ///
    /// The server derives the remaining quantity from the order and all existing
    /// non-void invoices. The caller cannot over-invoice a line by sending a stale
    /// client-side quantity. The invoice is then posted through the existing
    /// accounting/inventory pipeline, so tax, stock issue and ledger posting remain
    /// owned by InvoiceService.
    ///
    /// Empty <c>Lines</c> means "fulfill everything still uninvoiced".
    /// </summary>
    [HttpPost("{SalesOrderId:long}/fulfill")]
    [PermissionAction("approve")]
    public async Task<IActionResult> Fulfill(
        long SalesOrderId,
        [FromBody] FulfillSalesOrderRequest request,
        CancellationToken ct)
    {
        SalesOrderViewResult access = await _SalesOrders.GetAsync(SalesOrderId, ct);
        if (access.Outcome == SalesOrderOutcome.NotFound)
        {
            return NotFound();
        }
        if (access.Outcome == SalesOrderOutcome.Forbidden)
        {
            return Forbid();
        }
        if (access.Outcome != SalesOrderOutcome.Ok || access.View is null)
        {
            return BadRequest(new MessageResponse
            {
                Message = "The Sales Order could not be read for fulfillment."
            });
        }

        SalesOrder? order = await _db.SalesOrders
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.SalesOrderId == SalesOrderId, ct);

        if (order is null)
        {
            return NotFound();
        }

        if (order.Status != DocumentStatus.Posted)
        {
            return BadRequest(new MessageResponse
            {
                Message = "Only a confirmed Sales Order can be fulfilled."
            });
        }

        if (order.FulfilmentStatus is FulfilmentStatus.Closed or FulfilmentStatus.Cancelled)
        {
            return Conflict(new MessageResponse
            {
                Message = "This Sales Order is already closed."
            });
        }

        if (request.DueDate is null)
        {
            return BadRequest(new MessageResponse
            {
                Message = "An invoice requires a due date. Set the due date or payment term before fulfillment."
            });
        }

        // Serializable prevents two concurrent fulfillment requests from both
        // reading the same remaining quantity and issuing duplicate invoices.
        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);

        try
        {
            var orderLineIds = order.Lines.Select(x => x.SalesOrderDetailId).ToList();

            var invoiced = await (
                from detail in _db.InvoiceDetails
                join invoice in _db.Invoices on detail.InvoiceId equals invoice.InvoiceId
                where detail.SalesOrderDetailId.HasValue
                    && orderLineIds.Contains(detail.SalesOrderDetailId.Value)
                    && invoice.SalesOrderId == SalesOrderId
                    && invoice.Status != DocumentStatus.Void
                group detail by detail.SalesOrderDetailId!.Value into grouped
                select new
                {
                    SalesOrderDetailId = grouped.Key,
                    Quantity = grouped.Sum(x => x.Quantity)
                })
                .ToDictionaryAsync(x => x.SalesOrderDetailId, x => x.Quantity, ct);

            var requestedByLine = request.Lines
                .GroupBy(x => x.SalesOrderDetailId)
                .ToDictionary(x => x.Key, x => x.Sum(v => v.Quantity));

            var linesToFulfill = new List<(SalesOrderDetail Line, decimal Quantity, decimal PreviouslyInvoiced)>();

            foreach (SalesOrderDetail line in order.Lines)
            {
                decimal previouslyInvoiced = invoiced.GetValueOrDefault(line.SalesOrderDetailId);
                decimal remaining = Math.Max(0m, line.Quantity - previouslyInvoiced);

                if (requestedByLine.Count == 0)
                {
                    if (remaining > 0m)
                    {
                        linesToFulfill.Add((line, remaining, previouslyInvoiced));
                    }
                    continue;
                }

                if (!requestedByLine.TryGetValue(line.SalesOrderDetailId, out decimal requested))
                {
                    continue;
                }

                if (requested <= 0m)
                {
                    return BadRequest(new MessageResponse
                    {
                        Message = $"Line {line.LineNumber} quantity must be greater than zero."
                    });
                }

                if (requested > remaining)
                {
                    return Conflict(new MessageResponse
                    {
                        Message = $"Line {line.LineNumber} can only fulfill {remaining} remaining units; {requested} were requested."
                    });
                }

                linesToFulfill.Add((line, requested, previouslyInvoiced));
            }

            foreach (long requestedLineId in requestedByLine.Keys)
            {
                if (order.Lines.All(x => x.SalesOrderDetailId != requestedLineId))
                {
                    return BadRequest(new MessageResponse
                    {
                        Message = $"Sales Order line {requestedLineId} does not belong to this order."
                    });
                }
            }

            if (linesToFulfill.Count == 0)
            {
                return Conflict(new MessageResponse
                {
                    Message = "There is no remaining quantity to fulfill on this Sales Order."
                });
            }

            SaveInvoiceRequest invoiceRequest = new()
            {
                DocumentDate = request.DocumentDate
                    ?? DateOnly.FromDateTime(DateTime.UtcNow),
                DueDate = request.DueDate,
                PaymentTermId = request.PaymentTermId,
                ContactId = order.ContactId,
                QuoteId = order.QuoteId,
                SalesOrderId = order.SalesOrderId,
                ContactGstin = order.ContactGstin,
                PlaceOfSupplyStateCode = request.PlaceOfSupplyStateCode,
                BillingAddress = order.BillingAddress,
                ShippingAddress = order.ShippingAddress,
                CurrencyCode = order.CurrencyCode,
                ExchangeRate = order.ExchangeRate,
                Notes = request.Notes ?? order.Notes,
                TermsAndConditions = order.TermsAndConditions,
                Lines = linesToFulfill.Select(x => new SaveInvoiceLineRequest
                {
                    ItemId = x.Line.ItemId,
                    Description = x.Line.Description,
                    HsnSacCode = x.Line.HsnSacCode,
                    WarehouseId = x.Line.WarehouseId,
                    Quantity = x.Quantity,
                    UomId = x.Line.UomId,
                    ConversionFactor = x.Line.ConversionFactor,
                    UnitPrice = x.Line.UnitPrice,
                    IsPriceInclusive = x.Line.IsPriceInclusive,
                    DiscountPercent = x.Line.DiscountPercent,
                    DiscountAmount = x.Line.DiscountAmount,
                    TaxTreatment = x.Line.TaxTreatment,
                    TaxGroupId = x.Line.TaxGroupId,
                    LineType = x.Line.LineType,
                    AccountId = x.Line.AccountId,
                    FixedAssetCategoryId = x.Line.FixedAssetCategoryId,
                    ItemBatchId = x.Line.ItemBatchId,
                    LineNotes = x.Line.LineNotes,
                    SalesOrderDetailId = x.Line.SalesOrderDetailId,
                }).ToList()
            };

            InvoiceResult created = await _invoices.CreateAsync(invoiceRequest, ct);
            if (created.Outcome != InvoiceOutcome.Ok)
            {
                await transaction.RollbackAsync(ct);
                return InvoiceFailure(created);
            }

            InvoiceResult posted = await _invoices.PostAsync(created.InvoiceId, ct);
            if (posted.Outcome != InvoiceOutcome.Ok)
            {
                await transaction.RollbackAsync(ct);
                return InvoiceFailure(posted);
            }

            // Invoice posting has issued the goods. Keep the order's fulfilment
            // quantities aligned with DeliveryChallanService: a sales-order line
            // may be fulfilled by either a challan or an invoice, and the two
            // quantities are additive across documents.
            foreach ((SalesOrderDetail line, decimal quantity, _) in linesToFulfill)
            {
                line.DeliveredQuantity += quantity;
                if (line.LineType == DocumentLineType.Stock)
                {
                    line.ReservedQuantity = Math.Max(0m, line.ReservedQuantity - quantity);
                }
            }

            bool allDelivered = order.Lines.All(x => x.DeliveredQuantity >= x.Quantity);
            bool someDelivered = order.Lines.Any(x => x.DeliveredQuantity > 0m);
            order.FulfilmentStatus = allDelivered
                ? FulfilmentStatus.Closed
                : (someDelivered ? FulfilmentStatus.PartlyDelivered : FulfilmentStatus.Open);

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return Ok(new FulfillSalesOrderResult
            {
                SalesOrderId = order.SalesOrderId,
                InvoiceId = created.InvoiceId,
                Status = order.FulfilmentStatus.ToString(),
                Lines = linesToFulfill.Select(x =>
                {
                    decimal remaining = Math.Max(0m, x.Line.Quantity - x.PreviouslyInvoiced - x.Quantity);
                    return new FulfilledSalesOrderLine
                    {
                        SalesOrderDetailId = x.Line.SalesOrderDetailId,
                        OrderedQuantity = x.Line.Quantity,
                        PreviouslyInvoicedQuantity = x.PreviouslyInvoiced,
                        FulfilledQuantity = x.Quantity,
                        RemainingQuantity = remaining
                    };
                }).ToList()
            });
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    [HttpPut("{SalesOrderId:long}")]
    public async Task<IActionResult> Update(long SalesOrderId, [FromBody] SaveSalesOrderRequest request, CancellationToken ct)
    {
        SalesOrderResult result = await _SalesOrders.UpdateAsync(SalesOrderId, request, ct);

        return result.Outcome == SalesOrderOutcome.Ok
            ? NoContent()
            : Respond(result.Outcome, result.Detail);
    }

    [HttpPost("{SalesOrderId:long}/confirm")]
    [PermissionAction("approve")]
    public async Task<IActionResult> Confirm(long SalesOrderId, CancellationToken ct)
    {
        SalesOrderResult result = await _SalesOrders.ConfirmAsync(SalesOrderId, ct);

        return result.Outcome == SalesOrderOutcome.Ok
            ? NoContent()
            : Respond(result.Outcome, result.Detail);
    }

    [HttpPost("{SalesOrderId:long}/short-close")]
    [PermissionAction("approve")]
    public async Task<IActionResult> ShortClose(
        long SalesOrderId, [FromBody] ShortCloseSalesOrderRequest request, CancellationToken ct)
    {
        SalesOrderResult result = await _SalesOrders.ShortCloseAsync(SalesOrderId, request, ct);

        return result.Outcome == SalesOrderOutcome.Ok
            ? NoContent()
            : Respond(result.Outcome, result.Detail);
    }

    [HttpPost("{SalesOrderId:long}/void")]
    [PermissionAction("void")]
    public async Task<IActionResult> Void(long SalesOrderId, [FromBody] VoidSalesOrderRequest request, CancellationToken ct)
    {
        SalesOrderResult result = await _SalesOrders.VoidAsync(SalesOrderId, request, ct);

        return result.Outcome == SalesOrderOutcome.Ok
            ? NoContent()
            : Respond(result.Outcome, result.Detail);
    }

    private IActionResult InvoiceFailure(InvoiceResult result) =>
        result.Outcome switch
        {
            InvoiceOutcome.NotFound => NotFound(),
            InvoiceOutcome.InsufficientStock => Conflict(new MessageResponse { Message = result.Detail ?? "Insufficient stock to fulfill the Sales Order." }),
            InvoiceOutcome.CreditLimitExceeded => BadRequest(new MessageResponse { Message = result.Detail ?? "Credit limit exceeded or account on hold." }),
            InvoiceOutcome.DueDateMissing => BadRequest(new MessageResponse { Message = result.Detail ?? "An invoice requires a due date." }),
            InvoiceOutcome.PlaceOfSupplyRefused => BadRequest(new MessageResponse { Message = result.Detail ?? "Place of supply could not be determined." }),
            InvoiceOutcome.RatesUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new MessageResponse { Message = result.Detail ?? "Tax rates or base currency are temporarily unavailable." }),
            InvoiceOutcome.SourceInvalid => BadRequest(new MessageResponse { Message = result.Detail ?? "Source document is invalid." }),
            InvoiceOutcome.LifecycleRefused => BadRequest(new MessageResponse { Message = result.Detail ?? "Invoice lifecycle refused the operation." }),
            InvoiceOutcome.PostingRefused or InvoiceOutcome.StockRefused => BadRequest(new MessageResponse { Message = result.Detail ?? "Invoice posting failed." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new MessageResponse { Message = result.Detail ?? "Invoice fulfillment failed." })
        };

    private IActionResult Respond(SalesOrderOutcome outcome, string? detail) =>
        outcome switch
        {
            SalesOrderOutcome.NotFound => NotFound(),
            SalesOrderOutcome.Forbidden => Forbid(),
            SalesOrderOutcome.LifecycleRefused => BadRequest(new MessageResponse { Message = detail ?? "Action refused by document lifecycle." }),
            SalesOrderOutcome.LineInvalid => BadRequest(new MessageResponse { Message = detail ?? "One or more lines are invalid." }),
            SalesOrderOutcome.ValidityInvalid => BadRequest(new MessageResponse { Message = detail ?? "Validity date is invalid." }),
            SalesOrderOutcome.PlaceOfSupplyRefused => BadRequest(new MessageResponse { Message = detail ?? "Place of supply could not be determined." }),
            SalesOrderOutcome.RatesUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new MessageResponse { Message = detail ?? "Tax rates or base currency are temporarily unavailable." }),
            SalesOrderOutcome.AlreadyFulfilled => Conflict(new MessageResponse { Message = "This Sales Order has already been fulfilled." }),
            SalesOrderOutcome.InsufficientStock => Conflict(new MessageResponse { Message = detail ?? "Insufficient stock to reserve." }),
            SalesOrderOutcome.CreditLimitExceeded => BadRequest(new MessageResponse { Message = detail ?? "Credit limit exceeded or account on hold." }),
            SalesOrderOutcome.QuoteNotConvertible => Conflict(new MessageResponse { Message = detail ?? "This quote cannot be converted to a sales order." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
}
