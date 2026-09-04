using Microsoft.EntityFrameworkCore;
using Purchase.Entity.Enums;
using Purchase.Entity.Models;
using Purchase.Entity.TableEntities;
using Purchase.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tax;
using Shared.Kernel.Tenancy;

namespace Purchase.Api.Services;

/// <summary>
/// The goods receipt — <c>GRN</c>. The goods arrived.
///
/// <b>This is the first document in the module that touches anything outside
/// itself.</b> Posting one moves stock in Inventory, writes a posting in
/// Accounting, and advances the purchase order it came against. The order those
/// three happen in is the whole design of this class:
///
/// <list type="number">
/// <item>stock first, because it is the one that can genuinely refuse — an
/// unknown item, a batch-tracked item with no batch — and refusing before
/// anything else has happened leaves nothing to undo;</item>
/// <item>the ledger second, from figures taken off the document rather than out
/// of Inventory's reply, so a retry after a ledger failure posts the same
/// amount rather than zero;</item>
/// <item>the order's received quantities and the document's own status last, in
/// one <c>SaveChanges</c>, so a failure anywhere above leaves the receipt a
/// draft that can simply be posted again.</item>
/// </list>
///
/// <b>Both external calls are idempotent on the document's own key</b>, which is
/// what makes that retry safe: Inventory dedupes a movement on
/// (source type, source id, line) and reports it already recorded, and
/// Accounting replaces a posting keyed on the same document rather than adding
/// to it.
/// </summary>
public sealed class GoodsReceiptService
{
    /// <summary>Goods on the shelf. Debited by what was accepted, at what it cost.</summary>
    private const string InventoryAccount = "Inventory";

    /// <summary>
    /// The clearing account the receipt credits. The bill clears it against
    /// Accounts Payable; a balance sitting in it is stock held and not yet
    /// billed. Decision T4.1.
    /// </summary>
    private const string GrniAccount = "Goods Received Not Invoiced";

    /// <summary>1 ITEM — the line-level leg. 3 CONTROL — the document's other side.</summary>
    private const int ItemLedgerType = 1;

    private const int ControlLedgerType = 3;

    /// <summary>1 TRANSACTION — an ordinary document posting.</summary>
    private const int TransactionLedgerSource = 1;

    private readonly PurchaseDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly INumberGenerator _numbering;
    private readonly IBaseCurrencyProvider _baseCurrency;
    private readonly IBranchSettingsProvider _branchSettings;
    private readonly ITaxRateProvider _rates;
    private readonly IContactNameLookup _contactNames;
    private readonly IItemNameLookup _itemNames;
    private readonly IInventoryClient _inventory;
    private readonly ILedgerClient _ledger;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public GoodsReceiptService(
        PurchaseDbContext db,
        ITenantContext tenant,
        INumberGenerator numbering,
        IBaseCurrencyProvider baseCurrency,
        IBranchSettingsProvider branchSettings,
        ITaxRateProvider rates,
        IContactNameLookup contactNames,
        IItemNameLookup itemNames,
        IInventoryClient inventory,
        ILedgerClient ledger,
        ICurrentUser user,
        TimeProvider clock)
    {
        _db = db;
        _tenant = tenant;
        _numbering = numbering;
        _baseCurrency = baseCurrency;
        _branchSettings = branchSettings;
        _rates = rates;
        _contactNames = contactNames;
        _itemNames = itemNames;
        _inventory = inventory;
        _ledger = ledger;
        _user = user;
        _clock = clock;
    }

