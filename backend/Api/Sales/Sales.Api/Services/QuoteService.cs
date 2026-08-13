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

public sealed class QuoteService
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

    public QuoteService(
        SalesDbContext db,
        ITenantContext tenant,
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
        _tenant = tenant;
        _numbering = numbering;
        _baseCurrency = baseCurrency;
        _branchSettings = branchSettings;
        _rates = rates;
        _contactNames = contactNames;
        _itemNames = itemNames;
        _user = user;
        _clock = clock;
    }

    public async Task<QuoteResult> CreateAsync(SaveQuoteRequest request, CancellationToken ct)
    {
        string? baseCurrency = await _baseCurrency.GetBaseCurrencyAsync(ct);
        if (baseCurrency is null)
        {
            return new QuoteResult(QuoteOutcome.RatesUnavailable, Detail: "Branch base currency could not be read.");
        }

        BranchSettings? settings = await _branchSettings.GetSettingsAsync(ct);
        if (settings is null)
        {
            return new QuoteResult(QuoteOutcome.RatesUnavailable, Detail: "Branch settings could not be read.");
        }

        if (request.ValidUntil < request.DocumentDate)
        {
            return new QuoteResult(QuoteOutcome.ValidityInvalid, Detail: "Validity date cannot be before the document date.");
        }

        PlaceOfSupplyResult pos = PlaceOfSupply.Resolve(
            settings.StateCode, request.PlaceOfSupplyStateCode, request.ContactGstin);

        if (!pos.IsOk)
        {
            return new QuoteResult(QuoteOutcome.PlaceOfSupplyRefused, Detail: pos.Detail);
        }

        TaxContext taxContext = new(pos.IsInterState, settings.DiscountBeforeTax);

        NumberAllocation alloc = await _numbering.NextAsync("QTE", request.DocumentDate, ct);

        Quote quote = new()
        {
            TransactionTypeCode = "QTE",
            DocumentNo = alloc.Code,
            DocumentDate = request.DocumentDate,
            ValidUntil = request.ValidUntil,
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
            SaveQuoteLineRequest lineReq = request.Lines[i];

            if (lineReq.ItemId is null && string.IsNullOrWhiteSpace(lineReq.Description))
            {
                return new QuoteResult(QuoteOutcome.LineInvalid, Detail: $"Line {i + 1} is a free-text line and must have a description.");
            }
            if (lineReq.ItemId is null && lineReq.AccountId is null)
            {
                return new QuoteResult(QuoteOutcome.LineInvalid, Detail: $"Line {i + 1} is a free-text line and must have an account selected.");
            }

            TaxRate? rate = null;
            if (lineReq.TaxGroupId is long taxGroupId)
            {
                rate = await _rates.GetRateAsync(taxGroupId, request.DocumentDate, ct);
                if (rate is null)
                {
                    return new QuoteResult(QuoteOutcome.RatesUnavailable, Detail: $"Tax rate for group {taxGroupId} could not be read for date {request.DocumentDate}.");
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

            QuoteDetail detail = new()
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
                detail.Taxes.Add(new QuoteDetailTax
                {
                    TaxComponent = comp.Component,
                    Rate = comp.Rate,
                    TaxableAmount = comp.TaxableAmount,
                    Amount = comp.Amount,
                    AmountBase = comp.Amount * quote.ExchangeRate,
                });
            }

            quote.Lines.Add(detail);
        }

        TaxDocumentTotals totals = GstCalculator.Totals(taxLines);

        quote.SubTotal = totals.SubTotal;
        quote.DiscountAmount = totals.DiscountAmount;
        quote.TaxableAmount = totals.TaxableAmount;
        quote.CgstAmount = totals.CgstAmount;
        quote.SgstAmount = totals.SgstAmount;
        quote.IgstAmount = totals.IgstAmount;
        quote.CessAmount = totals.CessAmount;
        quote.RoundOffAmount = Math.Round(totals.TotalAmount, 0, MidpointRounding.AwayFromZero) - totals.TotalAmount;
        quote.TotalAmount = totals.TotalAmount + quote.RoundOffAmount;
        quote.TotalAmountBase = quote.TotalAmount * quote.ExchangeRate;

        quote.Status = DocumentStatus.Draft;
        quote.PostedAt = _clock.GetUtcNow();
        quote.PostedBy = _user.UserId;
        
        _db.Quotes.Add(quote);
        await _db.SaveChangesAsync(ct);

        return new QuoteResult(QuoteOutcome.Ok, quote.QuoteId);
    }

    public async Task<QuoteResult> UpdateAsync(long quoteId, SaveQuoteRequest request, CancellationToken ct)
    {
        Quote? quote = await _db.Quotes
            .Include(q => q.Lines)
            .ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(q => q.QuoteId == quoteId, ct);

        if (quote is null)
        {
            return new QuoteResult(QuoteOutcome.NotFound);
        }

        if (quote.Status != DocumentStatus.Draft)
        {
            return new QuoteResult(QuoteOutcome.LifecycleRefused, Detail: "Only draft quotes can be updated.");
        }

        BranchSettings? settings = await _branchSettings.GetSettingsAsync(ct);
        if (settings is null)
        {
            return new QuoteResult(QuoteOutcome.RatesUnavailable, Detail: "Branch settings could not be read.");
        }

        if (request.ValidUntil < request.DocumentDate)
        {
            return new QuoteResult(QuoteOutcome.ValidityInvalid, Detail: "Validity date cannot be before the document date.");
        }

        PlaceOfSupplyResult pos = PlaceOfSupply.Resolve(
            settings.StateCode, request.PlaceOfSupplyStateCode, request.ContactGstin);

        if (!pos.IsOk)
        {
            return new QuoteResult(QuoteOutcome.PlaceOfSupplyRefused, Detail: pos.Detail);
        }

        TaxContext taxContext = new(pos.IsInterState, settings.DiscountBeforeTax);

        quote.DocumentDate = request.DocumentDate;
        quote.ValidUntil = request.ValidUntil;
        quote.ContactId = request.ContactId;
        quote.ContactGstin = request.ContactGstin;
        quote.BillingAddress = request.BillingAddress;
        quote.ShippingAddress = request.ShippingAddress;
        quote.IsInterState = pos.IsInterState;
        
        if (request.CurrencyCode != null)
        {
            quote.CurrencyCode = request.CurrencyCode;
        }
        if (request.ExchangeRate.HasValue)
        {
            quote.ExchangeRate = request.ExchangeRate.Value;
        }

        quote.Notes = request.Notes;
        quote.TermsAndConditions = request.TermsAndConditions;

        var taxLines = new List<TaxLineResult>(request.Lines.Count);
        quote.Lines.Clear();

        for (int i = 0; i < request.Lines.Count; i++)
        {
            SaveQuoteLineRequest lineReq = request.Lines[i];

            if (lineReq.ItemId is null && string.IsNullOrWhiteSpace(lineReq.Description))
            {
                return new QuoteResult(QuoteOutcome.LineInvalid, Detail: $"Line {i + 1} is a free-text line and must have a description.");
            }
            if (lineReq.ItemId is null && lineReq.AccountId is null)
            {
                return new QuoteResult(QuoteOutcome.LineInvalid, Detail: $"Line {i + 1} is a free-text line and must have an account selected.");
            }

            TaxRate? rate = null;
            if (lineReq.TaxGroupId is long taxGroupId)
            {
                rate = await _rates.GetRateAsync(taxGroupId, request.DocumentDate, ct);
                if (rate is null)
                {
                    return new QuoteResult(QuoteOutcome.RatesUnavailable, Detail: $"Tax rate for group {taxGroupId} could not be read for date {request.DocumentDate}.");
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

            QuoteDetail detail = new()
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
                detail.Taxes.Add(new QuoteDetailTax
                {
                    TaxComponent = comp.Component,
                    Rate = comp.Rate,
                    TaxableAmount = comp.TaxableAmount,
                    Amount = comp.Amount,
                    AmountBase = comp.Amount * quote.ExchangeRate,
                });
            }

            quote.Lines.Add(detail);
        }

        TaxDocumentTotals totals = GstCalculator.Totals(taxLines);

        quote.SubTotal = totals.SubTotal;
        quote.DiscountAmount = totals.DiscountAmount;
        quote.TaxableAmount = totals.TaxableAmount;
        quote.CgstAmount = totals.CgstAmount;
        quote.SgstAmount = totals.SgstAmount;
        quote.IgstAmount = totals.IgstAmount;
        quote.CessAmount = totals.CessAmount;
        quote.RoundOffAmount = Math.Round(totals.TotalAmount, 0, MidpointRounding.AwayFromZero) - totals.TotalAmount;
        quote.TotalAmount = totals.TotalAmount + quote.RoundOffAmount;
        quote.TotalAmountBase = quote.TotalAmount * quote.ExchangeRate;

        await _db.SaveChangesAsync(ct);

        return new QuoteResult(QuoteOutcome.Ok, quote.QuoteId);
    }

    public async Task<QuoteResult> VoidAsync(long quoteId, VoidQuoteRequest request, CancellationToken ct)
    {
        Quote? quote = await _db.Quotes.FindAsync(new object[] { quoteId }, ct);

        if (quote is null)
        {
            return new QuoteResult(QuoteOutcome.NotFound);
        }

        bool hasDownstream = await _db.SalesOrders.AnyAsync(o => o.QuoteId == quoteId, ct)
            || await _db.Invoices.AnyAsync(i => i.QuoteId == quoteId, ct);

        DocumentTransition transition = DocumentLifecycle.CanVoid(quote.Status, hasDownstream, request.Reason);
        if (!transition.IsAllowed)
        {
            return new QuoteResult(QuoteOutcome.LifecycleRefused, Detail: transition.Detail);
        }

        quote.Status = DocumentStatus.Void;
        quote.VoidedAt = _clock.GetUtcNow();
        quote.VoidedBy = _user.UserId;
        quote.VoidReason = request.Reason;

        await _db.SaveChangesAsync(ct);
        return new QuoteResult(QuoteOutcome.Ok);
    }

    public async Task<QuoteView?> GetAsync(long quoteId, CancellationToken ct)
    {
        var quote = await _db.Quotes
            .Include(q => q.Lines)
            .ThenInclude(l => l.Taxes)
            .Where(q => q.QuoteId == quoteId)
            .Select(q => new QuoteView
            {
                QuoteId = q.QuoteId,
                DocumentNo = q.DocumentNo,
                DocumentDate = q.DocumentDate,
                ValidUntil = q.ValidUntil,
                ContactId = q.ContactId,
                CurrencyCode = q.CurrencyCode,
                TaxableAmount = q.TaxableAmount,
                TotalAmount = q.TotalAmount,
                Status = q.Status.ToString(),
                IsInterState = q.IsInterState,
                ConvertedToSalesOrderId = _db.SalesOrders.Where(o => o.QuoteId == q.QuoteId).Select(o => (long?)o.SalesOrderId).FirstOrDefault(),
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
                Lines = q.Lines.Select(l => new QuoteLineView
                {
                    QuoteDetailId = l.QuoteDetailId,
                    LineNumber = l.LineNumber,
                    ItemId = l.ItemId,
                    Description = l.Description,
                    HsnSacCode = l.HsnSacCode,
                    WarehouseId = l.WarehouseId,
                    Quantity = l.Quantity,
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
                    Taxes = l.Taxes.Select(t => new QuoteLineTaxView
                    {
                        QuoteDetailTaxId = t.QuoteDetailTaxId,
                        TaxComponent = t.TaxComponent.ToString(),
                        SubAccountId = t.SubAccountId,
                        Rate = t.Rate,
                        TaxableAmount = t.TaxableAmount,
                        Amount = t.Amount,
                        AmountBase = t.AmountBase,
                    }).ToList()
                }).ToList()
            }).FirstOrDefaultAsync(ct);

        if (quote is null) return null;

        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        quote.HasLapsed = quote.Status == DocumentStatus.Draft.ToString() && quote.ValidUntil < today;

        IReadOnlyDictionary<long, NamedRef> contacts = await _contactNames.ResolveAsync([quote.ContactId], ct);
        if (contacts.TryGetValue(quote.ContactId, out NamedRef? contactName))
        {
            quote.ContactName = contactName.Name;
            quote.ContactCode = contactName.Code;
        }

        var itemIds = quote.Lines.Where(l => l.ItemId.HasValue).Select(l => l.ItemId!.Value).Distinct().ToList();
        if (itemIds.Count > 0)
        {
            IReadOnlyDictionary<long, NamedRef> items = await _itemNames.ResolveAsync(itemIds, ct);
            foreach (var line in quote.Lines)
            {
                if (line.ItemId.HasValue && items.TryGetValue(line.ItemId.Value, out NamedRef? itemName))
                {
                    line.ItemLabel = $"{itemName.Code} - {itemName.Name}";
                }
            }
        }

        return quote;
    }

    public async Task<List<QuoteListItem>> ListAsync(CancellationToken ct)
    {
        var quotes = await _db.Quotes
            .OrderByDescending(q => q.DocumentDate)
            .ThenByDescending(q => q.DocumentNo)
            .Select(q => new QuoteListItem
            {
                QuoteId = q.QuoteId,
                DocumentNo = q.DocumentNo,
                DocumentDate = q.DocumentDate,
                ValidUntil = q.ValidUntil,
                ContactId = q.ContactId,
                CurrencyCode = q.CurrencyCode,
                TaxableAmount = q.TaxableAmount,
                TotalAmount = q.TotalAmount,
                Status = q.Status.ToString(),
                IsInterState = q.IsInterState,
                ConvertedToSalesOrderId = _db.SalesOrders.Where(o => o.QuoteId == q.QuoteId).Select(o => (long?)o.SalesOrderId).FirstOrDefault(),
            }).ToListAsync(ct);

        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        foreach (var q in quotes)
        {
            q.HasLapsed = q.Status == DocumentStatus.Draft.ToString() && q.ValidUntil < today;
        }

        var contactIds = quotes.Select(q => q.ContactId).Distinct().ToList();
        if (contactIds.Count > 0)
        {
            IReadOnlyDictionary<long, NamedRef> contacts = await _contactNames.ResolveAsync(contactIds, ct);
            foreach (var q in quotes)
            {
                if (contacts.TryGetValue(q.ContactId, out NamedRef? contactName))
                {
                    q.ContactName = contactName.Name;
                    q.ContactCode = contactName.Code;
                }
            }
        }

        return quotes;
    }
}
