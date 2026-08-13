using Microsoft.EntityFrameworkCore;
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
        IInventoryClient inventoryClient)
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

        SalesOrder.Status = DocumentStatus.Draft;
        SalesOrder.PostedAt = _clock.GetUtcNow();
        SalesOrder.PostedBy = _user.UserId;
        
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

        if (SalesOrder.Status != DocumentStatus.Draft)
        {
            return new SalesOrderResult(SalesOrderOutcome.LifecycleRefused, Detail: "Only draft SalesOrders can be updated.");
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

        await _db.SaveChangesAsync(ct);

        return new SalesOrderResult(SalesOrderOutcome.Ok, SalesOrder.SalesOrderId);
    }

    public async Task<SalesOrderResult> VoidAsync(long SalesOrderId, VoidSalesOrderRequest request, CancellationToken ct)
    {
        SalesOrder? SalesOrder = await _db.SalesOrders.FindAsync(new object[] { SalesOrderId }, ct);

        if (SalesOrder is null)
        {
            return new SalesOrderResult(SalesOrderOutcome.NotFound);
        }

        bool hasDownstream = await _db.SalesOrders.AnyAsync(o => o.SalesOrderId == SalesOrderId, ct)
            || await _db.Invoices.AnyAsync(i => i.SalesOrderId == SalesOrderId, ct);

        DocumentTransition transition = DocumentLifecycle.CanVoid(SalesOrder.Status, hasDownstream, request.Reason);
        if (!transition.IsAllowed)
        {
            return new SalesOrderResult(SalesOrderOutcome.LifecycleRefused, Detail: transition.Detail);
        }

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
            var releaseRes = await _inventoryClient.ReleaseAsync(releaseReq, ct);
            if (!releaseRes.Success)
            {
                // In a real system, you'd want to handle this gracefully or queue a retry.
            }
        }

        SalesOrder.Status = DocumentStatus.Void;
        SalesOrder.VoidedAt = _clock.GetUtcNow();
        SalesOrder.VoidedBy = _user.UserId;
        SalesOrder.VoidReason = request.Reason;

        await _db.SaveChangesAsync(ct);
        return new SalesOrderResult(SalesOrderOutcome.Ok);
    }

    public async Task<SalesOrderResult> PostAsync(long SalesOrderId, CancellationToken ct)
    {
        SalesOrder? SalesOrder = await _db.SalesOrders
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.SalesOrderId == SalesOrderId, ct);

        if (SalesOrder is null)
        {
            return new SalesOrderResult(SalesOrderOutcome.NotFound);
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

        if (reserveReq.Lines.Count > 0)
        {
            var reserveRes = await _inventoryClient.ReserveAsync(reserveReq, ct);
            if (!reserveRes.Success)
            {
                return new SalesOrderResult(SalesOrderOutcome.InsufficientStock, Detail: "Insufficient stock available to reserve for this order.");
            }
        }

        SalesOrder.Status = DocumentStatus.Posted;
        SalesOrder.PostedAt = _clock.GetUtcNow();
        SalesOrder.PostedBy = _user.UserId;

        await _db.SaveChangesAsync(ct);
        return new SalesOrderResult(SalesOrderOutcome.Ok);
    }

    public async Task<SalesOrderView?> GetAsync(long SalesOrderId, CancellationToken ct)
    {
        var SalesOrder = await _db.SalesOrders
            .Include(q => q.Lines)
            .ThenInclude(l => l.Taxes)
            .Where(q => q.SalesOrderId == SalesOrderId)
            .Select(q => new SalesOrderView
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
            }).FirstOrDefaultAsync(ct);

        if (SalesOrder is null) return null;

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

        return SalesOrder;
    }

    public async Task<List<SalesOrderListItem>> ListAsync(CancellationToken ct)
    {
        var SalesOrders = await _db.SalesOrders
            .OrderByDescending(q => q.DocumentDate)
            .ThenByDescending(q => q.DocumentNo)
            .Select(q => new SalesOrderListItem
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
            }).ToListAsync(ct);

        var contactIds = SalesOrders.Select(q => q.ContactId).Distinct().ToList();
        if (contactIds.Count > 0)
        {
            IReadOnlyDictionary<long, NamedRef> contacts = await _contactNames.ResolveAsync(contactIds, ct);
            foreach (var q in SalesOrders)
            {
                if (contacts.TryGetValue(q.ContactId, out NamedRef? contactName))
                {
                    q.ContactName = contactName.Name;
                    q.ContactCode = contactName.Code;
                }
            }
        }

        return SalesOrders;
    }
}