    public async Task<GoodsReceiptResult> CreateAsync(
        SaveGoodsReceiptRequest request, CancellationToken ct)
    {
        string? baseCurrency = await _baseCurrency.GetBaseCurrencyAsync(ct);
        if (baseCurrency is null)
        {
            return new GoodsReceiptResult(
                GoodsReceiptOutcome.RatesUnavailable,
                Detail: "Branch base currency could not be read.");
        }

        GoodsReceipt receipt = new()
        {
            TransactionTypeCode = "GRN",
            DocumentDate = request.DocumentDate,
            CurrencyCode = request.CurrencyCode ?? baseCurrency,
            ExchangeRate = request.ExchangeRate ?? 1m,
            Status = DocumentStatus.Draft,
        };

        GoodsReceiptResult applied = await ApplyAsync(receipt, request, ct);
        if (applied.Outcome != GoodsReceiptOutcome.Ok)
        {
            return applied;
        }

        // Allocated last, so a request refused above never spends a number.
        NumberAllocation alloc = await _numbering.NextAsync("GRN", request.DocumentDate, ct);
        receipt.DocumentNo = alloc.Code;

        _db.GoodsReceipts.Add(receipt);
        await _db.SaveChangesAsync(ct);

        return new GoodsReceiptResult(GoodsReceiptOutcome.Ok, receipt.GoodsReceiptId);
    }

    public async Task<GoodsReceiptResult> UpdateAsync(
        long goodsReceiptId, SaveGoodsReceiptRequest request, CancellationToken ct)
    {
        GoodsReceipt? receipt = await _db.GoodsReceipts
            .Include(r => r.Lines)
            .ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(r => r.GoodsReceiptId == goodsReceiptId, ct);

        if (receipt is null)
        {
            return new GoodsReceiptResult(GoodsReceiptOutcome.NotFound);
        }

        DocumentTransition transition = DocumentLifecycle.CanEdit(receipt.Status);
        if (!transition.IsAllowed)
        {
            return new GoodsReceiptResult(
                GoodsReceiptOutcome.LifecycleRefused, Detail: transition.Detail);
        }

        if (request.CurrencyCode is not null)
        {
            receipt.CurrencyCode = request.CurrencyCode;
        }

        if (request.ExchangeRate.HasValue)
        {
            receipt.ExchangeRate = request.ExchangeRate.Value;
        }

        receipt.DocumentDate = request.DocumentDate;

        _db.GoodsReceiptDetailTaxes.RemoveRange(receipt.Lines.SelectMany(l => l.Taxes));
        _db.GoodsReceiptDetails.RemoveRange(receipt.Lines);
        receipt.Lines.Clear();

        GoodsReceiptResult applied = await ApplyAsync(receipt, request, ct);
        if (applied.Outcome != GoodsReceiptOutcome.Ok)
        {
            return applied;
        }

        await _db.SaveChangesAsync(ct);
        return new GoodsReceiptResult(GoodsReceiptOutcome.Ok, receipt.GoodsReceiptId);
    }

