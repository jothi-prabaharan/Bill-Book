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

        PlaceOfSupplyResult pos = PlaceOfSupply.Resolve(
            settings.StateCode, request.PlaceOfSupplyStateCode, request.ContactGstin);

        if (!pos.IsOk)
        {
            return new QuoteResult(QuoteOutcome.PlaceOfSupplyRefused, Detail: pos.Detail);
        }

        TaxContext taxContext = new(pos.IsInterState, settings.DiscountBeforeTax);

        NumberAllocation alloc = await _numbering.NextAsync("QTE", request.DocumentDate, ct);

        Quote Quote = new()
        {
            TransactionTypeCode = "QTE",
            DocumentNo = alloc.Code,
            DocumentDate = request.DocumentDate,
            ValidUntil = request.ValidUntil,
            ContactId = request.ContactId,
            ContactGstin = request.ContactGstin,
            BillingAddress = request.BillingAddress,
            ShippingAddress = request.ShippingAddress,
            PlaceOfSupplyStateId = 0,
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
                    AmountBase = comp.Amount * Quote.ExchangeRate,
                });
            }

            Quote.Lines.Add(detail);
        }

        TaxDocumentTotals totals = GstCalculator.Totals(taxLines);

        Quote.SubTotal = totals.SubTotal;
        Quote.DiscountAmount = totals.DiscountAmount;
        Quote.TaxableAmount = totals.TaxableAmount;
        Quote.CgstAmount = totals.CgstAmount;
        Quote.SgstAmount = totals.SgstAmount;
        Quote.IgstAmount = totals.IgstAmount;
        Quote.CessAmount = totals.CessAmount;
        Quote.RoundOffAmount = Math.Round(totals.TotalAmount, 0, MidpointRounding.AwayFromZero) - totals.TotalAmount;
        Quote.TotalAmount = totals.TotalAmount + Quote.RoundOffAmount;
        Quote.TotalAmountBase = Quote.TotalAmount * Quote.ExchangeRate;

        Quote.Status = DocumentStatus.Draft;
        Quote.PostedAt = _clock.GetUtcNow();
        Quote.PostedBy = _user.UserId;
        
        _db.Quotes.Add(Quote);
        await _db.SaveChangesAsync(ct);

        return new QuoteResult(QuoteOutcome.Ok, Quote.QuoteId);
    }

    public async Task<QuoteResult> UpdateAsync(long QuoteId, SaveQuoteRequest request, CancellationToken ct)
    {
        Quote? Quote = await _db.Quotes
            .Include(q => q.Lines)
            .ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(q => q.QuoteId == QuoteId, ct);

        if (Quote is null)
        {
            return new QuoteResult(QuoteOutcome.NotFound);
        }

        if (Quote.Status != DocumentStatus.Draft)
        {
            return new QuoteResult(QuoteOutcome.LifecycleRefused, Detail: "Only draft Quotes can be updated.");
        }

        BranchSettings? settings = await _branchSettings.GetSettingsAsync(ct);
        if (settings is null)
        {
            return new QuoteResult(QuoteOutcome.RatesUnavailable, Detail: "Branch settings could not be read.");
        }

        PlaceOfSupplyResult pos = PlaceOfSupply.Resolve(
            settings.StateCode, request.PlaceOfSupplyStateCode, request.ContactGstin);

        if (!pos.IsOk)
        {
            return new QuoteResult(QuoteOutcome.PlaceOfSupplyRefused, Detail: pos.Detail);
        }

        TaxContext taxContext = new(pos.IsInterState, settings.DiscountBeforeTax);

        Quote.DocumentDate = request.DocumentDate;
        Quote.ValidUntil = request.ValidUntil;
        Quote.ContactId = request.ContactId;
        Quote.ContactGstin = request.ContactGstin;
        Quote.BillingAddress = request.BillingAddress;
        Quote.ShippingAddress = request.ShippingAddress;
        Quote.IsInterState = pos.IsInterState;
        
        if (request.CurrencyCode != null)
        {
            Quote.CurrencyCode = request.CurrencyCode;
        }
        if (request.ExchangeRate.HasValue)
        {
            Quote.ExchangeRate = request.ExchangeRate.Value;
        }

        Quote.Notes = request.Notes;
        Quote.TermsAndConditions = request.TermsAndConditions;

        var taxLines = new List<TaxLineResult>(request.Lines.Count);
        Quote.Lines.Clear();

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
                    AmountBase = comp.Amount * Quote.ExchangeRate,
                });
            }

            Quote.Lines.Add(detail);
        }

        TaxDocumentTotals totals = GstCalculator.Totals(taxLines);

        Quote.SubTotal = totals.SubTotal;
        Quote.DiscountAmount = totals.DiscountAmount;
        Quote.TaxableAmount = totals.TaxableAmount;
        Quote.CgstAmount = totals.CgstAmount;
        Quote.SgstAmount = totals.SgstAmount;
        Quote.IgstAmount = totals.IgstAmount;
        Quote.CessAmount = totals.CessAmount;
        Quote.RoundOffAmount = Math.Round(totals.TotalAmount, 0, MidpointRounding.AwayFromZero) - totals.TotalAmount;
        Quote.TotalAmount = totals.TotalAmount + Quote.RoundOffAmount;
        Quote.TotalAmountBase = Quote.TotalAmount * Quote.ExchangeRate;

        await _db.SaveChangesAsync(ct);

        return new QuoteResult(QuoteOutcome.Ok, Quote.QuoteId);
    }

    public async Task<QuoteResult> VoidAsync(long QuoteId, VoidQuoteRequest request, CancellationToken ct)
    {
        Quote? Quote = await _db.Quotes.FindAsync(new object[] { QuoteId }, ct);

        if (Quote is null)
        {
            return new QuoteResult(QuoteOutcome.NotFound);
        }

        bool hasDownstream = await _db.SalesOrders.AnyAsync(o => o.QuoteId == QuoteId, ct);

        DocumentTransition transition = DocumentLifecycle.CanVoid(Quote.Status, hasDownstream, request.Reason);
        if (!transition.IsAllowed)
        {
            return new QuoteResult(QuoteOutcome.LifecycleRefused, Detail: transition.Detail);
        }

        Quote.Status = DocumentStatus.Void;
        Quote.VoidedAt = _clock.GetUtcNow();
        Quote.VoidedBy = _user.UserId;
        Quote.VoidReason = request.Reason;

        await _db.SaveChangesAsync(ct);
        return new QuoteResult(QuoteOutcome.Ok);
    }

    public async Task<QuoteResult> PostAsync(long QuoteId, CancellationToken ct)
    {
        Quote? Quote = await _db.Quotes
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.QuoteId == QuoteId, ct);

        if (Quote is null)
        {
            return new QuoteResult(QuoteOutcome.NotFound);
        }

        DocumentTransition transition = DocumentLifecycle.CanPost(Quote.Status, Quote.Lines.Count);
        if (!transition.IsAllowed)
        {
            return new QuoteResult(QuoteOutcome.LifecycleRefused, Detail: transition.Detail);
        }

        Quote.Status = DocumentStatus.Posted;
        Quote.PostedAt = _clock.GetUtcNow();
        Quote.PostedBy = _user.UserId;

        await _db.SaveChangesAsync(ct);
        return new QuoteResult(QuoteOutcome.Ok);
    }

    public async Task<QuoteView?> GetAsync(long QuoteId, CancellationToken ct)
    {
        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().Date);
        var Quote = await _db.Quotes
            .Include(q => q.Lines)
            .ThenInclude(l => l.Taxes)
            .Where(q => q.QuoteId == QuoteId)
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
                HasLapsed = q.Status == DocumentStatus.Posted && q.ValidUntil < today,
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

        if (Quote is null) return null;

        IReadOnlyDictionary<long, NamedRef> contacts = await _contactNames.ResolveAsync([Quote.ContactId], ct);
        if (contacts.TryGetValue(Quote.ContactId, out NamedRef? contactName))
        {
            Quote.ContactName = contactName.Name;
            Quote.ContactCode = contactName.Code;
        }

        var itemIds = Quote.Lines.Where(l => l.ItemId.HasValue).Select(l => l.ItemId!.Value).Distinct().ToList();
        if (itemIds.Count > 0)
        {
            IReadOnlyDictionary<long, NamedRef> items = await _itemNames.ResolveAsync(itemIds, ct);
            foreach (var line in Quote.Lines)
            {
                if (line.ItemId.HasValue && items.TryGetValue(line.ItemId.Value, out NamedRef? itemName))
                {
                    line.ItemLabel = $"{itemName.Code} - {itemName.Name}";
                }
            }
        }

        return Quote;
    }

    public async Task<List<QuoteListItem>> ListAsync(CancellationToken ct)
    {
        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().Date);
        var Quotes = await _db.Quotes
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
                HasLapsed = q.Status == DocumentStatus.Posted && q.ValidUntil < today,
            }).ToListAsync(ct);

        var contactIds = Quotes.Select(q => q.ContactId).Distinct().ToList();
        if (contactIds.Count > 0)
        {
            IReadOnlyDictionary<long, NamedRef> contacts = await _contactNames.ResolveAsync(contactIds, ct);
            foreach (var q in Quotes)
            {
                if (contacts.TryGetValue(q.ContactId, out NamedRef? contactName))
                {
                    q.ContactName = contactName.Name;
                    q.ContactCode = contactName.Code;
                }
            }
        }

        return Quotes;
    }
}
