using Microsoft.EntityFrameworkCore;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tax;
using Shared.Kernel.Tenancy;
using Shared.Kernel.Interfaces;

namespace Sales.Api.Services;

public sealed class SalesOrderService
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
    private readonly ICreditCheckClient _creditCheckClient;

    public SalesOrderService(
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
        ICreditCheckClient creditCheckClient)
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
        _creditCheckClient = creditCheckClient;
    }

    public async Task<SalesOrderResult> CreateAsync(SaveSalesOrderRequest request, CancellationToken ct)
    {
        string? baseCurrency = await _baseCurrency.GetBaseCurrencyAsync(ct);
        if (baseCurrency is null)
        {
            return new SalesOrderResult(SalesOrderOutcome.RatesUnavailable, Detail: "Branch base currency could not be read.");
        }

        BranchSettings? settings = await _branchSettings.GetSettingsAsync(ct);
        if (settings is null)
        {
            return new SalesOrderResult(SalesOrderOutcome.RatesUnavailable, Detail: "Branch settings could not be read.");
        }



        PlaceOfSupplyResult pos = PlaceOfSupply.Resolve(
            settings.StateCode, request.PlaceOfSupplyStateCode, request.ContactGstin);

        if (!pos.IsOk)
        {
            return new SalesOrderResult(SalesOrderOutcome.PlaceOfSupplyRefused, Detail: pos.Detail);
        }

        TaxContext taxContext = new(pos.IsInterState, settings.DiscountBeforeTax);

        NumberAllocation alloc = await _numbering.NextAsync("SOR", request.DocumentDate, ct);

        SalesOrder SalesOrder = new()
        {
            TransactionTypeCode = "SOR",
            DocumentNo = alloc.Code,
            DocumentDate = request.DocumentDate,
            DeliveryDate = request.DeliveryDate,
            QuoteId = request.QuoteId,
            ContactId = request.ContactId,
            ContactGstin = request.ContactGstin,
            BillingAddress = request.BillingAddress,
            ShippingAddress = request.ShippingAddress,
            PlaceOfSupplyStateId = 0, // Master data missing PlaceOfSupplyStateId lookup for now; using 0 since it's unenforced
            IsInterState = pos.IsInterState,
            CurrencyCode = request.CurrencyCode ?? baseCurrency,
            ExchangeRate = request.ExchangeRate ?? 1m,
            Notes = request.Notes,
            TermsAndConditions = request.TermsAndConditions,
        };

        var taxLines = new List<TaxLineResult>(request.Lines.Count);

        for (int i = 0; i < request.Lines.Count; i++)
        {
            SaveSalesOrderLineRequest lineReq = request.Lines[i];

            if (lineReq.ItemId is null && string.IsNullOrWhiteSpace(lineReq.Description))
            {
                return new SalesOrderResult(SalesOrderOutcome.LineInvalid, Detail: $"Line {i + 1} is a free-text line and must have a description.");
            }
            if (lineReq.ItemId is null && lineReq.AccountId is null)
            {
                return new SalesOrderResult(SalesOrderOutcome.LineInvalid, Detail: $"Line {i + 1} is a free-text line and must have an account selected.");
            }

            TaxRate? rate = null;
            if (lineReq.TaxGroupId is long taxGroupId)
            {
                rate = await _rates.GetRateAsync(taxGroupId, request.DocumentDate, ct);
                if (rate is null)
                {
                    return new SalesOrderResult(SalesOrderOutcome.RatesUnavailable, Detail: $"Tax rate for group {taxGroupId} could not be read for date {request.DocumentDate}.");
                }
            }

            TaxLineInput taxInput = new()
            {
                Quantity = lineReq.Quantity,
                UnitPrice = lineReq.UnitPrice,
                DiscountPercent = lineReq.DiscountPercent,
                DiscountAmount = lineReq.DiscountAmount,
                IsPriceInclusive = lineReq.IsPriceInclusive,
                TaxTreatment = lineReq.TaxTreatment,
                Rate = rate,
                ConversionFactor = lineReq.ConversionFactor,
            };

            TaxLineResult computed = GstCalculator.Compute(taxInput, taxContext);
            taxLines.Add(computed);

            SalesOrderDetail detail = new()
            {
                LineNumber = i + 1,
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
            };

            foreach (var comp in computed.Components)
            {
                detail.Taxes.Add(new SalesOrderDetailTax
                {
                    TaxComponent = comp.Component,
                    Rate = comp.Rate,
                    TaxableAmount = comp.TaxableAmount,
                    Amount = comp.Amount,
                    AmountBase = comp.Amount * SalesOrder.ExchangeRate,
                });
            }

            SalesOrder.Lines.Add(detail);
        }

        TaxDocumentTotals totals = GstCalculator.Totals(taxLines);

        SalesOrder.SubTotal = totals.SubTotal;
        SalesOrder.DiscountAmount = totals.DiscountAmount;
        SalesOrder.TaxableAmount = totals.TaxableAmount;
        SalesOrder.CgstAmount = totals.CgstAmount;
        SalesOrder.SgstAmount = totals.SgstAmount;
        SalesOrder.IgstAmount = totals.IgstAmount;
        SalesOrder.CessAmount = totals.CessAmount;
        SalesOrder.RoundOffAmount = Math.Round(totals.TotalAmount, 0, MidpointRounding.AwayFromZero) - totals.TotalAmount;
        SalesOrder.TotalAmount = totals.TotalAmount + SalesOrder.RoundOffAmount;
        SalesOrder.TotalAmountBase = SalesOrder.TotalAmount * SalesOrder.ExchangeRate;

        var eval = await _creditCheckClient.EvaluateAsync(SalesOrder.ContactId, SalesOrder.TotalAmountBase, ct);
        if (!eval.Allowed)
        {
            return new SalesOrderResult(SalesOrderOutcome.CreditLimitExceeded, Detail: eval.Reason);
        }

        // A new order is a draft and nothing more. It used to be stamped
        // PostedAt/PostedBy here as well, which is what tells a voided order that
        // was live apart from one that was abandoned as a draft — so every
        // abandoned draft read as an order that had once been confirmed.
        SalesOrder.Status = DocumentStatus.Draft;
        SalesOrder.FulfilmentStatus = FulfilmentStatus.Open;

        _db.SalesOrders.Add(SalesOrder);
        await _db.SaveChangesAsync(ct);

        return new SalesOrderResult(SalesOrderOutcome.Ok, SalesOrder.SalesOrderId);
    }

    public async Task<SalesOrderResult> UpdateAsync(long SalesOrderId, SaveSalesOrderRequest request, CancellationToken ct)
    {
        SalesOrder? SalesOrder = await _db.SalesOrders
            .Include(q => q.Lines)
            .ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(q => q.SalesOrderId == SalesOrderId, ct);

        if (SalesOrder is null)
        {
            return new SalesOrderResult(SalesOrderOutcome.NotFound);
        }

        if (!BelongsToCaller(SalesOrder))
        {
            return new SalesOrderResult(SalesOrderOutcome.Forbidden);
        }

        // The lifecycle answers this for every document in the product, so a
        // sales order does not get its own opinion about whether ReadyToPost is
        // still editable.
        DocumentTransition editable = DocumentLifecycle.CanEdit(SalesOrder.Status);
        if (!editable.IsAllowed)
        {
            return new SalesOrderResult(SalesOrderOutcome.LifecycleRefused, Detail: editable.Detail);
        }

        BranchSettings? settings = await _branchSettings.GetSettingsAsync(ct);
        if (settings is null)
        {
            return new SalesOrderResult(SalesOrderOutcome.RatesUnavailable, Detail: "Branch settings could not be read.");
        }



        PlaceOfSupplyResult pos = PlaceOfSupply.Resolve(
            settings.StateCode, request.PlaceOfSupplyStateCode, request.ContactGstin);

        if (!pos.IsOk)
        {
            return new SalesOrderResult(SalesOrderOutcome.PlaceOfSupplyRefused, Detail: pos.Detail);
        }

        TaxContext taxContext = new(pos.IsInterState, settings.DiscountBeforeTax);

        SalesOrder.DocumentDate = request.DocumentDate;
        SalesOrder.DeliveryDate = request.DeliveryDate;
        SalesOrder.ContactId = request.ContactId;
        SalesOrder.ContactGstin = request.ContactGstin;
        SalesOrder.BillingAddress = request.BillingAddress;
        SalesOrder.ShippingAddress = request.ShippingAddress;
        SalesOrder.IsInterState = pos.IsInterState;
        
        if (request.CurrencyCode != null)
        {
            SalesOrder.CurrencyCode = request.CurrencyCode;
        }
        if (request.ExchangeRate.HasValue)
        {
            SalesOrder.ExchangeRate = request.ExchangeRate.Value;
        }

        SalesOrder.Notes = request.Notes;
        SalesOrder.TermsAndConditions = request.TermsAndConditions;

        var taxLines = new List<TaxLineResult>(request.Lines.Count);
        SalesOrder.Lines.Clear();

        for (int i = 0; i < request.Lines.Count; i++)
        {
            SaveSalesOrderLineRequest lineReq = request.Lines[i];

            if (lineReq.ItemId is null && string.IsNullOrWhiteSpace(lineReq.Description))
            {
                return new SalesOrderResult(SalesOrderOutcome.LineInvalid, Detail: $"Line {i + 1} is a free-text line and must have a description.");
            }
            if (lineReq.ItemId is null && lineReq.AccountId is null)
            {
                return new SalesOrderResult(SalesOrderOutcome.LineInvalid, Detail: $"Line {i + 1} is a free-text line and must have an account selected.");
            }

            TaxRate? rate = null;
            if (lineReq.TaxGroupId is long taxGroupId)
            {
                rate = await _rates.GetRateAsync(taxGroupId, request.DocumentDate, ct);
                if (rate is null)
                {
                    return new SalesOrderResult(SalesOrderOutcome.RatesUnavailable, Detail: $"Tax rate for group {taxGroupId} could not be read for date {request.DocumentDate}.");
                }
            }

            TaxLineInput taxInput = new()
            {
                Quantity = lineReq.Quantity,
                UnitPrice = lineReq.UnitPrice,
                DiscountPercent = lineReq.DiscountPercent,
                DiscountAmount = lineReq.DiscountAmount,
                IsPriceInclusive = lineReq.IsPriceInclusive,
                TaxTreatment = lineReq.TaxTreatment,
                Rate = rate,
                ConversionFactor = lineReq.ConversionFactor,
            };

            TaxLineResult computed = GstCalculator.Compute(taxInput, taxContext);
            taxLines.Add(computed);

            SalesOrderDetail detail = new()
            {
                LineNumber = i + 1,
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
            };

            foreach (var comp in computed.Components)
            {
                detail.Taxes.Add(new SalesOrderDetailTax
                {
                    TaxComponent = comp.Component,
                    Rate = comp.Rate,
                    TaxableAmount = comp.TaxableAmount,
                    Amount = comp.Amount,
                    AmountBase = comp.Amount * SalesOrder.ExchangeRate,
                });
            }

            SalesOrder.Lines.Add(detail);
        }

        TaxDocumentTotals totals = GstCalculator.Totals(taxLines);

        SalesOrder.SubTotal = totals.SubTotal;
        SalesOrder.DiscountAmount = totals.DiscountAmount;
        SalesOrder.TaxableAmount = totals.TaxableAmount;
        SalesOrder.CgstAmount = totals.CgstAmount;
        SalesOrder.SgstAmount = totals.SgstAmount;
        SalesOrder.IgstAmount = totals.IgstAmount;
        SalesOrder.CessAmount = totals.CessAmount;
        SalesOrder.RoundOffAmount = Math.Round(totals.TotalAmount, 0, MidpointRounding.AwayFromZero) - totals.TotalAmount;
        SalesOrder.TotalAmount = totals.TotalAmount + SalesOrder.RoundOffAmount;
        SalesOrder.TotalAmountBase = SalesOrder.TotalAmount * SalesOrder.ExchangeRate;

        var eval = await _creditCheckClient.EvaluateAsync(SalesOrder.ContactId, SalesOrder.TotalAmountBase, ct);
        if (!eval.Allowed)
        {
            return new SalesOrderResult(SalesOrderOutcome.CreditLimitExceeded, Detail: eval.Reason);
        }

        await _db.SaveChangesAsync(ct);

        return new SalesOrderResult(SalesOrderOutcome.Ok, SalesOrder.SalesOrderId);
    }

    public async Task<SalesOrderResult> VoidAsync(long SalesOrderId, VoidSalesOrderRequest request, CancellationToken ct)
    {
        // Loaded with its lines, not by key: the reservation released below is
        // built from them, and FindAsync leaves the collection empty — so this
        // used to release nothing at all and quietly report success.
        SalesOrder? SalesOrder = await _db.SalesOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.SalesOrderId == SalesOrderId, ct);

        if (SalesOrder is null)
        {
            return new SalesOrderResult(SalesOrderOutcome.NotFound);
        }

        if (!BelongsToCaller(SalesOrder))
        {
            return new SalesOrderResult(SalesOrderOutcome.Forbidden);
        }

        // What points *at* this order — an invoice raised from it or a challan
        // delivered against it. It previously asked whether the order itself
        // existed, which it always does by this line, so every void was refused
        // as having downstream documents and no sales order could be withdrawn.
        bool hasDownstream = await _db.Invoices.AnyAsync(i => i.SalesOrderId == SalesOrderId, ct)
            || await _db.DeliveryChallans.AnyAsync(d => d.SalesOrderId == SalesOrderId, ct);

        DocumentTransition transition = DocumentLifecycle.CanVoid(SalesOrder.Status, hasDownstream, request.Reason);
        if (!transition.IsAllowed)
        {
            return new SalesOrderResult(SalesOrderOutcome.LifecycleRefused, Detail: transition.Detail);
        }

        // Only a confirmed order holds a reservation. Releasing against a draft
        // would hand back stock that was never taken, which reads as a windfall
        // on the next availability check.
        if (SalesOrder.Status == DocumentStatus.Posted)
        {
            var releaseReq = new ReleaseStockRequest
            {
                OrgId = _tenant.OrgId.GetValueOrDefault(),
                CustomerId = _tenant.CustomerId.GetValueOrDefault(),
                Lines = SalesOrder.Lines.Where(l => l.LineType == DocumentLineType.Stock && l.ItemId.HasValue)
                    .Select(l => new ReleaseStockLine { ItemId = l.ItemId!.Value, Quantity = l.Quantity })
                    .ToList()
            };

            if (releaseReq.Lines.Count > 0)
            {
                ReleaseStockResponse releaseRes = await _inventoryClient.ReleaseAsync(releaseReq, ct);
                if (!releaseRes.Success)
                {
                    // The void is refused rather than recorded, because a voided
                    // order whose reservation is still held is stock nobody can
                    // sell and no document explains. Ask again; it is idempotent.
                    return new SalesOrderResult(
                        SalesOrderOutcome.InsufficientStock,
                        Detail: "The reservation held by this order could not be released, so it "
                            + "has not been voided. Try again in a moment.");
                }
            }
        }

        SalesOrder.FulfilmentStatus = FulfilmentStatus.Cancelled;
        SalesOrder.Status = DocumentStatus.Void;
        SalesOrder.VoidedAt = _clock.GetUtcNow();
        SalesOrder.VoidedBy = _user.UserId;
        SalesOrder.VoidReason = request.Reason;

        await _db.SaveChangesAsync(ct);
        return new SalesOrderResult(SalesOrderOutcome.Ok);
    }

    /// <summary>
    /// Confirming the order: the customer has committed, so the stock is taken
    /// off the shelf for them.
    ///
    /// <b>Nothing reaches the ledger.</b> An order is a promise, and a promise
    /// is not a supply — the double entry is the invoice's job, and posting one
    /// here would recognise revenue on goods that have not left. What it does do
    /// is reserve: the quantity stays in stock and in the valuation but stops
    /// being available, which is the only thing standing between two salespeople
    /// and the same last unit.
    /// </summary>
    public async Task<SalesOrderResult> ConfirmAsync(long SalesOrderId, CancellationToken ct)
    {
        SalesOrder? SalesOrder = await _db.SalesOrders
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.SalesOrderId == SalesOrderId, ct);

        if (SalesOrder is null)
        {
            return new SalesOrderResult(SalesOrderOutcome.NotFound);
        }

        if (!BelongsToCaller(SalesOrder))
        {
            return new SalesOrderResult(SalesOrderOutcome.Forbidden);
        }

        DocumentTransition transition = DocumentLifecycle.CanPost(SalesOrder.Status, SalesOrder.Lines.Count);
        if (!transition.IsAllowed)
        {
            return new SalesOrderResult(SalesOrderOutcome.LifecycleRefused, Detail: transition.Detail);
        }

        var reserveReq = new ReserveStockRequest
        {
            OrgId = _tenant.OrgId.GetValueOrDefault(),
            CustomerId = _tenant.CustomerId.GetValueOrDefault(),
            Lines = SalesOrder.Lines.Where(l => l.LineType == DocumentLineType.Stock && l.ItemId.HasValue)
                .Select(l => new ReserveStockLine { ItemId = l.ItemId!.Value, Quantity = l.Quantity })
                .ToList()
        };

        // Reserved before the status moves, not after: a confirmed order whose
        // reservation failed is one the screen says is committed and the shelf
        // says is available.
        if (reserveReq.Lines.Count > 0)
        {
            ReserveStockResponse reserveRes = await _inventoryClient.ReserveAsync(reserveReq, ct);
            if (!reserveRes.Success)
            {
                return new SalesOrderResult(
                    SalesOrderOutcome.InsufficientStock,
                    Detail: await ShortfallDetailAsync(reserveRes, ct));
            }

            foreach (SalesOrderDetail line in SalesOrder.Lines)
            {
                if (line.LineType == DocumentLineType.Stock && line.ItemId.HasValue)
                {
                    line.ReservedQuantity = line.Quantity;
                }
            }
        }

        SalesOrder.Status = DocumentStatus.Posted;
        SalesOrder.FulfilmentStatus = FulfilmentStatus.Open;
        SalesOrder.PostedAt = _clock.GetUtcNow();
        SalesOrder.PostedBy = _user.UserId;

        await _db.SaveChangesAsync(ct);
        return new SalesOrderResult(SalesOrderOutcome.Ok, SalesOrder.SalesOrderId);
    }

    /// <summary>
    /// An accepted quote, turned into an order.
    ///
    /// <b>The lines are read from the quote, never sent by the caller</b>, and
    /// they go through <see cref="CreateAsync"/> like any other order — so the
    /// tax is recomputed at the rates in force on the order's own date rather
    /// than copied from a quote that may have been priced months ago. That is
    /// also why nothing here touches <c>GstCalculator</c>: one path computes an
    /// order, and this is not a second one.
    /// </summary>
    public async Task<SalesOrderResult> CreateFromQuoteAsync(
        long quoteId, CreateOrderFromQuoteRequest request, CancellationToken ct)
    {
        Quote? quote = await _db.Quotes
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.QuoteId == quoteId, ct);

        if (quote is null)
        {
            return new SalesOrderResult(SalesOrderOutcome.NotFound, Detail: "No such quote.");
        }

        if (_tenant.OrgId is not Guid callerOrgId || quote.OrgId != callerOrgId)
        {
            return new SalesOrderResult(SalesOrderOutcome.Forbidden);
        }

        if (quote.Status != DocumentStatus.Posted)
        {
            return new SalesOrderResult(
                SalesOrderOutcome.QuoteNotConvertible,
                Detail: "Only a quote the customer has accepted becomes an order. Approve the "
                    + "quote first.");
        }

        if (await _db.SalesOrders.AnyAsync(o => o.QuoteId == quoteId, ct))
        {
            return new SalesOrderResult(
                SalesOrderOutcome.QuoteNotConvertible,
                Detail: "This quote has already been converted to a sales order.");
        }

        DateOnly documentDate = request.DocumentDate
            ?? DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        SaveSalesOrderRequest order = new()
        {
            DocumentDate = documentDate,
            DeliveryDate = request.DeliveryDate,
            ContactId = quote.ContactId,
            QuoteId = quote.QuoteId,
            ContactGstin = quote.ContactGstin,
            PlaceOfSupplyStateCode = request.PlaceOfSupplyStateCode,
            BillingAddress = quote.BillingAddress,
            ShippingAddress = quote.ShippingAddress,
            CurrencyCode = quote.CurrencyCode,
            ExchangeRate = quote.ExchangeRate,
            Notes = request.Notes ?? quote.Notes,
            TermsAndConditions = quote.TermsAndConditions,
            Lines = [.. quote.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new SaveSalesOrderLineRequest
                {
                    ItemId = l.ItemId,
                    Description = l.Description,
                    HsnSacCode = l.HsnSacCode,
                    WarehouseId = l.WarehouseId,
                    Quantity = l.Quantity,
                    UomId = l.UomId,
                    ConversionFactor = l.ConversionFactor,
                    UnitPrice = l.UnitPrice,
                    IsPriceInclusive = l.IsPriceInclusive,
                    DiscountPercent = l.DiscountPercent,
                    DiscountAmount = l.DiscountAmount,
                    TaxTreatment = l.TaxTreatment,
                    TaxGroupId = l.TaxGroupId,
                    LineType = l.LineType,
                    AccountId = l.AccountId,
                    FixedAssetCategoryId = l.FixedAssetCategoryId,
                    ItemBatchId = l.ItemBatchId,
                    LineNotes = l.LineNotes,
                })],
        };

        // The place of supply is re-resolved from the GSTIN rather than copied:
        // the quote stored the answer (IsInterState), and copying an answer is
        // how a branch that has since changed state files the wrong return.
        return await CreateAsync(order, ct);
    }

    public async Task<SalesOrderViewResult> GetAsync(long SalesOrderId, CancellationToken ct)
    {
        var found = await _db.SalesOrders
            .Include(q => q.Lines)
            .ThenInclude(l => l.Taxes)
            .Where(q => q.SalesOrderId == SalesOrderId)
            .Select(q => new { q.OrgId, View = new SalesOrderView
            {
                SalesOrderId = q.SalesOrderId,
                DocumentNo = q.DocumentNo,
                DocumentDate = q.DocumentDate,
                DeliveryDate = q.DeliveryDate,
                FulfilmentStatus = q.FulfilmentStatus.ToString(),
                ContactId = q.ContactId,
                CurrencyCode = q.CurrencyCode,
                TaxableAmount = q.TaxableAmount,
                TotalAmount = q.TotalAmount,
                Status = q.Status.ToString(),
                IsInterState = q.IsInterState,
                InvoicedDocumentId = _db.Invoices.Where(o => o.SalesOrderId == q.SalesOrderId).Select(o => (long?)o.InvoiceId).FirstOrDefault(),
                ContactGstin = q.ContactGstin,
                PlaceOfSupplyStateId = q.PlaceOfSupplyStateId,
                BillingAddress = q.BillingAddress,
                ShippingAddress = q.ShippingAddress,
                ExchangeRate = q.ExchangeRate,
                SubTotal = q.SubTotal,
                DiscountAmount = q.DiscountAmount,
                CgstAmount = q.CgstAmount,
                SgstAmount = q.SgstAmount,
                IgstAmount = q.IgstAmount,
                CessAmount = q.CessAmount,
                RoundOffAmount = q.RoundOffAmount,
                TotalAmountBase = q.TotalAmountBase,
                Notes = q.Notes,
                TermsAndConditions = q.TermsAndConditions,
                PostedAt = q.PostedAt,
                VoidedAt = q.VoidedAt,
                VoidReason = q.VoidReason,
                Lines = q.Lines.Select(l => new SalesOrderLineView
                {
                    SalesOrderDetailId = l.SalesOrderDetailId,
                    LineNumber = l.LineNumber,
                    ItemId = l.ItemId,
                    Description = l.Description,
                    HsnSacCode = l.HsnSacCode,
                    WarehouseId = l.WarehouseId,
                    Quantity = l.Quantity,
                    UomId = l.UomId,
                    ConversionFactor = l.ConversionFactor,
                    BaseQuantity = l.BaseQuantity,
                    ReservedQuantity = l.ReservedQuantity,
                    DeliveredQuantity = l.DeliveredQuantity,
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
                    Taxes = l.Taxes.Select(t => new SalesOrderLineTaxView
                    {
                        SalesOrderDetailTaxId = t.SalesOrderDetailTaxId,
                        TaxComponent = t.TaxComponent.ToString(),
                        SubAccountId = t.SubAccountId,
                        Rate = t.Rate,
                        TaxableAmount = t.TaxableAmount,
                        Amount = t.Amount,
                        AmountBase = t.AmountBase,
                    }).ToList()
                }).ToList()
            } }).FirstOrDefaultAsync(ct);

        if (found is null)
        {
            return new SalesOrderViewResult(SalesOrderOutcome.NotFound);
        }

        // The OrgId is carried out of the projection and never onto the view:
        // the caller has to be told whose row this is, and the browser does not.
        if (_tenant.OrgId is not Guid callerOrgId || found.OrgId != callerOrgId)
        {
            return new SalesOrderViewResult(SalesOrderOutcome.Forbidden);
        }

        SalesOrderView SalesOrder = found.View;

        IReadOnlyDictionary<long, NamedRef> contacts = await _contactNames.ResolveAsync([SalesOrder.ContactId], ct);
        if (contacts.TryGetValue(SalesOrder.ContactId, out NamedRef? contactName))
        {
            SalesOrder.ContactName = contactName.Name;
            SalesOrder.ContactCode = contactName.Code;
        }

        var itemIds = SalesOrder.Lines.Where(l => l.ItemId.HasValue).Select(l => l.ItemId!.Value).Distinct().ToList();
        if (itemIds.Count > 0)
        {
            IReadOnlyDictionary<long, NamedRef> items = await _itemNames.ResolveAsync(itemIds, ct);
            foreach (var line in SalesOrder.Lines)
            {
                if (line.ItemId.HasValue && items.TryGetValue(line.ItemId.Value, out NamedRef? itemName))
                {
                    line.ItemLabel = $"{itemName.Code} - {itemName.Name}";
                }
            }
        }

        return new SalesOrderViewResult(SalesOrderOutcome.Ok, SalesOrder);
    }

    /// <summary>How many rows one page may ask for, however large a number it sends.</summary>
    private const int MaxPageSize = 200;

    /// <summary>
    /// One page of sales orders, newest first, with the total that matched.
    ///
    /// <b>Both bounds are clamped rather than trusted.</b> <c>skip</c> comes off
    /// a query string, and a negative one is not merely odd — <c>Skip(-1)</c>
    /// throws on some providers and silently returns the first page on others,
    /// so a hand-edited URL either 500s or quietly shows page one while the pager
    /// says otherwise. <c>take</c> is clamped at both ends for the same reason
    /// and a second one: <c>take=1000000</c> is a way to ask for every order in
    /// the branch in a single response.
    ///
    /// The projection is a <see cref="SalesOrderListItem"/> built in the database
    /// rather than an entity read and mapped after, so the fifteen columns a list
    /// screen shows are the fifteen that cross the wire — not the header's forty
    /// plus every line and tax row hanging off it.
    /// </summary>
    public async Task<SalesOrderListPage> ListAsync(
        int skip, int take, string? status, string? search, CancellationToken ct)
    {
        int safeSkip = Math.Max(skip, 0);
        int safeTake = Math.Clamp(take, 1, MaxPageSize);

        IQueryable<SalesOrder> query = _db.SalesOrders;

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse(status.Trim(), ignoreCase: true, out DocumentStatus wanted))
        {
            query = query.Where(o => o.Status == wanted);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(o => o.DocumentNo.Contains(term));
        }

        // Counted before paging: the screen has to say how many matched, not how
        // many fitted on the page.
        int total = await query.CountAsync(ct);

        List<SalesOrderListItem> rows = await query
            .OrderByDescending(o => o.DocumentDate)
            .ThenByDescending(o => o.DocumentNo)
            .Skip(safeSkip)
            .Take(safeTake)
            .Select(o => new SalesOrderListItem
            {
                SalesOrderId = o.SalesOrderId,
                DocumentNo = o.DocumentNo,
                DocumentDate = o.DocumentDate,
                QuoteId = o.QuoteId,
                DeliveryDate = o.DeliveryDate,
                FulfilmentStatus = o.FulfilmentStatus.ToString(),
                ContactId = o.ContactId,
                CurrencyCode = o.CurrencyCode,
                TaxableAmount = o.TaxableAmount,
                TotalAmount = o.TotalAmount,
                Status = o.Status.ToString(),
                IsInterState = o.IsInterState,
                InvoicedDocumentId = _db.Invoices
                    .Where(i => i.SalesOrderId == o.SalesOrderId)
                    .Select(i => (long?)i.InvoiceId)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        // One call for the whole page, never one per row — Contacts is another
        // database and this is the list screen that would make it an N+1.
        List<long> contactIds = [.. rows.Select(r => r.ContactId).Distinct()];
        if (contactIds.Count > 0)
        {
            IReadOnlyDictionary<long, NamedRef> contacts = await _contactNames.ResolveAsync(contactIds, ct);
            foreach (SalesOrderListItem row in rows)
            {
                if (contacts.TryGetValue(row.ContactId, out NamedRef? contactName))
                {
                    row.ContactName = contactName.Name;
                    row.ContactCode = contactName.Code;
                }
            }
        }

        return new SalesOrderListPage
        {
            Total = total,
            Skip = safeSkip,
            Take = safeTake,
            Rows = rows,
        };
    }

    /// <summary>
    /// Whether a row that was found may be shown to this caller.
    ///
    /// See <see cref="SalesOrderOutcome.Forbidden"/> for why this exists when
    /// the query filter and RLS have already hidden other branches' rows.
    /// </summary>
    private bool BelongsToCaller(SalesOrder order) =>
        _tenant.OrgId is Guid orgId && order.OrgId == orgId;

    /// <summary>
    /// Which items were short, by name.
    ///
    /// "Insufficient stock" on a twenty-line order tells the person on the phone
    /// to the customer nothing they can act on. Inventory answers per line, so
    /// the message says which items and lets them take those lines off.
    /// </summary>
    private async Task<string> ShortfallDetailAsync(ReserveStockResponse response, CancellationToken ct)
    {
        List<long> shortItemIds = [.. response.Lines.Where(l => !l.Success).Select(l => l.ItemId).Distinct()];

        if (shortItemIds.Count == 0)
        {
            return "There is not enough stock available to reserve for this order.";
        }

        IReadOnlyDictionary<long, NamedRef> names = await _itemNames.ResolveAsync(shortItemIds, ct);

        IEnumerable<string> labels = shortItemIds.Select(id =>
            names.TryGetValue(id, out NamedRef? named) ? $"{named.Code} - {named.Name}" : $"item {id}");

        return "There is not enough stock available to reserve: " + string.Join(", ", labels) + ".";
    }
}