    /// <summary>Everything create and update do identically. One copy, called twice.</summary>
    private async Task<GoodsReceiptResult> ApplyAsync(
        GoodsReceipt receipt, SaveGoodsReceiptRequest request, CancellationToken ct)
    {
        BranchSettings? settings = await _branchSettings.GetSettingsAsync(ct);
        if (settings is null)
        {
            return new GoodsReceiptResult(
                GoodsReceiptOutcome.RatesUnavailable,
                Detail: "Branch settings could not be read.");
        }

        PlaceOfSupplyResult pos = PlaceOfSupply.Resolve(
            settings.StateCode, request.PlaceOfSupplyStateCode, request.ContactGstin);

        if (!pos.IsOk)
        {
            return new GoodsReceiptResult(
                GoodsReceiptOutcome.PlaceOfSupplyRefused, Detail: pos.Detail);
        }

        // The order, when one was named. Checked here rather than trusted: a
        // receipt filed against another vendor's order would advance the wrong
        // order's fulfilment and make a bill match against nothing.
        if (request.PurchaseOrderId is long orderId)
        {
            PurchaseOrder? order = await _db.PurchaseOrders
                .FirstOrDefaultAsync(o => o.PurchaseOrderId == orderId, ct);

            if (order is null)
            {
                return new GoodsReceiptResult(
                    GoodsReceiptOutcome.OrderInvalid,
                    Detail: "That purchase order does not exist in this branch.");
            }

            if (order.ContactId != request.ContactId)
            {
                return new GoodsReceiptResult(
                    GoodsReceiptOutcome.OrderInvalid,
                    Detail: "That purchase order belongs to a different vendor.");
            }

            if (order.Status == DocumentStatus.Void)
            {
                return new GoodsReceiptResult(
                    GoodsReceiptOutcome.OrderInvalid,
                    Detail: "That purchase order has been voided.");
            }
        }

        receipt.PurchaseOrderId = request.PurchaseOrderId;
        receipt.VendorDeliveryNoteNo = request.VendorDeliveryNoteNo;
        receipt.VendorDeliveryNoteDate = request.VendorDeliveryNoteDate;
        receipt.ReceivedBy = request.ReceivedBy ?? _user.UserId;
        receipt.ContactId = request.ContactId;
        receipt.ContactGstin = request.ContactGstin;
        receipt.BillingAddress = request.BillingAddress;
        receipt.ShippingAddress = request.ShippingAddress;
        receipt.IsInterState = pos.IsInterState;
        receipt.Notes = request.Notes;
        receipt.TermsAndConditions = request.TermsAndConditions;

        TaxContext taxContext = new(pos.IsInterState, settings.DiscountBeforeTax);
        List<TaxLineResult> taxLines = new(request.Lines.Count);

        for (int i = 0; i < request.Lines.Count; i++)
        {
            SaveGoodsReceiptLineRequest lineReq = request.Lines[i];
            int lineNumber = i + 1;

            // A goods receipt receives goods. An expense or a capital line has
            // nothing arriving to count, and both belong on the bill — freight is
            // landed cost and an asset is capitalised from the document that buys
            // it. Refusing here is clearer than half-supporting them.
            if (lineReq.LineType != DocumentLineType.Stock)
            {
                return new GoodsReceiptResult(
                    GoodsReceiptOutcome.LineInvalid,
                    Detail: $"Line {lineNumber} is an expense or capital line. A goods receipt "
                        + "records goods arriving; put those on the bill instead.");
            }

            if (lineReq.ItemId is null)
            {
                return new GoodsReceiptResult(
                    GoodsReceiptOutcome.LineInvalid,
                    Detail: $"Line {lineNumber} is a stock line, so it needs an item.");
            }

            // What arrived is what was accepted plus what was refused. The
            // database enforces this too; catching it here names the line.
            if (lineReq.AcceptedQuantity + lineReq.RejectedQuantity != lineReq.Quantity)
            {
                return new GoodsReceiptResult(
                    GoodsReceiptOutcome.LineInvalid,
                    Detail: $"Line {lineNumber}: accepted plus rejected must equal the quantity "
                        + $"delivered ({lineReq.Quantity}).");
            }

            if (lineReq.RejectedQuantity > 0
                && string.IsNullOrWhiteSpace(lineReq.RejectionReason))
            {
                return new GoodsReceiptResult(
                    GoodsReceiptOutcome.LineInvalid,
                    Detail: $"Line {lineNumber} rejects {lineReq.RejectedQuantity}, so it needs a "
                        + "reason. A rejection with no reason is an argument with the vendor that "
                        + "nobody can reconstruct.");
            }

            TaxRate? rate = null;
            if (lineReq.TaxGroupId is long taxGroupId)
            {
                rate = await _rates.GetRateAsync(taxGroupId, request.DocumentDate, ct);
                if (rate is null)
                {
                    return new GoodsReceiptResult(
                        GoodsReceiptOutcome.RatesUnavailable,
                        Detail: $"Tax rate for group {taxGroupId} could not be read for "
                            + $"{request.DocumentDate}.");
                }
            }

            TaxLineResult computed = GstCalculator.Compute(
                new TaxLineInput
                {
                    Quantity = lineReq.Quantity,
                    UnitPrice = lineReq.UnitPrice,
                    DiscountPercent = lineReq.DiscountPercent,
                    DiscountAmount = lineReq.DiscountAmount,
                    IsPriceInclusive = lineReq.IsPriceInclusive,
                    TaxTreatment = lineReq.TaxTreatment,
                    Rate = rate,
                    ConversionFactor = lineReq.ConversionFactor,
                },
                taxContext);

            taxLines.Add(computed);

            GoodsReceiptDetail detail = new()
            {
                LineNumber = lineNumber,
                PurchaseOrderDetailId = lineReq.PurchaseOrderDetailId,
                ItemId = lineReq.ItemId,
                Description = lineReq.Description,
                HsnSacCode = lineReq.HsnSacCode,
                WarehouseId = lineReq.WarehouseId,
                Quantity = lineReq.Quantity,
                AcceptedQuantity = lineReq.AcceptedQuantity,
                RejectedQuantity = lineReq.RejectedQuantity,
                RejectionReason = string.IsNullOrWhiteSpace(lineReq.RejectionReason)
                    ? null
                    : lineReq.RejectionReason,
                UomId = lineReq.UomId,
                ConversionFactor = lineReq.ConversionFactor,
                BaseQuantity = computed.BaseQuantity,
                UnitPrice = lineReq.UnitPrice,
                IsPriceInclusive = lineReq.IsPriceInclusive,
                DiscountPercent = lineReq.DiscountPercent,
                DiscountAmount = computed.DiscountAmount,
                GrossAmount = computed.GrossAmount,
                TaxableAmount = computed.TaxableAmount,
                TaxTreatment = lineReq.TaxTreatment,
                TaxMasterId = rate?.TaxMasterId,
                TaxGroupId = rate?.TaxGroupId,
                TaxAmount = computed.TaxAmount,
                LineType = lineReq.LineType,
                AccountId = lineReq.AccountId,
                FixedAssetCategoryId = lineReq.FixedAssetCategoryId,
                LineTotal = computed.LineTotal,
                ItemBatchId = lineReq.ItemBatchId,
                LineNotes = lineReq.LineNotes,
            };

            foreach (TaxComponentAmount component in computed.Components)
            {
                detail.Taxes.Add(new GoodsReceiptDetailTax
                {
                    TaxComponent = component.Component,
                    Rate = component.Rate,
                    TaxableAmount = component.TaxableAmount,
                    Amount = component.Amount,
                    AmountBase = component.Amount * receipt.ExchangeRate,
                });
            }

            receipt.Lines.Add(detail);
        }

        TaxDocumentTotals totals = GstCalculator.Totals(taxLines);

        receipt.SubTotal = totals.SubTotal;
        receipt.DiscountAmount = totals.DiscountAmount;
        receipt.TaxableAmount = totals.TaxableAmount;
        receipt.CgstAmount = totals.CgstAmount;
        receipt.SgstAmount = totals.SgstAmount;
        receipt.IgstAmount = totals.IgstAmount;
        receipt.CessAmount = totals.CessAmount;
        receipt.RoundOffAmount =
            Math.Round(totals.TotalAmount, 0, MidpointRounding.AwayFromZero) - totals.TotalAmount;
        receipt.TotalAmount = totals.TotalAmount + receipt.RoundOffAmount;
        receipt.TotalAmountBase = receipt.TotalAmount * receipt.ExchangeRate;

        return new GoodsReceiptResult(GoodsReceiptOutcome.Ok, receipt.GoodsReceiptId);
    }

