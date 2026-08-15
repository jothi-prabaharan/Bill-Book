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
/// The purchase order — <c>POR</c>.
///
/// <b>It posts nothing and it reserves nothing</b>, which is what makes it the
/// right first screen for this module: no ledger call, no Inventory call, no
/// cost layer. What it does exercise is everything the later documents need —
/// tax determination, numbering, the lifecycle, the batched name lookups — so
/// the machinery is proved somewhere a mistake cannot reach the books.
///
/// The consequence for this class is that there is no <c>IInventoryClient</c>
/// and no <c>ILedgerClient</c> in it. A sales order has both; ordering from a
/// vendor has nothing to hold back and nothing to record.
/// </summary>
public sealed class PurchaseOrderService
{
    private readonly PurchaseDbContext _db;
    private readonly INumberGenerator _numbering;
    private readonly IBaseCurrencyProvider _baseCurrency;
    private readonly IBranchSettingsProvider _branchSettings;
    private readonly ITaxRateProvider _rates;
    private readonly IContactNameLookup _contactNames;
    private readonly IItemNameLookup _itemNames;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public PurchaseOrderService(
        PurchaseDbContext db,
        INumberGenerator numbering,
        IBaseCurrencyProvider baseCurrency,
        IBranchSettingsProvider branchSettings,
        ITaxRateProvider rates,
        IContactNameLookup contactNames,
        IItemNameLookup itemNames,
        ICurrentUser user,
        TimeProvider clock)
    {
        _db = db;
        _numbering = numbering;
        _baseCurrency = baseCurrency;
        _branchSettings = branchSettings;
        _rates = rates;
        _contactNames = contactNames;
        _itemNames = itemNames;
        _user = user;
        _clock = clock;
    }

    public async Task<PurchaseOrderResult> CreateAsync(
        SavePurchaseOrderRequest request, CancellationToken ct)
    {
        string? baseCurrency = await _baseCurrency.GetBaseCurrencyAsync(ct);
        if (baseCurrency is null)
        {
            return new PurchaseOrderResult(
                PurchaseOrderOutcome.RatesUnavailable,
                Detail: "Branch base currency could not be read.");
        }

        PurchaseOrder order = new()
        {
            TransactionTypeCode = "POR",
            DocumentDate = request.DocumentDate,
            CurrencyCode = request.CurrencyCode ?? baseCurrency,
            ExchangeRate = request.ExchangeRate ?? 1m,
            Status = DocumentStatus.Draft,
            FulfilmentStatus = PurchaseFulfilmentStatus.Open,
        };

        PurchaseOrderResult applied = await ApplyAsync(order, request, ct);
        if (applied.Outcome != PurchaseOrderOutcome.Ok)
        {
            return applied;
        }

        // Allocated last, so a request refused above never spends a number. The
        // generator joins this request's transaction, so a failed insert gives
        // the number back and the series stays gapless.
        NumberAllocation alloc = await _numbering.NextAsync("POR", request.DocumentDate, ct);
        order.DocumentNo = alloc.Code;

        _db.PurchaseOrders.Add(order);
        await _db.SaveChangesAsync(ct);

        return new PurchaseOrderResult(PurchaseOrderOutcome.Ok, order.PurchaseOrderId);
    }

    public async Task<PurchaseOrderResult> UpdateAsync(
        long purchaseOrderId, SavePurchaseOrderRequest request, CancellationToken ct)
    {
        PurchaseOrder? order = await _db.PurchaseOrders
            .Include(o => o.Lines)
            .ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(o => o.PurchaseOrderId == purchaseOrderId, ct);

        if (order is null)
        {
            return new PurchaseOrderResult(PurchaseOrderOutcome.NotFound);
        }

        DocumentTransition transition = DocumentLifecycle.CanEdit(order.Status);
        if (!transition.IsAllowed)
        {
            return new PurchaseOrderResult(
                PurchaseOrderOutcome.LifecycleRefused, Detail: transition.Detail);
        }

        if (request.CurrencyCode is not null)
        {
            order.CurrencyCode = request.CurrencyCode;
        }

        if (request.ExchangeRate.HasValue)
        {
            order.ExchangeRate = request.ExchangeRate.Value;
        }

        order.DocumentDate = request.DocumentDate;

        // Lines are replaced wholesale rather than diffed. The tax rows hang off
        // them and are recomputed anyway, so matching old to new would be work
        // done to arrive at the same rows.
        _db.PurchaseOrderDetailTaxes.RemoveRange(order.Lines.SelectMany(l => l.Taxes));
        _db.PurchaseOrderDetails.RemoveRange(order.Lines);
        order.Lines.Clear();

        PurchaseOrderResult applied = await ApplyAsync(order, request, ct);
        if (applied.Outcome != PurchaseOrderOutcome.Ok)
        {
            return applied;
        }

        await _db.SaveChangesAsync(ct);
        return new PurchaseOrderResult(PurchaseOrderOutcome.Ok, order.PurchaseOrderId);
    }

