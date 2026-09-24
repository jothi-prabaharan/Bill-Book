using Shared.Kernel.Storage;
using Microsoft.EntityFrameworkCore;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tax;
using Shared.Kernel.Tenancy;
using Shared.Kernel.Interfaces;
using Sales.Entity.Enums;

namespace Sales.Api.Services;

public sealed class DeliveryChallanService
{
    private readonly SalesDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly INumberGenerator _numbering;
    private readonly IBaseCurrencyProvider _baseCurrency;
    private readonly IBranchSettingsProvider _branchSettings;
    private readonly ITaxRateProvider _rates;
    private readonly IContactNameLookup _contactNames;
    private readonly IItemNameLookup _itemNames;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    private readonly IInventoryClient _inventoryClient;
    private readonly Sales.Api.Services.Pdf.SalesDocumentArchive _archive;

    public DeliveryChallanService(
        SalesDbContext db,
        ITenantContext tenant,
        INumberGenerator numbering,
        IBaseCurrencyProvider baseCurrency,
        IBranchSettingsProvider branchSettings,
        ITaxRateProvider rates,
        IContactNameLookup contactNames,
        IItemNameLookup itemNames,
        ICurrentUser user,
        TimeProvider clock,
        IInventoryClient inventoryClient,
        Sales.Api.Services.Pdf.SalesDocumentArchive archive)
    {
        _db = db;
        _tenant = tenant;
        _numbering = numbering;
        _baseCurrency = baseCurrency;
        _branchSettings = branchSettings;
        _rates = rates;
        _contactNames = contactNames;
        _itemNames = itemNames;
        _user = user;
        _clock = clock;
        _inventoryClient = inventoryClient;
        _archive = archive;
    }

    public async Task<IReadOnlyList<DeliveryChallanListItem>> ListAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var query = _db.DeliveryChallans.AsNoTracking();

        if (from.HasValue) query = query.Where(x => x.DocumentDate >= from.Value);
        if (to.HasValue) query = query.Where(x => x.DocumentDate <= to.Value);

        var list = await query
            .OrderByDescending(x => x.DocumentDate)
            .ThenByDescending(x => x.DeliveryChallanId)
            .Select(x => new
            {
                x.DeliveryChallanId,
                x.SalesOrderId,
                x.DocumentDate,
                x.DocumentNo,
                x.ContactId,
                x.Status,
                x.DispatchDate,
                x.TotalAmount
            })
            .ToListAsync(ct);

        var contactIds = list.Select(x => x.ContactId).Distinct().ToList();
        var contacts = await _contactNames.ResolveAsync(contactIds, ct);