    /// <summary>
    /// Posts the receipt: stock in, ledger written, the order advanced.
    ///
    /// <b>The tax on the lines is not posted here.</b> A receipt carries the tax
    /// figures so the document reads correctly and so the bill can be matched
    /// against it, but input credit is claimed against the <i>vendor's</i>
    /// invoice number, which this document does not have. The GST legs arrive
    /// with the bill; what posts here is goods against the clearing account.
    /// </summary>
    public async Task<GoodsReceiptResult> PostAsync(long goodsReceiptId, CancellationToken ct)
    {
        GoodsReceipt? receipt = await _db.GoodsReceipts
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.GoodsReceiptId == goodsReceiptId, ct);

        if (receipt is null)
        {
            return new GoodsReceiptResult(GoodsReceiptOutcome.NotFound);
        }

        DocumentTransition transition =
            DocumentLifecycle.CanPost(receipt.Status, receipt.Lines.Count);

        if (!transition.IsAllowed)
        {
            return new GoodsReceiptResult(
                GoodsReceiptOutcome.LifecycleRefused, Detail: transition.Detail);
        }

        (Guid customerId, Guid orgId) = _tenant.Require();

        // Only what was accepted, and only lines that actually carry stock. A
        // line fully rejected moves nothing and is skipped rather than sent as a
        // zero, which Inventory would refuse as a non-positive quantity.
        List<GoodsReceiptDetail> stockLines = receipt.Lines
            .Where(l => l.ItemId.HasValue && l.AcceptedQuantity > 0)
            .OrderBy(l => l.LineNumber)
            .ToList();