    /// <summary>
    /// Everything create and update do identically: validate the header, rebuild
    /// every line through the shared calculator, and foot the document.
    ///
    /// <b>One copy, called twice.</b> The equivalent on the sales side is written
    /// out in full in both methods — about a hundred lines each — and the two have
    /// already drifted: only one of them re-resolves the place of supply. Two
    /// copies of the arithmetic that decides a GST return is one copy that gets
    /// corrected and one that does not.
    /// </summary>
    private async Task<PurchaseOrderResult> ApplyAsync(
        PurchaseOrder order, SavePurchaseOrderRequest request, CancellationToken ct)
    {
        if (request.ExpectedDate is DateOnly expected && expected < request.DocumentDate)
        {
            return new PurchaseOrderResult(
                PurchaseOrderOutcome.ExpectedDateInvalid,
                Detail: "The expected delivery date cannot fall before the order date.");
        }

        BranchSettings? settings = await _branchSettings.GetSettingsAsync(ct);
        if (settings is null)
        {
            return new PurchaseOrderResult(
                PurchaseOrderOutcome.RatesUnavailable,
                Detail: "Branch settings could not be read.");
        }

        PlaceOfSupplyResult pos = PlaceOfSupply.Resolve(
            settings.StateCode, request.PlaceOfSupplyStateCode, request.ContactGstin);

        if (!pos.IsOk)
        {
            return new PurchaseOrderResult(
                PurchaseOrderOutcome.PlaceOfSupplyRefused, Detail: pos.Detail);
        }

        order.ExpectedDate = request.ExpectedDate;
        order.ContactId = request.ContactId;
        order.ContactGstin = request.ContactGstin;
        order.BillingAddress = request.BillingAddress;
        order.ShippingAddress = request.ShippingAddress;
        order.IsInterState = pos.IsInterState;
        order.Notes = request.Notes;
        order.TermsAndConditions = request.TermsAndConditions;

        TaxContext taxContext = new(pos.IsInterState, settings.DiscountBeforeTax);
        List<TaxLineResult> taxLines = new(request.Lines.Count);

        for (int i = 0; i < request.Lines.Count; i++)
        {
            SavePurchaseOrderLineRequest lineReq = request.Lines[i];
            int lineNumber = i + 1;

            if (lineReq.ItemId is null && string.IsNullOrWhiteSpace(lineReq.Description))
            {
                return new PurchaseOrderResult(
                    PurchaseOrderOutcome.LineInvalid,
                    Detail: $"Line {lineNumber} has no item, so it needs a description.");
            }

            if (lineReq.ItemId is null && lineReq.AccountId is null)
            {
                return new PurchaseOrderResult(
                    PurchaseOrderOutcome.LineInvalid,
                    Detail: $"Line {lineNumber} has no item, so it needs an account to post to.");
            }

            // The three line kinds, checked here as well as by the database. All
            // three are genuinely used on a purchase — see Purchase.md §4 — and
            // the check constraint would refuse these anyway; catching them here
            // is what turns a 500 into a message naming the line.
            if (lineReq.LineType == DocumentLineType.Stock && lineReq.ItemId is null)
            {
                return new PurchaseOrderResult(
                    PurchaseOrderOutcome.LineInvalid,
                    Detail: $"Line {lineNumber} is a stock line, so it needs an item.");
            }

            if (lineReq.LineType == DocumentLineType.Expense && lineReq.AccountId is null)
            {
                return new PurchaseOrderResult(
                    PurchaseOrderOutcome.LineInvalid,
                    Detail: $"Line {lineNumber} is an expense line, so it needs an account.");
            }

            if (lineReq.LineType == DocumentLineType.Capital
                && lineReq.FixedAssetCategoryId is null)
            {
                return new PurchaseOrderResult(
                    PurchaseOrderOutcome.LineInvalid,
                    Detail: $"Line {lineNumber} is a capital line, so it needs a fixed asset "
                        + "category — the category owns the accounts, not the asset.");
            }

            TaxRate? rate = null;
            if (lineReq.TaxGroupId is long taxGroupId)
            {
                rate = await _rates.GetRateAsync(taxGroupId, request.DocumentDate, ct);
                if (rate is null)
                {
                    return new PurchaseOrderResult(
                        PurchaseOrderOutcome.RatesUnavailable,
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

            PurchaseOrderDetail detail = new()
            {
                LineNumber = lineNumber,
                ItemId = lineReq.ItemId,
                Description = lineReq.Description,
                HsnSacCode = lineReq.HsnSacCode,
                WarehouseId = lineReq.WarehouseId,
                Quantity = lineReq.Quantity,
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

                // Nothing has arrived and nothing has been billed. Both stay zero
                // until a goods receipt and a bill say otherwise.
                ReceivedQuantity = 0m,
                BilledQuantity = 0m,
            };

            foreach (TaxComponentAmount component in computed.Components)
            {
                detail.Taxes.Add(new PurchaseOrderDetailTax
                {
                    TaxComponent = component.Component,
                    Rate = component.Rate,
                    TaxableAmount = component.TaxableAmount,
                    Amount = component.Amount,
                    AmountBase = component.Amount * order.ExchangeRate,
                });
            }

            order.Lines.Add(detail);
        }

        TaxDocumentTotals totals = GstCalculator.Totals(taxLines);

        order.SubTotal = totals.SubTotal;
        order.DiscountAmount = totals.DiscountAmount;
        order.TaxableAmount = totals.TaxableAmount;
        order.CgstAmount = totals.CgstAmount;
        order.SgstAmount = totals.SgstAmount;
        order.IgstAmount = totals.IgstAmount;
        order.CessAmount = totals.CessAmount;
        order.RoundOffAmount =
            Math.Round(totals.TotalAmount, 0, MidpointRounding.AwayFromZero) - totals.TotalAmount;
        order.TotalAmount = totals.TotalAmount + order.RoundOffAmount;
        order.TotalAmountBase = order.TotalAmount * order.ExchangeRate;

        return new PurchaseOrderResult(PurchaseOrderOutcome.Ok, order.PurchaseOrderId);
    }

    /// <summary>
    /// Draft → ReadyToPost. The review step <c>purchase.approve</c> was seeded
    /// for and nothing ever read.
    /// </summary>
    public async Task<PurchaseOrderResult> ApproveAsync(long purchaseOrderId, CancellationToken ct)
    {
        PurchaseOrder? order = await _db.PurchaseOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.PurchaseOrderId == purchaseOrderId, ct);

        if (order is null)
        {
            return new PurchaseOrderResult(PurchaseOrderOutcome.NotFound);
        }

        DocumentTransition transition =
            DocumentLifecycle.CanApprove(order.Status, order.Lines.Count);

        if (!transition.IsAllowed)
        {
            return new PurchaseOrderResult(
                PurchaseOrderOutcome.LifecycleRefused, Detail: transition.Detail);
        }

        order.Status = DocumentStatus.ReadyToPost;
        await _db.SaveChangesAsync(ct);

        return new PurchaseOrderResult(PurchaseOrderOutcome.Ok, order.PurchaseOrderId);
    }

    /// <summary>
    /// Draft or ReadyToPost → Posted: the order is committed and issued to the
    /// vendor.
    ///
    /// <b>"Posted" here means issued, not posted to the ledger.</b> A purchase
    /// order reaches no account — <c>mst.TransactionTypes</c> has <c>POR</c> with
    /// <c>IsLedgerPosting = false</c> — so this writes no journal and calls no
    /// other service. What the status buys is that the document is frozen and a
    /// goods receipt can now be raised against it.
    /// </summary>
    public async Task<PurchaseOrderResult> ConfirmAsync(long purchaseOrderId, CancellationToken ct)
    {
        PurchaseOrder? order = await _db.PurchaseOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.PurchaseOrderId == purchaseOrderId, ct);

        if (order is null)
        {
            return new PurchaseOrderResult(PurchaseOrderOutcome.NotFound);
        }

        DocumentTransition transition = DocumentLifecycle.CanPost(order.Status, order.Lines.Count);
        if (!transition.IsAllowed)
        {
            return new PurchaseOrderResult(
                PurchaseOrderOutcome.LifecycleRefused, Detail: transition.Detail);
        }

        order.Status = DocumentStatus.Posted;
        order.PostedAt = _clock.GetUtcNow();
        order.PostedBy = _user.UserId;

        await _db.SaveChangesAsync(ct);
        return new PurchaseOrderResult(PurchaseOrderOutcome.Ok, order.PurchaseOrderId);
    }

    public async Task<PurchaseOrderResult> VoidAsync(
        long purchaseOrderId, VoidPurchaseOrderRequest request, CancellationToken ct)
    {
        PurchaseOrder? order = await _db.PurchaseOrders
            .FirstOrDefaultAsync(o => o.PurchaseOrderId == purchaseOrderId, ct);

        if (order is null)
        {
            return new PurchaseOrderResult(PurchaseOrderOutcome.NotFound);
        }

        // A receipt or a bill pointing here is what makes this order un-voidable.
        // Checked against both tables rather than the fulfilment status, because
        // the status is a summary and this is the fact it summarises.
        bool hasDownstream =
            await _db.GoodsReceipts.AnyAsync(g => g.PurchaseOrderId == purchaseOrderId, ct)
            || await _db.Bills.AnyAsync(b => b.PurchaseOrderId == purchaseOrderId, ct);

        DocumentTransition transition =
            DocumentLifecycle.CanVoid(order.Status, hasDownstream, request.Reason);

        if (!transition.IsAllowed)
        {
            return transition.Outcome == DocumentTransitionOutcome.HasDownstream
                ? new PurchaseOrderResult(
                    PurchaseOrderOutcome.AlreadyReceived, Detail: transition.Detail)
                : new PurchaseOrderResult(
                    PurchaseOrderOutcome.LifecycleRefused, Detail: transition.Detail);
        }

        order.Status = DocumentStatus.Void;
        order.VoidedAt = _clock.GetUtcNow();
        order.VoidedBy = _user.UserId;
        order.VoidReason = request.Reason;
        order.FulfilmentStatus = PurchaseFulfilmentStatus.Cancelled;

        await _db.SaveChangesAsync(ct);
        return new PurchaseOrderResult(PurchaseOrderOutcome.Ok, order.PurchaseOrderId);
    }

    public async Task<PurchaseOrderView?> GetAsync(long purchaseOrderId, CancellationToken ct)
    {
        PurchaseOrderView? order = await _db.PurchaseOrders
            .Where(o => o.PurchaseOrderId == purchaseOrderId)
            .Select(o => new PurchaseOrderView
            {
                PurchaseOrderId = o.PurchaseOrderId,
                DocumentNo = o.DocumentNo,
                DocumentDate = o.DocumentDate,
                ExpectedDate = o.ExpectedDate,
                FulfilmentStatus = o.FulfilmentStatus.ToString(),
                ContactId = o.ContactId,
                CurrencyCode = o.CurrencyCode,
                TaxableAmount = o.TaxableAmount,
                TotalAmount = o.TotalAmount,
                Status = o.Status.ToString(),
                IsInterState = o.IsInterState,
                ReceiptCount = _db.GoodsReceipts.Count(g => g.PurchaseOrderId == o.PurchaseOrderId),
                ContactGstin = o.ContactGstin,
                PlaceOfSupplyStateId = o.PlaceOfSupplyStateId,
                BillingAddress = o.BillingAddress,
                ShippingAddress = o.ShippingAddress,
                ExchangeRate = o.ExchangeRate,
                SubTotal = o.SubTotal,
                DiscountAmount = o.DiscountAmount,
                CgstAmount = o.CgstAmount,
                SgstAmount = o.SgstAmount,
                IgstAmount = o.IgstAmount,
                CessAmount = o.CessAmount,
                RoundOffAmount = o.RoundOffAmount,
                TotalAmountBase = o.TotalAmountBase,
                Notes = o.Notes,
                TermsAndConditions = o.TermsAndConditions,
                PostedAt = o.PostedAt,
                VoidedAt = o.VoidedAt,
                VoidReason = o.VoidReason,
                Lines = o.Lines
                    .OrderBy(l => l.LineNumber)
                    .Select(l => new PurchaseOrderLineView
                    {
                        PurchaseOrderDetailId = l.PurchaseOrderDetailId,
                        LineNumber = l.LineNumber,
                        ItemId = l.ItemId,
                        Description = l.Description,
                        HsnSacCode = l.HsnSacCode,
                        WarehouseId = l.WarehouseId,
                        Quantity = l.Quantity,
                        UomId = l.UomId,
                        ConversionFactor = l.ConversionFactor,
                        BaseQuantity = l.BaseQuantity,
                        ReceivedQuantity = l.ReceivedQuantity,
                        BilledQuantity = l.BilledQuantity,
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
                            .Select(t => new PurchaseOrderLineTaxView
                            {
                                PurchaseOrderDetailTaxId = t.PurchaseOrderDetailTaxId,
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

        if (order is null)
        {
            return null;
        }

        await ResolveContactNamesAsync([order], ct);

        List<long> itemIds = order.Lines
            .Where(l => l.ItemId.HasValue)
            .Select(l => l.ItemId!.Value)
            .Distinct()
            .ToList();

        if (itemIds.Count > 0)
        {
            IReadOnlyDictionary<long, NamedRef> items = await _itemNames.ResolveAsync(itemIds, ct);
            foreach (PurchaseOrderLineView line in order.Lines)
            {
                if (line.ItemId.HasValue && items.TryGetValue(line.ItemId.Value, out NamedRef? item))
                {
                    line.ItemLabel = $"{item.Code} - {item.Name}";
                }
            }
        }

        return order;
    }

    public async Task<List<PurchaseOrderListItem>> ListAsync(CancellationToken ct)
    {
        List<PurchaseOrderListItem> orders = await _db.PurchaseOrders
            .OrderByDescending(o => o.DocumentDate)
            .ThenByDescending(o => o.DocumentNo)
            .Select(o => new PurchaseOrderListItem
            {
                PurchaseOrderId = o.PurchaseOrderId,
                DocumentNo = o.DocumentNo,
                DocumentDate = o.DocumentDate,
                ExpectedDate = o.ExpectedDate,
                FulfilmentStatus = o.FulfilmentStatus.ToString(),
                ContactId = o.ContactId,
                CurrencyCode = o.CurrencyCode,
                TaxableAmount = o.TaxableAmount,
                TotalAmount = o.TotalAmount,
                Status = o.Status.ToString(),
                IsInterState = o.IsInterState,
                ReceiptCount = _db.GoodsReceipts.Count(g => g.PurchaseOrderId == o.PurchaseOrderId),
            })
            .ToListAsync(ct);

        await ResolveContactNamesAsync(orders, ct);
        return orders;
    }

    /// <summary>
    /// Fills in the vendor names the documents deliberately do not store, in one
    /// call for the whole page.
    ///
    /// The name is absent from the row so a corrected spelling shows everywhere,
    /// including on orders already raised. That only stays affordable batched —
    /// per row it is the N+1 CLAUDE.md warns about on list screens.
    /// </summary>
    private async Task ResolveContactNamesAsync(
        IReadOnlyCollection<PurchaseOrderListItem> orders, CancellationToken ct)
    {
        List<long> contactIds = orders.Select(o => o.ContactId).Distinct().ToList();
        if (contactIds.Count == 0)
        {
            return;
        }

        IReadOnlyDictionary<long, NamedRef> contacts =
            await _contactNames.ResolveAsync(contactIds, ct);

        foreach (PurchaseOrderListItem order in orders)
        {
            if (contacts.TryGetValue(order.ContactId, out NamedRef? contact))
            {
                order.ContactName = contact.Name;
                order.ContactCode = contact.Code;
            }
        }
    }
}