        return list.Select(x => new DeliveryChallanListItem
        {
            DeliveryChallanId = x.DeliveryChallanId,
            SalesOrderId = x.SalesOrderId,
            DocumentDate = x.DocumentDate,
            DocumentNo = x.DocumentNo,
            ContactId = x.ContactId,
            ContactName = contacts.TryGetValue(x.ContactId, out var c) ? c.Name : "Unknown",
            Status = x.Status.ToString(),
            DispatchDate = x.DispatchDate,
            TotalAmount = x.TotalAmount
        }).ToList();
    }

    public async Task<DeliveryChallanView?> GetAsync(long id, CancellationToken ct)
    {
        var deliveryChallan = await _db.DeliveryChallans
            .Include(x => x.Lines.OrderBy(l => l.LineNumber))
                .ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(x => x.DeliveryChallanId == id, ct);

        if (deliveryChallan == null) return null;

        var contactName = (await _contactNames.ResolveAsync([deliveryChallan.ContactId], ct))
            .TryGetValue(deliveryChallan.ContactId, out var contact) ? contact.Name : "Unknown";

        var itemIds = deliveryChallan.Lines.Select(l => l.ItemId).Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
        var itemNames = await _itemNames.ResolveAsync(itemIds, ct);

        return new DeliveryChallanView
        {
            DeliveryChallanId = deliveryChallan.DeliveryChallanId,
            SalesOrderId = deliveryChallan.SalesOrderId,
            DocumentDate = deliveryChallan.DocumentDate,
            DocumentNo = deliveryChallan.DocumentNo,
            ContactId = deliveryChallan.ContactId,
            ContactName = contactName,
            Status = deliveryChallan.Status.ToString(),
            ChallanType = deliveryChallan.ChallanType,
            ContactGstin = deliveryChallan.ContactGstin,
            VoidReason = deliveryChallan.VoidReason,
            VehicleNo = deliveryChallan.VehicleNo,
            TransporterName = deliveryChallan.TransporterName,
            EwayBillNo = deliveryChallan.EwayBillNo,
            EwayBillDate = deliveryChallan.EwayBillDate,
            DispatchDate = deliveryChallan.DispatchDate,
            CurrencyCode = deliveryChallan.CurrencyCode,
            ExchangeRate = deliveryChallan.ExchangeRate,
            Notes = deliveryChallan.Notes,
            BillingAddress = deliveryChallan.BillingAddress,
            ShippingAddress = deliveryChallan.ShippingAddress,
            PlaceOfSupplyStateId = deliveryChallan.PlaceOfSupplyStateId,
            IsInterState = deliveryChallan.IsInterState,
            SubTotal = deliveryChallan.SubTotal,
            DiscountAmount = deliveryChallan.DiscountAmount,
            TaxableAmount = deliveryChallan.TaxableAmount,
            CgstAmount = deliveryChallan.CgstAmount,
            SgstAmount = deliveryChallan.SgstAmount,
            IgstAmount = deliveryChallan.IgstAmount,
            CessAmount = deliveryChallan.CessAmount,
            RoundOffAmount = deliveryChallan.RoundOffAmount,
            TotalAmount = deliveryChallan.TotalAmount,
            TotalAmountBase = deliveryChallan.TotalAmountBase,
            Lines = deliveryChallan.Lines.Select(l => new DeliveryChallanLineView
            {
                DeliveryChallanDetailId = l.DeliveryChallanDetailId,
                SalesOrderDetailId = l.SalesOrderDetailId,
                ItemId = l.ItemId,
                ItemLabel = l.ItemId.HasValue && itemNames.TryGetValue(l.ItemId.Value, out var itemName) ? itemName.Name : null,
                HsnSacCode = l.HsnSacCode,
                Description = l.Description,
                TaxGroupId = l.TaxGroupId,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                DiscountPercent = l.DiscountPercent ?? 0m,
                DiscountAmount = l.DiscountAmount,
                LineTotal = l.LineTotal,
                TaxAmount = l.TaxAmount,
                Taxes = l.Taxes.Select(t => new DeliveryChallanLineTaxView
                {
                    TaxComponent = t.TaxComponent,
                    SubAccountId = t.SubAccountId,
                    Amount = t.Amount
                }).ToList()
            }).ToList()
        };
    }

    public async Task<DeliveryChallanResult> SaveAsync(
        long? deliveryChallanId, SaveDeliveryChallanRequest request, CancellationToken ct)
    {
        var (_, orgId) = _tenant.Require();

        DeliveryChallan deliveryChallan;
        if (deliveryChallanId.HasValue)
        {
            DeliveryChallan? existing = await _db.DeliveryChallans
                .Include(x => x.Lines)
                    .ThenInclude(l => l.Taxes)
                .FirstOrDefaultAsync(x => x.DeliveryChallanId == deliveryChallanId.Value, ct);

            if (existing is null)
            {
                return new DeliveryChallanResult(DeliveryChallanOutcome.NotFound);
            }

            DocumentTransition edit = DocumentLifecycle.CanEdit(existing.Status);
            if (!edit.IsAllowed)
            {
                return new DeliveryChallanResult(
                    DeliveryChallanOutcome.LifecycleRefused, existing.DeliveryChallanId, edit.Detail);
            }

            deliveryChallan = existing;
        }
        else
        {
            deliveryChallan = new DeliveryChallan();
        }

        // Everything that can refuse is checked before anything is changed, so a
        // refused edit leaves the draft exactly as it was rather than half-rewritten
        // in the change tracker.
        DeliveryChallanResult? orderRefusal = await CheckOrderLinkAsync(request, ct);
        if (orderRefusal is not null)
        {
            return orderRefusal with { DeliveryChallanId = deliveryChallanId ?? 0 };
        }

        BranchSettings? settings = await _branchSettings.GetSettingsAsync(ct);
        if (settings is null)
        {
            return new DeliveryChallanResult(
                DeliveryChallanOutcome.RatesUnavailable,
                Detail: "The branch's settings could not be read. Try again in a moment.");
        }

        // Resolved once, the same way Invoice and SalesOrder resolve it — a
        // GSTIN that disagrees with the stated place of supply is refused
        // rather than guessed at, because a wrong pick still balances, still
        // prints and still posts, and the mismatch only surfaces on a return.
        PlaceOfSupplyResult pos = PlaceOfSupply.Resolve(
            settings.StateCode, request.PlaceOfSupplyStateCode, request.ContactGstin);
        if (!pos.IsOk)
        {
            return new DeliveryChallanResult(
                DeliveryChallanOutcome.PlaceOfSupplyRefused, Detail: pos.Detail);
        }

        var rates = new List<TaxRate?>(request.Lines.Count);
        foreach (SaveDeliveryChallanLineRequest reqLine in request.Lines)
        {
            long? taxGroupId = TaxGroupOf(reqLine);
            TaxRate? rate = null;

            if (taxGroupId.HasValue)
            {
                rate = await _rates.GetRateAsync(taxGroupId.Value, request.DocumentDate, ct);
                if (rate is null)
                {
                    return new DeliveryChallanResult(
                        DeliveryChallanOutcome.RatesUnavailable,
                        Detail: "A tax rate on this challan could not be read for its date. "
                            + "Try again in a moment.");
                }
            }

            rates.Add(rate);
        }

        string? baseCurrency = await _baseCurrency.GetBaseCurrencyAsync(ct);
        if (baseCurrency is null)
        {
            return new DeliveryChallanResult(
                DeliveryChallanOutcome.RatesUnavailable,
                Detail: "The branch's base currency could not be read. Try again in a moment.");
        }

        if (deliveryChallanId.HasValue)
        {
            _db.DeliveryChallanDetailTaxes.RemoveRange(deliveryChallan.Lines.SelectMany(l => l.Taxes));
            _db.DeliveryChallanDetails.RemoveRange(deliveryChallan.Lines);
            deliveryChallan.Lines.Clear();
        }
        else
        {
            var alloc = await _numbering.NextAsync("DLC", request.DocumentDate, ct);
            deliveryChallan.OrgId = orgId;
            deliveryChallan.DocumentNo = alloc.Code;
            deliveryChallan.TransactionTypeCode = "DLC";
            deliveryChallan.Status = DocumentStatus.Draft;
            _db.DeliveryChallans.Add(deliveryChallan);
        }

        TaxContext taxContext = new(pos.IsInterState, settings.DiscountBeforeTax);

        deliveryChallan.ChallanType = request.ChallanType;
        deliveryChallan.SalesOrderId = request.SalesOrderId;
        deliveryChallan.VehicleNo = request.VehicleNo;
        deliveryChallan.TransporterName = request.TransporterName;
        deliveryChallan.EwayBillNo = request.EwayBillNo;
        deliveryChallan.EwayBillDate = request.EwayBillDate;
        deliveryChallan.DispatchDate = request.DispatchDate;

        deliveryChallan.ContactId = request.ContactId;
        deliveryChallan.ContactGstin = request.ContactGstin;
        deliveryChallan.PlaceOfSupplyStateId = 0;
        deliveryChallan.IsInterState = pos.IsInterState;
        deliveryChallan.DocumentDate = request.DocumentDate;
        deliveryChallan.PrintTemplateId = request.PrintTemplateId;
        deliveryChallan.Notes = request.Notes;

        // The branch's own currency when none is given. This defaulted to "USD",
        // so a challan keyed without a currency was a foreign-currency document.
        deliveryChallan.CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode)
            ? baseCurrency
            : request.CurrencyCode;
        deliveryChallan.ExchangeRate = request.ExchangeRate;
        deliveryChallan.BillingAddress = request.BillingAddress;
        deliveryChallan.ShippingAddress = request.ShippingAddress;

        var taxLines = new List<TaxLineResult>(request.Lines.Count);

        for (int i = 0; i < request.Lines.Count; i++)
        {
            SaveDeliveryChallanLineRequest reqLine = request.Lines[i];
            TaxRate? rate = rates[i];
            long? taxGroupId = TaxGroupOf(reqLine);

            TaxLineInput taxInput = new()
            {
                Quantity = reqLine.Quantity,
                UnitPrice = reqLine.UnitPrice,
                DiscountPercent = reqLine.DiscountPercent > 0 ? reqLine.DiscountPercent : null,
                Rate = rate,
            };

            // BaseQuantity, GrossAmount, TaxableAmount and LineTotal all come
            // from here — computing them by hand next to this call is exactly
            // how they drifted out of step with "chk_deliverychallandetails_*"
            // before. See Shared.Kernel.Tax.GstCalculator.
            TaxLineResult computed = GstCalculator.Compute(taxInput, taxContext);
            taxLines.Add(computed);

            var line = new DeliveryChallanDetail
            {
                OrgId = deliveryChallan.OrgId,
                LineNumber = i + 1,
                SalesOrderDetailId = reqLine.SalesOrderDetailId,
                ItemId = reqLine.ItemId,
                Quantity = reqLine.Quantity,
                ConversionFactor = 1m,
                BaseQuantity = computed.BaseQuantity,
                UnitPrice = reqLine.UnitPrice,
                DiscountPercent = reqLine.DiscountPercent,
                DiscountAmount = computed.DiscountAmount,
                GrossAmount = computed.GrossAmount,
                TaxableAmount = computed.TaxableAmount,
                TaxMasterId = rate?.TaxMasterId,
                TaxGroupId = rate?.TaxGroupId ?? taxGroupId,
                TaxAmount = computed.TaxAmount,
                LineTotal = computed.LineTotal,
            };

            foreach (var comp in computed.Components)
            {
                line.Taxes.Add(new DeliveryChallanDetailTax
                {
                    OrgId = deliveryChallan.OrgId,
                    TaxComponent = comp.Component,
                    SubAccountId = taxGroupId ?? 0,
                    Rate = comp.Rate,
                    TaxableAmount = comp.TaxableAmount,
                    Amount = comp.Amount,
                    AmountBase = comp.Amount * deliveryChallan.ExchangeRate,
                });
            }

            deliveryChallan.Lines.Add(line);
        }

        TaxDocumentTotals totals = GstCalculator.Totals(taxLines);

        deliveryChallan.SubTotal = totals.SubTotal;
        deliveryChallan.DiscountAmount = totals.DiscountAmount;
        deliveryChallan.TaxableAmount = totals.TaxableAmount;
        deliveryChallan.CgstAmount = totals.CgstAmount;
        deliveryChallan.SgstAmount = totals.SgstAmount;
        deliveryChallan.IgstAmount = totals.IgstAmount;
        deliveryChallan.CessAmount = totals.CessAmount;
        deliveryChallan.RoundOffAmount =
            Math.Round(totals.TotalAmount, 0, MidpointRounding.AwayFromZero) - totals.TotalAmount;
        deliveryChallan.TotalAmount = totals.TotalAmount + deliveryChallan.RoundOffAmount;
        deliveryChallan.TotalAmountBase = deliveryChallan.TotalAmount * deliveryChallan.ExchangeRate;

        await _db.SaveChangesAsync(ct);
        return new DeliveryChallanResult(DeliveryChallanOutcome.Ok, deliveryChallan.DeliveryChallanId);
    }

    /// <summary>
    /// Dispatches the goods: issues the stock, and moves the order's delivered
    /// and reserved quantities when the challan is against one.
    ///
    /// <b>It writes nothing to the ledger and nothing to the sales register.</b>
    ///
    /// Not the register, because a delivery challan is not a supply under GST —
    /// the invoice raised from it is, and GSTR-1 and GSTR-3B read every row of
    /// <c>sal.SalesRegister</c> with no filter on document type, so a challan's
    /// rows were counted a second time beside its invoice's.
    ///
    /// Not the ledger, because the stock issue already reaches it: Inventory's
    /// costing worker posts every issue movement, this one included, at the cost
    /// it settles. The challan's own <c>Dr Goods Delivered Not Invoiced / Cr
    /// Inventory</c> post sat beside that one, so Inventory was credited twice,
    /// and it named an account the chart of accounts does not seed. Where a sale
    /// challan's cost should land — GDNI rather than cost of sales — is the
    /// worker's mapping to change, which is its own card.
    /// </summary>
    public async Task<DeliveryChallanResult> PostAsync(long deliveryChallanId, CancellationToken ct)
    {
        var (customerId, _) = _tenant.Require();

        // Resolved before Inventory is called, which is over HTTP and outside
        // this transaction (TK-22).
        StorageScope archiveScope = _archive.Scope();

        DeliveryChallan? deliveryChallan = await _db.DeliveryChallans
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.DeliveryChallanId == deliveryChallanId, ct);

        if (deliveryChallan is null)
        {
            return new DeliveryChallanResult(DeliveryChallanOutcome.NotFound);
        }

        DocumentTransition post = DocumentLifecycle.CanPost(deliveryChallan.Status, deliveryChallan.Lines.Count);
        if (!post.IsAllowed)
        {
            return new DeliveryChallanResult(
                DeliveryChallanOutcome.LifecycleRefused, deliveryChallanId, post.Detail);
        }

        // The order is read again rather than trusted from the save: another
        // challan may have delivered against it since this one was drafted.
        SalesOrder? salesOrder = null;
        if (deliveryChallan.SalesOrderId.HasValue)
        {
            salesOrder = await _db.SalesOrders
                .Include(x => x.Lines)
                .FirstOrDefaultAsync(x => x.SalesOrderId == deliveryChallan.SalesOrderId.Value, ct);

            string? orderProblem = OrderProblem(salesOrder, deliveryChallan.ContactId);
            if (orderProblem is not null)
            {
                return new DeliveryChallanResult(
                    DeliveryChallanOutcome.SourceInvalid, deliveryChallanId, orderProblem);
            }

            foreach (var byOrderLine in deliveryChallan.Lines.GroupBy(l => l.SalesOrderDetailId))
            {
                SalesOrderDetail? orderLine = byOrderLine.Key is long orderLineId
                    ? salesOrder!.Lines.FirstOrDefault(x => x.SalesOrderDetailId == orderLineId)
                    : null;

                if (orderLine is null)
                {
                    return new DeliveryChallanResult(
                        DeliveryChallanOutcome.LineInvalid, deliveryChallanId,
                        "A line on this challan is not a line of its sales order.");
                }

                decimal outstanding = orderLine.Quantity - orderLine.DeliveredQuantity;
                decimal delivering = byOrderLine.Sum(l => l.Quantity);
                if (delivering > outstanding)
                {
                    return new DeliveryChallanResult(
                        DeliveryChallanOutcome.OverDelivered, deliveryChallanId,
                        $"Order line {orderLine.LineNumber} has {outstanding:0.####} left to deliver, "
                            + $"and this challan delivers {delivering:0.####}.");
                }
            }
        }

        var issueRequest = new IssueStockRequest
        {
            OrgId = deliveryChallan.OrgId,
            CustomerId = customerId,
            MovementDate = deliveryChallan.DocumentDate,
            SourceType = deliveryChallan.TransactionTypeCode,
            SourceId = deliveryChallan.DeliveryChallanId,
            Lines = deliveryChallan.Lines.Select(l => new IssueStockLine
            {
                SourceLineId = l.DeliveryChallanDetailId,
                ItemId = l.ItemId ?? 0,
                Quantity = l.Quantity,
                WarehouseId = l.WarehouseId,
                ReleaseReservation = l.SalesOrderDetailId.HasValue,
            }).ToList(),
        };

        var issueResult = await _inventoryClient.IssueAsync(issueRequest, ct);
        if (!issueResult.Success)
        {
            return new DeliveryChallanResult(
                DeliveryChallanOutcome.StockRefused, deliveryChallanId,
                "Inventory refused to issue the goods on this challan — usually there is not "
                    + "enough on hand. Nothing was dispatched.");
        }

        foreach (var issueLine in issueResult.Lines)
        {
            DeliveryChallanDetail? line = deliveryChallan.Lines
                .FirstOrDefault(l => l.DeliveryChallanDetailId == issueLine.SourceLineId);
            if (line is not null)
            {
                line.StockMovementId = issueLine.StockMovementId;
                line.UnitCost = issueLine.UnitCost;
            }
        }

        if (salesOrder is not null)
        {
            foreach (DeliveryChallanDetail line in deliveryChallan.Lines)
            {
                SalesOrderDetail orderLine = salesOrder.Lines
                    .First(x => x.SalesOrderDetailId == line.SalesOrderDetailId);

                orderLine.DeliveredQuantity += line.Quantity;

                // Never below zero: an order confirmed while stock was short
                // reserved less than it ordered, and delivering the rest must
                // not leave the order claiming a negative hold.
                orderLine.ReservedQuantity = Math.Max(0m, orderLine.ReservedQuantity - line.Quantity);
            }

            SalesOrderFulfilment.Refresh(salesOrder);
        }

        deliveryChallan.Status = DocumentStatus.Posted;
        deliveryChallan.PostedAt = _clock.GetUtcNow();
        deliveryChallan.PostedBy = _user.UserId;

        await _archive.ArchiveAsync(
            archiveScope,
            Sales.Api.Services.Pdf.ArchivedSalesDocument.DeliveryChallan,
            deliveryChallan.DeliveryChallanId,
            "DELIVERY CHALLAN",
            "Challan No",
            deliveryChallan,
            deliveryChallan.Lines,
            salesOrder is null ? null : $"Against sales order {salesOrder.DocumentNo}",
            ct);

        await _db.SaveChangesAsync(ct);
        return new DeliveryChallanResult(DeliveryChallanOutcome.Ok, deliveryChallanId);
    }

    /// <summary>
    /// Withdraws a draft.
    ///
    /// <b>Only a draft.</b> A posted challan has already taken stock off the
    /// shelf and moved its order; flipping the status would leave both behind
    /// with nothing pointing at them. Reversing those is its own document — a
    /// return — not a status change, so it is refused here rather than half-done.
    ///
    /// A draft moved nothing, so voiding one moves nothing back. This used to
    /// subtract the challan's quantities from its order's delivered figure — a
    /// delivery that had never been added.
    /// </summary>
    public async Task<DeliveryChallanResult> VoidAsync(
        long deliveryChallanId, string reason, CancellationToken ct)
    {
        DeliveryChallan? deliveryChallan = await _db.DeliveryChallans
            .FirstOrDefaultAsync(x => x.DeliveryChallanId == deliveryChallanId, ct);

        if (deliveryChallan is null)
        {
            return new DeliveryChallanResult(DeliveryChallanOutcome.NotFound);
        }

        if (deliveryChallan.Status == DocumentStatus.Posted)
        {
            return new DeliveryChallanResult(
                DeliveryChallanOutcome.LifecycleRefused, deliveryChallanId,
                "This challan has already dispatched its goods. Voiding it would leave the "
                    + "stock issued and the order marked delivered. Raise a return instead.");
        }

        DocumentTransition transition = DocumentLifecycle.CanVoid(deliveryChallan.Status, false, reason);
        if (!transition.IsAllowed)
        {
            return new DeliveryChallanResult(
                DeliveryChallanOutcome.LifecycleRefused, deliveryChallanId, transition.Detail);
        }

        deliveryChallan.Status = DocumentStatus.Void;
        deliveryChallan.VoidedAt = _clock.GetUtcNow();

        // Set together with the timestamp, because chk_deliverychallans_void_stamp
        // requires exactly that — a void carrying only the stamp was refused by
        // the database, so this path threw before the reason was passed in.
        deliveryChallan.VoidReason = reason.Trim();
        deliveryChallan.VoidedBy = _user.UserId;

        await _db.SaveChangesAsync(ct);
        return new DeliveryChallanResult(DeliveryChallanOutcome.Ok, deliveryChallanId);
    }

    /// <summary>
    /// The request's order link, checked against the order: the lines name its
    /// lines and its items, and it is for this customer. Null when it holds.
    /// </summary>
    private async Task<DeliveryChallanResult?> CheckOrderLinkAsync(
        SaveDeliveryChallanRequest request, CancellationToken ct)
    {
        if (!request.SalesOrderId.HasValue)
        {
            return request.Lines.Any(l => l.SalesOrderDetailId.HasValue)
                ? new DeliveryChallanResult(
                    DeliveryChallanOutcome.LineInvalid,
                    Detail: "A line names a sales order line, but the challan names no sales order.")
                : null;
        }

        SalesOrder? order = await _db.SalesOrders
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.SalesOrderId == request.SalesOrderId.Value, ct);

        string? problem = OrderProblem(order, request.ContactId);
        if (problem is not null)
        {
            return new DeliveryChallanResult(DeliveryChallanOutcome.SourceInvalid, Detail: problem);
        }

        foreach (SaveDeliveryChallanLineRequest line in request.Lines)
        {
            SalesOrderDetail? orderLine = line.SalesOrderDetailId is long id
                ? order!.Lines.FirstOrDefault(x => x.SalesOrderDetailId == id)
                : null;

            if (orderLine is null)
            {
                return new DeliveryChallanResult(
                    DeliveryChallanOutcome.LineInvalid,
                    Detail: "Every line on a challan against a sales order must be one of that "
                        + "order's lines. Remove the line, or raise it on a challan of its own.");
            }

            if (orderLine.ItemId != line.ItemId)
            {
                return new DeliveryChallanResult(
                    DeliveryChallanOutcome.LineInvalid,
                    Detail: $"A line's item is not the item on order line {orderLine.LineNumber}.");
            }
        }

        return null;
    }

    /// <summary>Why goods cannot go out against this order, or null when they can.</summary>
    private static string? OrderProblem(SalesOrder? order, long contactId)
    {
        if (order is null)
        {
            return "The sales order this challan names could not be found in this branch.";
        }

        if (order.Status != DocumentStatus.Posted)
        {
            return "Only a confirmed sales order can be delivered against.";
        }

        if (order.FulfilmentStatus is FulfilmentStatus.Closed or FulfilmentStatus.Cancelled)
        {
            return "This sales order is already closed.";
        }

        if (order.ContactId != contactId)
        {
            return "The challan's customer is not the sales order's customer.";
        }

        return null;
    }

    private static long? TaxGroupOf(SaveDeliveryChallanLineRequest line) =>
        line.TaxGroupId ?? (line.TaxGroupIds.Count > 0 ? line.TaxGroupIds[0] : null);
}