        if (stockLines.Count > 0)
        {
            ReceiveStockResponse stock = await _inventory.ReceiveAsync(
                new ReceiveStockRequest
                {
                    CustomerId = customerId,
                    OrgId = orgId,
                    MovementDate = receipt.DocumentDate,
                    SourceType = "GRN",
                    SourceId = receipt.GoodsReceiptId,
                    Lines = stockLines
                        .Select(l => new ReceiveStockLine
                        {
                            SourceLineId = l.LineNumber,
                            ItemId = l.ItemId!.Value,
                            Quantity = l.AcceptedQuantity,
                            UnitCost = NetUnitCost(l),
                            WarehouseId = l.WarehouseId,
                            UomId = l.UomId,
                            ItemBatchId = l.ItemBatchId,
                        })
                        .ToList(),
                },
                ct);

            if (!stock.Success)
            {
                string refused = string.Join(
                    "; ",
                    stock.Lines
                        .Where(l => !l.Success)
                        .Select(l => $"line {l.SourceLineId}: {l.Outcome}"));

                return new GoodsReceiptResult(
                    GoodsReceiptOutcome.StockRefused,
                    Detail: refused.Length > 0
                        ? $"Inventory refused the receipt — {refused}."
                        : "Inventory refused the receipt and gave no reason.");
            }
        }

        // Computed from the document, not from Inventory's reply. A retry after a
        // failed posting finds every movement already recorded and would be told
        // the value was zero; the amount owed to the clearing account is a
        // property of the receipt, not of how many times it has been sent.
        decimal receivedValue = stockLines.Sum(NetLineValue);

        if (receivedValue > 0)
        {
            List<LedgerLegRequest> legs = stockLines
                .Where(l => NetLineValue(l) > 0)
                .Select(l => new LedgerLegRequest
                {
                    LedgerTypeId = ItemLedgerType,
                    LedgerSourceId = TransactionLedgerSource,
                    TransactionDetailId = l.GoodsReceiptDetailId,
                    AccountSystemName = InventoryAccount,
                    DebitAmount = NetLineValue(l),
                    TransactionDesc = "Goods received",
                })
                .ToList();

            // One credit for the document, against the clearing account the bill
            // will later clear. Its balance is goods held and not yet billed.
            legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = ControlLedgerType,
                LedgerSourceId = TransactionLedgerSource,
                TransactionDetailId = 0,
                AccountSystemName = GrniAccount,
                CreditAmount = receivedValue,
                TransactionDesc = "Goods received not invoiced",
            });

            PostLedgerOutcomeResult posted = await _ledger.PostAsync(
                new PostLedgerRequest
                {
                    CustomerId = customerId,
                    OrgId = orgId,
                    TransactionTypeCode = "GRN",
                    TransactionId = receipt.GoodsReceiptId,
                    // The number on the document's face, so the ledger can report it
                    // without reaching into this service's schema to look it up.
                    DocumentNo = receipt.DocumentNo,
                    LedgerDate = receipt.DocumentDate,
                    ContactId = receipt.ContactId,
                    SourceDocumentId = receipt.GoodsReceiptId,
                    Legs = legs,
                },
                ct);

            if (!posted.Posted)
            {
                // Left a draft on purpose. The stock is recorded and dedupes on
                // replay, so posting again is the whole recovery.
                return new GoodsReceiptResult(
                    GoodsReceiptOutcome.PostingRefused, Detail: posted.Detail);
            }
        }

        await AdvanceOrderAsync(receipt, ct);

        receipt.Status = DocumentStatus.Posted;
        receipt.PostedAt = _clock.GetUtcNow();
        receipt.PostedBy = _user.UserId;

        await _db.SaveChangesAsync(ct);
        return new GoodsReceiptResult(GoodsReceiptOutcome.Ok, receipt.GoodsReceiptId);
    }

    /// <summary>
    /// Adds what arrived to the order's running received quantities, and moves
    /// the order's fulfilment status to match.
    ///
    /// <b>Only the accepted quantity counts.</b> An order for ten, delivered ten
    /// with two refused, has been received eight — and stays partly open, which
    /// is the honest answer: two are still owed.
    /// </summary>
    private async Task AdvanceOrderAsync(GoodsReceipt receipt, CancellationToken ct)
    {
        if (receipt.PurchaseOrderId is not long orderId)
        {
            return;
        }

        PurchaseOrder? order = await _db.PurchaseOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.PurchaseOrderId == orderId, ct);

        if (order is null)
        {
            return;
        }

        foreach (GoodsReceiptDetail line in receipt.Lines)
        {
            if (line.PurchaseOrderDetailId is not long orderLineId)
            {
                continue;
            }

            PurchaseOrderDetail? orderLine = order.Lines
                .FirstOrDefault(l => l.PurchaseOrderDetailId == orderLineId);

            if (orderLine is not null)
            {
                orderLine.ReceivedQuantity += line.AcceptedQuantity;
            }
        }

        // Closed only when every line is satisfied. An order closed short by
        // agreement is a different act, and it sets this column directly rather
        // than arriving here.
        bool anyReceived = order.Lines.Exists(l => l.ReceivedQuantity > 0);
        bool allReceived = order.Lines.TrueForAll(l => l.ReceivedQuantity >= l.Quantity);

        order.FulfilmentStatus = allReceived
            ? PurchaseFulfilmentStatus.Closed
            : anyReceived
                ? PurchaseFulfilmentStatus.PartlyReceived
                : PurchaseFulfilmentStatus.Open;
    }

    /// <summary>
    /// What one unit actually cost, net of discount and before tax.
    ///
    /// <b>Divided by the delivered quantity, not the accepted one.</b> The
    /// discount was agreed against the whole line; spreading it over only the
    /// accepted part would make rejecting a carton quietly raise the cost of
    /// every carton kept.
    /// </summary>
    private static decimal NetUnitCost(GoodsReceiptDetail line) =>
        line.Quantity == 0 ? 0m : line.TaxableAmount / line.Quantity;

    /// <summary>What the accepted part of a line is worth. Inventory and GRNI both move by this.</summary>
    private static decimal NetLineValue(GoodsReceiptDetail line) =>
        Math.Round(NetUnitCost(line) * line.AcceptedQuantity, 2, MidpointRounding.AwayFromZero);

    public async Task<GoodsReceiptResult> VoidAsync(
        long goodsReceiptId, VoidGoodsReceiptRequest request, CancellationToken ct)
    {
        GoodsReceipt? receipt = await _db.GoodsReceipts
            .FirstOrDefaultAsync(r => r.GoodsReceiptId == goodsReceiptId, ct);

        if (receipt is null)
        {
            return new GoodsReceiptResult(GoodsReceiptOutcome.NotFound);
        }

        bool hasBill = await _db.Bills.AnyAsync(b => b.GoodsReceiptId == goodsReceiptId, ct);

        DocumentTransition transition =
            DocumentLifecycle.CanVoid(receipt.Status, hasBill, request.Reason);

        if (!transition.IsAllowed)
        {
            return transition.Outcome == DocumentTransitionOutcome.HasDownstream
                ? new GoodsReceiptResult(
                    GoodsReceiptOutcome.AlreadyBilled, Detail: transition.Detail)
                : new GoodsReceiptResult(
                    GoodsReceiptOutcome.LifecycleRefused, Detail: transition.Detail);
        }

        // A posted receipt has moved stock and written a posting, and neither is
        // undone here. Voiding one is a reversal document, not an erasure — the
        // same reason a stock adjustment sheet has no void. Refused explicitly so
        // nobody discovers it by finding the stock still there afterwards.
        if (receipt.Status == DocumentStatus.Posted)
        {
            return new GoodsReceiptResult(
                GoodsReceiptOutcome.LifecycleRefused,
                Detail: "This receipt has been posted, so the stock is on the shelf and the "
                    + "ledger has it. Voiding cannot un-receive goods — raise a debit note to "
                    + "send them back, which reverses both.");
        }

        receipt.Status = DocumentStatus.Void;
        receipt.VoidedAt = _clock.GetUtcNow();
        receipt.VoidedBy = _user.UserId;
        receipt.VoidReason = request.Reason;

        await _db.SaveChangesAsync(ct);
        return new GoodsReceiptResult(GoodsReceiptOutcome.Ok, receipt.GoodsReceiptId);
    }

    public async Task<GoodsReceiptView?> GetAsync(long goodsReceiptId, CancellationToken ct)
    {
        GoodsReceiptView? receipt = await _db.GoodsReceipts
            .Where(r => r.GoodsReceiptId == goodsReceiptId)
            .Select(r => new GoodsReceiptView
            {
                GoodsReceiptId = r.GoodsReceiptId,
                DocumentNo = r.DocumentNo,
                DocumentDate = r.DocumentDate,
                PurchaseOrderId = r.PurchaseOrderId,
                PurchaseOrderNo = _db.PurchaseOrders
                    .Where(o => o.PurchaseOrderId == r.PurchaseOrderId)
                    .Select(o => o.DocumentNo)
                    .FirstOrDefault(),
                VendorDeliveryNoteNo = r.VendorDeliveryNoteNo,
                VendorDeliveryNoteDate = r.VendorDeliveryNoteDate,
                ReceivedBy = r.ReceivedBy,
                ContactId = r.ContactId,
                CurrencyCode = r.CurrencyCode,
                TaxableAmount = r.TaxableAmount,
                TotalAmount = r.TotalAmount,
                Status = r.Status.ToString(),
                IsInterState = r.IsInterState,
                RejectedQuantity = r.Lines.Sum(l => l.RejectedQuantity),
                ContactGstin = r.ContactGstin,
                PlaceOfSupplyStateId = r.PlaceOfSupplyStateId,
                BillingAddress = r.BillingAddress,
                ShippingAddress = r.ShippingAddress,
                ExchangeRate = r.ExchangeRate,
                SubTotal = r.SubTotal,
                DiscountAmount = r.DiscountAmount,
                CgstAmount = r.CgstAmount,
                SgstAmount = r.SgstAmount,
                IgstAmount = r.IgstAmount,
                CessAmount = r.CessAmount,
                RoundOffAmount = r.RoundOffAmount,
                TotalAmountBase = r.TotalAmountBase,
                Notes = r.Notes,
                TermsAndConditions = r.TermsAndConditions,
                PostedAt = r.PostedAt,
                VoidedAt = r.VoidedAt,
                VoidReason = r.VoidReason,
                Lines = r.Lines
                    .OrderBy(l => l.LineNumber)
                    .Select(l => new GoodsReceiptLineView
                    {
                        GoodsReceiptDetailId = l.GoodsReceiptDetailId,
                        LineNumber = l.LineNumber,
                        PurchaseOrderDetailId = l.PurchaseOrderDetailId,
                        ItemId = l.ItemId,
                        Description = l.Description,
                        HsnSacCode = l.HsnSacCode,
                        WarehouseId = l.WarehouseId,
                        Quantity = l.Quantity,
                        AcceptedQuantity = l.AcceptedQuantity,
                        RejectedQuantity = l.RejectedQuantity,
                        RejectionReason = l.RejectionReason,
                        UomId = l.UomId,
                        ConversionFactor = l.ConversionFactor,
                        BaseQuantity = l.BaseQuantity,
                        UnitPrice = l.UnitPrice,
                        IsPriceInclusive = l.IsPriceInclusive,
                        DiscountPercent = l.DiscountPercent,
                        DiscountAmount = l.DiscountAmount,
                        GrossAmount = l.GrossAmount,
                        TaxableAmount = l.TaxableAmount,
                        TaxTreatment = l.TaxTreatment.ToString(),
                        TaxMasterId = l.TaxMasterId,
                        TaxGroupId = l.TaxGroupId,
                        TaxAmount = l.TaxAmount,
                        LineType = l.LineType.ToString(),
                        AccountId = l.AccountId,
                        FixedAssetCategoryId = l.FixedAssetCategoryId,
                        LineTotal = l.LineTotal,
                        ItemBatchId = l.ItemBatchId,
                        LineNotes = l.LineNotes,
                        Taxes = l.Taxes
                            .Select(t => new GoodsReceiptLineTaxView
                            {
                                GoodsReceiptDetailTaxId = t.GoodsReceiptDetailTaxId,
                                TaxComponent = t.TaxComponent.ToString(),
                                SubAccountId = t.SubAccountId,
                                Rate = t.Rate,
                                TaxableAmount = t.TaxableAmount,
                                Amount = t.Amount,
                                AmountBase = t.AmountBase,
                            })
                            .ToList(),
                    })
                    .ToList(),
            })
            .FirstOrDefaultAsync(ct);

        if (receipt is null)
        {
            return null;
        }

        await ResolveContactNamesAsync([receipt], ct);

        List<long> itemIds = receipt.Lines
            .Where(l => l.ItemId.HasValue)
            .Select(l => l.ItemId!.Value)
            .Distinct()
            .ToList();

        if (itemIds.Count > 0)
        {
            IReadOnlyDictionary<long, NamedRef> items = await _itemNames.ResolveAsync(itemIds, ct);
            foreach (GoodsReceiptLineView line in receipt.Lines)
            {
                if (line.ItemId.HasValue && items.TryGetValue(line.ItemId.Value, out NamedRef? item))
                {
                    line.ItemLabel = $"{item.Code} - {item.Name}";
                }
            }
        }

        return receipt;
    }

    public async Task<List<GoodsReceiptListItem>> ListAsync(CancellationToken ct)
    {
        List<GoodsReceiptListItem> receipts = await _db.GoodsReceipts
            .OrderByDescending(r => r.DocumentDate)
            .ThenByDescending(r => r.DocumentNo)
            .Select(r => new GoodsReceiptListItem
            {
                GoodsReceiptId = r.GoodsReceiptId,
                DocumentNo = r.DocumentNo,
                DocumentDate = r.DocumentDate,
                PurchaseOrderId = r.PurchaseOrderId,
                PurchaseOrderNo = _db.PurchaseOrders
                    .Where(o => o.PurchaseOrderId == r.PurchaseOrderId)
                    .Select(o => o.DocumentNo)
                    .FirstOrDefault(),
                VendorDeliveryNoteNo = r.VendorDeliveryNoteNo,
                ContactId = r.ContactId,
                CurrencyCode = r.CurrencyCode,
                TaxableAmount = r.TaxableAmount,
                TotalAmount = r.TotalAmount,
                Status = r.Status.ToString(),
                IsInterState = r.IsInterState,
                RejectedQuantity = r.Lines.Sum(l => l.RejectedQuantity),
            })
            .ToListAsync(ct);

        await ResolveContactNamesAsync(receipts, ct);
        return receipts;
    }

    /// <summary>One call for the whole page — the names are not stored on the row.</summary>
    private async Task ResolveContactNamesAsync(
        IReadOnlyCollection<GoodsReceiptListItem> receipts, CancellationToken ct)
    {
        List<long> contactIds = receipts.Select(r => r.ContactId).Distinct().ToList();
        if (contactIds.Count == 0)
        {
            return;
        }

        IReadOnlyDictionary<long, NamedRef> contacts =
            await _contactNames.ResolveAsync(contactIds, ct);

        foreach (GoodsReceiptListItem receipt in receipts)
        {
            if (contacts.TryGetValue(receipt.ContactId, out NamedRef? contact))
            {
                receipt.ContactName = contact.Name;
                receipt.ContactCode = contact.Code;
            }
        }
    }
}
