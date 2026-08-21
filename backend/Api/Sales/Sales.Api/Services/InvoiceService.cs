using Microsoft.EntityFrameworkCore;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tax;
using Shared.Kernel.Tenancy;

namespace Sales.Api.Services;

/// <summary>
/// The Sales Invoice and POS Sale service — <c>INV</c> and <c>POS</c>.
///
/// Handles draft save/update with GST calculations via <see cref="GstCalculator"/>,
/// consecutive atomic CAS number generation via <see cref="INumberGenerator"/>,
/// double-entry accounting ledger posting via <see cref="ILedgerClient"/>,
/// inventory depletion / reservation releases via <see cref="IInventoryClient"/>,
/// and GSTR-1 synchronous sales register recording.
/// </summary>
public sealed class InvoiceService : IInvoiceService
{
    private const string InventoryAccount = "Inventory";
    private const string GdniAccount = "Goods Delivered Not Invoiced";
    private const string CogsAccount = "Cost of Goods Sold";
    private const string SalesRevenueAccount = "Sales";
    private const string TaxPayableAccount = "Tax Payable";
    private const string AccountsReceivableAccount = "Accounts Receivable";
    private const string CashAccount = "Cash";
    private const string RoundOffAccount = "Round Off";

    private const int ItemLedgerType = 1;
    private const int TaxLedgerType = 2;
    private const int ControlLedgerType = 3;
    private const int CogsLedgerType = 4;
    private const int RoundOffLedgerType = 6;
    private const int TransactionLedgerSource = 3;

    private const int ContactReference = 1;
    private const int TaxReference = 3;

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
    private readonly ILedgerClient _ledgerClient;
    private readonly ICreditCheckClient _creditCheckClient;

    public InvoiceService(
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
        ILedgerClient ledgerClient,
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
        _ledgerClient = ledgerClient;
        _creditCheckClient = creditCheckClient;
    }

    public async Task<InvoiceResult> CreateAsync(SaveInvoiceRequest request, CancellationToken ct)
    {
        string? baseCurrency = await _baseCurrency.GetBaseCurrencyAsync(ct);
        if (baseCurrency is null)
        {
            return new InvoiceResult(
                InvoiceOutcome.RatesUnavailable, Detail: "Branch base currency could not be read.");
        }

        BranchSettings? settings = await _branchSettings.GetSettingsAsync(ct);
        if (settings is null)
        {
            return new InvoiceResult(
                InvoiceOutcome.RatesUnavailable, Detail: "Branch settings could not be read.");
        }

        PlaceOfSupplyResult pos = PlaceOfSupply.Resolve(
            settings.StateCode, request.PlaceOfSupplyStateCode, request.ContactGstin);

        if (!pos.IsOk)
        {
            return new InvoiceResult(InvoiceOutcome.PlaceOfSupplyRefused, Detail: pos.Detail);
        }

        bool isPos = request.TillId.HasValue;
        if (!isPos && request.DueDate is null)
        {
            return new InvoiceResult(
                InvoiceOutcome.DueDateMissing,
                Detail: "An invoice requires a due date. Choose a payment term or set the due date directly.");
        }

        string typeCode = isPos ? "POS" : "INV";
        TaxContext taxContext = new(pos.IsInterState, settings.DiscountBeforeTax);

        NumberAllocation alloc = await _numbering.NextAsync(typeCode, request.DocumentDate, ct);

        Invoice invoice = new()
        {
            TransactionTypeCode = typeCode,
            DocumentNo = alloc.Code,
            DocumentDate = request.DocumentDate,
            DueDate = request.DueDate,
            QuoteId = request.QuoteId,
            SalesOrderId = request.SalesOrderId,
            DeliveryChallanId = request.DeliveryChallanId,
            PaymentTermId = request.PaymentTermId,
            TillId = request.TillId,
            CashierUserId = request.CashierUserId,
            PaymentMode = request.PaymentMode,
            TenderedAmount = request.TenderedAmount,
            ChangeAmount = request.ChangeAmount,
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
            Status = DocumentStatus.Draft,
        };

        var taxLines = new List<TaxLineResult>(request.Lines.Count);

        for (int i = 0; i < request.Lines.Count; i++)
        {
            SaveInvoiceLineRequest lineReq = request.Lines[i];
            int lineNumber = i + 1;

            if (lineReq.ItemId is null && string.IsNullOrWhiteSpace(lineReq.Description))
            {
                return new InvoiceResult(
                    InvoiceOutcome.LineInvalid,
                    Detail: $"Line {lineNumber} is a free-text line and must have a description.");
            }
            if (lineReq.ItemId is null && lineReq.AccountId is null)
            {
                return new InvoiceResult(
                    InvoiceOutcome.LineInvalid,
                    Detail: $"Line {lineNumber} is a free-text line and must have an account selected.");
            }

            long? taxGroupId = lineReq.TaxGroupId
                ?? (lineReq.TaxGroupIds.Count > 0 ? lineReq.TaxGroupIds[0] : null);

            TaxRate? rate = null;
            if (taxGroupId.HasValue)
            {
                rate = await _rates.GetRateAsync(taxGroupId.Value, request.DocumentDate, ct);
                if (rate is null)
                {
                    return new InvoiceResult(
                        InvoiceOutcome.RatesUnavailable,
                        Detail: $"Tax rate for group {taxGroupId.Value} could not be read for date {request.DocumentDate}.");
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

            InvoiceDetail detail = new()
            {
                LineNumber = lineNumber,
                SalesOrderDetailId = lineReq.SalesOrderDetailId,
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
                TaxGroupId = rate?.TaxGroupId ?? taxGroupId,
                TaxAmount = computed.TaxAmount,
                LineType = lineReq.LineType,
                AccountId = lineReq.AccountId,
                FixedAssetCategoryId = lineReq.FixedAssetCategoryId,
                LineTotal = computed.LineTotal,
                ItemBatchId = lineReq.ItemBatchId,
                LineNotes = lineReq.LineNotes,
                ReturnedQuantity = 0m,
            };

            foreach (var comp in computed.Components)
            {
                detail.Taxes.Add(new InvoiceDetailTax
                {
                    TaxComponent = comp.Component,
                    Rate = comp.Rate,
                    TaxableAmount = comp.TaxableAmount,
                    Amount = comp.Amount,
                    AmountBase = comp.Amount * invoice.ExchangeRate,
                });
            }

            invoice.Lines.Add(detail);
        }

        TaxDocumentTotals totals = GstCalculator.Totals(taxLines);

        invoice.SubTotal = totals.SubTotal;
        invoice.DiscountAmount = totals.DiscountAmount;
        invoice.TaxableAmount = totals.TaxableAmount;
        invoice.CgstAmount = totals.CgstAmount;
        invoice.SgstAmount = totals.SgstAmount;
        invoice.IgstAmount = totals.IgstAmount;
        invoice.CessAmount = totals.CessAmount;
        invoice.RoundOffAmount =
            Math.Round(totals.TotalAmount, 0, MidpointRounding.AwayFromZero) - totals.TotalAmount;
        invoice.TotalAmount = totals.TotalAmount + invoice.RoundOffAmount;
        invoice.TotalAmountBase = invoice.TotalAmount * invoice.ExchangeRate;

        var eval = await _creditCheckClient.EvaluateAsync(
            invoice.ContactId, invoice.TotalAmountBase, ct);
        if (!eval.Allowed)
        {
            return new InvoiceResult(InvoiceOutcome.CreditLimitExceeded, Detail: eval.Reason);
        }

        var detailLines = invoice.Lines.ToList();
        invoice.Lines.Clear();

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(ct);

        foreach (var detail in detailLines)
        {
            detail.InvoiceId = invoice.InvoiceId;
            var detailTaxes = detail.Taxes.ToList();
            detail.Taxes.Clear();

            _db.InvoiceDetails.Add(detail);
            await _db.SaveChangesAsync(ct);

            foreach (var tax in detailTaxes)
            {
                tax.InvoiceDetailId = detail.InvoiceDetailId;
                _db.InvoiceDetailTaxes.Add(tax);
            }
            await _db.SaveChangesAsync(ct);
            detail.Taxes.AddRange(detailTaxes);
        }

        invoice.Lines.AddRange(detailLines);
        return new InvoiceResult(InvoiceOutcome.Ok, invoice.InvoiceId);
    }

    public async Task<InvoiceResult> UpdateAsync(
        long invoiceId, SaveInvoiceRequest request, CancellationToken ct)
    {
        Invoice? invoice = await _db.Invoices
            .Include(x => x.Lines)
            .ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(x => x.InvoiceId == invoiceId, ct);

        if (invoice is null)
        {
            return new InvoiceResult(InvoiceOutcome.NotFound);
        }

        DocumentTransition transition = DocumentLifecycle.CanEdit(invoice.Status);
        if (!transition.IsAllowed)
        {
            return new InvoiceResult(
                InvoiceOutcome.LifecycleRefused, Detail: "Only draft invoices can be updated.");
        }

        BranchSettings? settings = await _branchSettings.GetSettingsAsync(ct);
        if (settings is null)
        {
            return new InvoiceResult(
                InvoiceOutcome.RatesUnavailable, Detail: "Branch settings could not be read.");
        }

        PlaceOfSupplyResult pos = PlaceOfSupply.Resolve(
            settings.StateCode, request.PlaceOfSupplyStateCode, request.ContactGstin);

        if (!pos.IsOk)
        {
            return new InvoiceResult(InvoiceOutcome.PlaceOfSupplyRefused, Detail: pos.Detail);
        }

        bool isPos = request.TillId.HasValue || invoice.TransactionTypeCode == "POS";
        if (!isPos && request.DueDate is null)
        {
            return new InvoiceResult(
                InvoiceOutcome.DueDateMissing,
                Detail: "An invoice requires a due date. Choose a payment term or set the due date directly.");
        }

        TaxContext taxContext = new(pos.IsInterState, settings.DiscountBeforeTax);

        invoice.DocumentDate = request.DocumentDate;
        invoice.DueDate = request.DueDate;
        invoice.QuoteId = request.QuoteId;
        invoice.SalesOrderId = request.SalesOrderId;
        invoice.DeliveryChallanId = request.DeliveryChallanId;
        invoice.PaymentTermId = request.PaymentTermId;
        invoice.TillId = request.TillId;
        invoice.CashierUserId = request.CashierUserId;
        invoice.PaymentMode = request.PaymentMode;
        invoice.TenderedAmount = request.TenderedAmount;
        invoice.ChangeAmount = request.ChangeAmount;
        invoice.ContactId = request.ContactId;
        invoice.ContactGstin = request.ContactGstin;
        invoice.BillingAddress = request.BillingAddress;
        invoice.ShippingAddress = request.ShippingAddress;
        invoice.IsInterState = pos.IsInterState;

        if (request.CurrencyCode is not null)
        {
            invoice.CurrencyCode = request.CurrencyCode;
        }
        if (request.ExchangeRate.HasValue)
        {
            invoice.ExchangeRate = request.ExchangeRate.Value;
        }

        invoice.Notes = request.Notes;
        invoice.TermsAndConditions = request.TermsAndConditions;

        var existingLines = await _db.InvoiceDetails
            .Where(d => d.InvoiceId == invoiceId)
            .Include(d => d.Taxes)
            .ToListAsync(ct);

        _db.InvoiceDetailTaxes.RemoveRange(existingLines.SelectMany(l => l.Taxes));
        _db.InvoiceDetails.RemoveRange(existingLines);
        invoice.Lines.Clear();
        await _db.SaveChangesAsync(ct);

        var taxLines = new List<TaxLineResult>(request.Lines.Count);
        var newDetails = new List<InvoiceDetail>(request.Lines.Count);

        for (int i = 0; i < request.Lines.Count; i++)
        {
            SaveInvoiceLineRequest lineReq = request.Lines[i];
            int lineNumber = i + 1;

            if (lineReq.ItemId is null && string.IsNullOrWhiteSpace(lineReq.Description))
            {
                return new InvoiceResult(
                    InvoiceOutcome.LineInvalid,
                    Detail: $"Line {lineNumber} is a free-text line and must have a description.");
            }
            if (lineReq.ItemId is null && lineReq.AccountId is null)
            {
                return new InvoiceResult(
                    InvoiceOutcome.LineInvalid,
                    Detail: $"Line {lineNumber} is a free-text line and must have an account selected.");
            }

            long? taxGroupId = lineReq.TaxGroupId
                ?? (lineReq.TaxGroupIds.Count > 0 ? lineReq.TaxGroupIds[0] : null);

            TaxRate? rate = null;
            if (taxGroupId.HasValue)
            {
                rate = await _rates.GetRateAsync(taxGroupId.Value, request.DocumentDate, ct);
                if (rate is null)
                {
                    return new InvoiceResult(
                        InvoiceOutcome.RatesUnavailable,
                        Detail: $"Tax rate for group {taxGroupId.Value} could not be read for date {request.DocumentDate}.");
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

            InvoiceDetail detail = new()
            {
                LineNumber = lineNumber,
                SalesOrderDetailId = lineReq.SalesOrderDetailId,
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
                TaxGroupId = rate?.TaxGroupId ?? taxGroupId,
                TaxAmount = computed.TaxAmount,
                LineType = lineReq.LineType,
                AccountId = lineReq.AccountId,
                FixedAssetCategoryId = lineReq.FixedAssetCategoryId,
                LineTotal = computed.LineTotal,
                ItemBatchId = lineReq.ItemBatchId,
                LineNotes = lineReq.LineNotes,
                ReturnedQuantity = 0m,
            };

            foreach (var comp in computed.Components)
            {
                detail.Taxes.Add(new InvoiceDetailTax
                {
                    TaxComponent = comp.Component,
                    Rate = comp.Rate,
                    TaxableAmount = comp.TaxableAmount,
                    Amount = comp.Amount,
                    AmountBase = comp.Amount * invoice.ExchangeRate,
                });
            }

            newDetails.Add(detail);
        }

        TaxDocumentTotals totals = GstCalculator.Totals(taxLines);

        invoice.SubTotal = totals.SubTotal;
        invoice.DiscountAmount = totals.DiscountAmount;
        invoice.TaxableAmount = totals.TaxableAmount;
        invoice.CgstAmount = totals.CgstAmount;
        invoice.SgstAmount = totals.SgstAmount;
        invoice.IgstAmount = totals.IgstAmount;
        invoice.CessAmount = totals.CessAmount;
        invoice.RoundOffAmount =
            Math.Round(totals.TotalAmount, 0, MidpointRounding.AwayFromZero) - totals.TotalAmount;
        invoice.TotalAmount = totals.TotalAmount + invoice.RoundOffAmount;
        invoice.TotalAmountBase = invoice.TotalAmount * invoice.ExchangeRate;

        var eval = await _creditCheckClient.EvaluateAsync(
            invoice.ContactId, invoice.TotalAmountBase, ct);
        if (!eval.Allowed)
        {
            return new InvoiceResult(InvoiceOutcome.CreditLimitExceeded, Detail: eval.Reason);
        }

        await _db.SaveChangesAsync(ct);

        foreach (var detail in newDetails)
        {
            detail.InvoiceId = invoice.InvoiceId;
            var detailTaxes = detail.Taxes.ToList();
            detail.Taxes.Clear();

            _db.InvoiceDetails.Add(detail);
            await _db.SaveChangesAsync(ct);

            foreach (var tax in detailTaxes)
            {
                tax.InvoiceDetailId = detail.InvoiceDetailId;
                _db.InvoiceDetailTaxes.Add(tax);
            }
            await _db.SaveChangesAsync(ct);
            detail.Taxes.AddRange(detailTaxes);
        }

        invoice.Lines.AddRange(newDetails);
        return new InvoiceResult(InvoiceOutcome.Ok, invoice.InvoiceId);
    }

    public async Task<InvoiceResult> SaveAsync(
        SaveInvoiceRequest request, long? invoiceId, CancellationToken ct)
    {
        if (invoiceId.HasValue && invoiceId.Value > 0)
        {
            return await UpdateAsync(invoiceId.Value, request, ct);
        }

        return await CreateAsync(request, ct);
    }

    public async Task<InvoiceView?> GetAsync(long invoiceId, CancellationToken ct)
    {
        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        var invoice = await _db.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.InvoiceId == invoiceId, ct);

        if (invoice is null) return null;

        var lines = await _db.InvoiceDetails
            .Where(d => d.InvoiceId == invoiceId)
            .AsNoTracking()
            .OrderBy(d => d.LineNumber)
            .ToListAsync(ct);

        var detailIds = lines.Select(l => l.InvoiceDetailId).ToList();
        var taxes = await _db.InvoiceDetailTaxes
            .Where(t => detailIds.Contains(t.InvoiceDetailId))
            .AsNoTracking()
            .ToListAsync(ct);

        foreach (var line in lines)
        {
            line.Taxes = taxes.Where(t => t.InvoiceDetailId == line.InvoiceDetailId).ToList();
        }

        invoice.Lines = lines;

        var contacts = await _contactNames.ResolveAsync([invoice.ContactId], ct);
        var itemIds = invoice.Lines
            .Where(l => l.ItemId.HasValue)
            .Select(l => l.ItemId!.Value)
            .Distinct()
            .ToList();
        var items = itemIds.Count > 0
            ? await _itemNames.ResolveAsync(itemIds, ct)
            : new Dictionary<long, NamedRef>();

        var view = new InvoiceView
        {
            InvoiceId = invoice.InvoiceId,
            QuoteId = invoice.QuoteId,
            SalesOrderId = invoice.SalesOrderId,
            DeliveryChallanId = invoice.DeliveryChallanId,
            PaymentTermId = invoice.PaymentTermId,
            DueDate = invoice.DueDate,
            TillId = invoice.TillId,
            CashierUserId = invoice.CashierUserId,
            PaymentMode = invoice.PaymentMode,
            TenderedAmount = invoice.TenderedAmount,
            ChangeAmount = invoice.ChangeAmount,
            DocumentDate = invoice.DocumentDate,
            DocumentNo = invoice.DocumentNo,
            Notes = invoice.Notes,
            TermsAndConditions = invoice.TermsAndConditions,
            Status = invoice.Status.ToString(),
            CurrencyCode = invoice.CurrencyCode,
            ExchangeRate = invoice.ExchangeRate,
            ContactId = invoice.ContactId,
            ContactName = contacts.TryGetValue(invoice.ContactId, out var c) ? c.Name : null,
            ContactCode = contacts.TryGetValue(invoice.ContactId, out var cCode) ? cCode.Code : null,
            ContactGstin = invoice.ContactGstin,
            PlaceOfSupplyStateId = invoice.PlaceOfSupplyStateId,
            BillingAddress = invoice.BillingAddress,
            ShippingAddress = invoice.ShippingAddress,
            IsInterState = invoice.IsInterState,
            SubTotal = invoice.SubTotal,
            DiscountAmount = invoice.DiscountAmount,
            TaxableAmount = invoice.TaxableAmount,
            CgstAmount = invoice.CgstAmount,
            SgstAmount = invoice.SgstAmount,
            IgstAmount = invoice.IgstAmount,
            CessAmount = invoice.CessAmount,
            RoundOffAmount = invoice.RoundOffAmount,
            TotalAmount = invoice.TotalAmount,
            TotalAmountBase = invoice.TotalAmountBase,
            PostedAt = invoice.PostedAt,
            VoidedAt = invoice.VoidedAt,
            VoidReason = invoice.VoidReason,
            DaysOverdue = invoice.Status == DocumentStatus.Posted && invoice.DueDate.HasValue
                ? Math.Max(0, today.DayNumber - invoice.DueDate.Value.DayNumber)
                : 0,
        };

        foreach (var line in invoice.Lines.OrderBy(l => l.LineNumber))
        {
            var lineView = new InvoiceLineView
            {
                InvoiceDetailId = line.InvoiceDetailId,
                LineNumber = line.LineNumber,
                SalesOrderDetailId = line.SalesOrderDetailId,
                ItemId = line.ItemId,
                ItemLabel = (line.ItemId.HasValue && items.TryGetValue(line.ItemId.Value, out var item))
                    ? $"{item.Code} - {item.Name}"
                    : null,
                Description = line.Description,
                HsnSacCode = line.HsnSacCode,
                WarehouseId = line.WarehouseId,
                Quantity = line.Quantity,
                UomId = line.UomId,
                ConversionFactor = line.ConversionFactor,
                BaseQuantity = line.BaseQuantity,
                ReturnedQuantity = line.ReturnedQuantity,
                UnitPrice = line.UnitPrice,
                IsPriceInclusive = line.IsPriceInclusive,
                DiscountPercent = line.DiscountPercent,
                DiscountAmount = line.DiscountAmount,
                GrossAmount = line.GrossAmount,
                TaxableAmount = line.TaxableAmount,
                TaxTreatment = line.TaxTreatment.ToString(),
                TaxMasterId = line.TaxMasterId,
                TaxGroupId = line.TaxGroupId,
                TaxAmount = line.TaxAmount,
                LineType = line.LineType.ToString(),
                AccountId = line.AccountId,
                FixedAssetCategoryId = line.FixedAssetCategoryId,
                LineTotal = line.LineTotal,
                ItemBatchId = line.ItemBatchId,
                LineNotes = line.LineNotes,
            };

            foreach (var tax in line.Taxes)
            {
                lineView.Taxes.Add(new InvoiceLineTaxView
                {
                    InvoiceDetailTaxId = tax.InvoiceDetailTaxId,
                    TaxComponent = tax.TaxComponent.ToString(),
                    SubAccountId = tax.SubAccountId,
                    Rate = tax.Rate,
                    TaxableAmount = tax.TaxableAmount,
                    Amount = tax.Amount,
                    AmountBase = tax.AmountBase,
                });
            }

            view.Lines.Add(lineView);
        }

        return view;
    }

    public async Task<List<InvoiceListItem>> ListAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        var query = _db.Invoices.AsNoTracking();

        if (from.HasValue) query = query.Where(x => x.DocumentDate >= from.Value);
        if (to.HasValue) query = query.Where(x => x.DocumentDate <= to.Value);

        var list = await query
            .OrderByDescending(x => x.DocumentDate)
            .ThenByDescending(x => x.DocumentNo)
            .Select(x => new InvoiceListItem
            {
                InvoiceId = x.InvoiceId,
                DocumentNo = x.DocumentNo,
                DocumentDate = x.DocumentDate,
                DueDate = x.DueDate,
                QuoteId = x.QuoteId,
                SalesOrderId = x.SalesOrderId,
                DeliveryChallanId = x.DeliveryChallanId,
                ContactId = x.ContactId,
                CurrencyCode = x.CurrencyCode,
                TaxableAmount = x.TaxableAmount,
                TotalAmount = x.TotalAmount,
                Status = x.Status.ToString(),
                IsInterState = x.IsInterState,
                DaysOverdue = x.Status == DocumentStatus.Posted && x.DueDate.HasValue
                    ? Math.Max(0, today.DayNumber - x.DueDate.Value.DayNumber)
                    : 0,
                PaymentMode = x.PaymentMode,
            })
            .ToListAsync(ct);

        var contactIds = list.Select(x => x.ContactId).Distinct().ToList();
        if (contactIds.Count > 0)
        {
            var contacts = await _contactNames.ResolveAsync(contactIds, ct);
            foreach (var item in list)
            {
                if (contacts.TryGetValue(item.ContactId, out var c))
                {
                    item.ContactName = c.Name;
                    item.ContactCode = c.Code;
                }
            }
        }

        return list;
    }

    public Task<List<InvoiceListItem>> ListAsync(CancellationToken ct) =>
        ListAsync(null, null, ct);

    public async Task<GlPreviewResult?> PreviewGlAsync(long invoiceId, CancellationToken ct)
    {
        var invoice = await _db.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.InvoiceId == invoiceId, ct);

        if (invoice is null)
        {
            return null;
        }

        var lines = await _db.InvoiceDetails
            .Where(d => d.InvoiceId == invoiceId)
            .AsNoTracking()
            .OrderBy(d => d.LineNumber)
            .ToListAsync(ct);

        var detailIds = lines.Select(l => l.InvoiceDetailId).ToList();
        var detailTaxes = await _db.InvoiceDetailTaxes
            .Where(t => detailIds.Contains(t.InvoiceDetailId))
            .AsNoTracking()
            .ToListAsync(ct);

        foreach (var line in lines)
        {
            line.Taxes = detailTaxes.Where(t => t.InvoiceDetailId == line.InvoiceDetailId).ToList();
        }

        invoice.Lines = lines;

        var legs = new List<GlEntryLegView>();
        bool isTill = invoice.TillId.HasValue;

        // Debit Accounts Receivable or Cash (CONTROL leg)
        legs.Add(new GlEntryLegView
        {
            LedgerTypeId = ControlLedgerType,
            AccountName = isTill ? CashAccount : AccountsReceivableAccount,
            SubAccountName = isTill ? null : $"Contact {invoice.ContactId}",
            DebitAmount = invoice.TotalAmount,
            CreditAmount = 0m,
            Description = isTill ? "Cash sale" : "Receivable from customer",
        });

        // Credit Sales Revenue (ITEM leg)
        decimal netSales = invoice.SubTotal - invoice.DiscountAmount;
        if (netSales > 0)
        {
            legs.Add(new GlEntryLegView
            {
                LedgerTypeId = ItemLedgerType,
                AccountName = SalesRevenueAccount,
                DebitAmount = 0m,
                CreditAmount = netSales,
                Description = "Sales revenue",
            });
        }

        // Credit Tax Payable (TAX legs)
        var taxGroups = invoice.Lines.SelectMany(l => l.Taxes)
            .GroupBy(t => new { t.SubAccountId, t.TaxComponent })
            .Select(g => new
            {
                g.Key.SubAccountId,
                g.Key.TaxComponent,
                Amount = g.Sum(t => t.Amount),
            });

        foreach (var tax in taxGroups.Where(t => t.Amount > 0))
        {
            legs.Add(new GlEntryLegView
            {
                LedgerTypeId = TaxLedgerType,
                AccountName = TaxPayableAccount,
                SubAccountName = $"{tax.TaxComponent} (SubAccount {tax.SubAccountId})",
                DebitAmount = 0m,
                CreditAmount = tax.Amount,
                Description = $"Output {tax.TaxComponent}",
            });
        }

        // Round Off leg
        if (invoice.RoundOffAmount != 0)
        {
            legs.Add(new GlEntryLegView
            {
                LedgerTypeId = RoundOffLedgerType,
                AccountName = RoundOffAccount,
                DebitAmount = invoice.RoundOffAmount < 0 ? -invoice.RoundOffAmount : 0m,
                CreditAmount = invoice.RoundOffAmount > 0 ? invoice.RoundOffAmount : 0m,
                Description = "Round off difference",
            });
        }

        decimal totalDebit = legs.Sum(l => l.DebitAmount);
        decimal totalCredit = legs.Sum(l => l.CreditAmount);

        return new GlPreviewResult
        {
            Legs = legs,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            IsBalanced = Math.Round(totalDebit, 2) == Math.Round(totalCredit, 2),
        };
    }

    public async Task<InvoiceResult> PostAsync(long invoiceId, CancellationToken ct)
    {
        (Guid customerId, Guid orgId) = _tenant.Require();

        Invoice? invoice = await _db.Invoices
            .FirstOrDefaultAsync(x => x.InvoiceId == invoiceId, ct);

        if (invoice is null)
        {
            return new InvoiceResult(InvoiceOutcome.NotFound);
        }

        var lines = await _db.InvoiceDetails
            .Where(d => d.InvoiceId == invoiceId)
            .OrderBy(d => d.LineNumber)
            .ToListAsync(ct);

        var detailIds = lines.Select(l => l.InvoiceDetailId).ToList();
        var detailTaxes = await _db.InvoiceDetailTaxes
            .Where(t => detailIds.Contains(t.InvoiceDetailId))
            .ToListAsync(ct);

        foreach (var line in lines)
        {
            line.Taxes = detailTaxes.Where(t => t.InvoiceDetailId == line.InvoiceDetailId).ToList();
        }

        invoice.Lines = lines;

        DocumentTransition transition = DocumentLifecycle.CanPost(invoice.Status, invoice.Lines.Count);
        if (!transition.IsAllowed)
        {
            return new InvoiceResult(InvoiceOutcome.LifecycleRefused, Detail: transition.Detail);
        }

        decimal totalCogs = 0;

        if (invoice.DeliveryChallanId.HasValue)
        {
            var challan = await _db.DeliveryChallans
                .Include(c => c.Lines)
                .FirstOrDefaultAsync(c => c.DeliveryChallanId == invoice.DeliveryChallanId.Value, ct);

            if (challan is not null)
            {
                foreach (var line in invoice.Lines)
                {
                    var challanLine = challan.Lines.FirstOrDefault(l => l.ItemId == line.ItemId);
                    if (challanLine is not null)
                    {
                        line.UnitCost = challanLine.UnitCost;
                        line.StockMovementId = challanLine.StockMovementId;
                        totalCogs += line.UnitCost * line.Quantity;
                    }
                }
            }
        }
        else
        {
            var stockLines = invoice.Lines
                .Where(l => l.LineType == DocumentLineType.Stock && l.ItemId.HasValue)
                .ToList();

            if (stockLines.Count > 0)
            {
                var issueRequest = new IssueStockRequest
                {
                    OrgId = invoice.OrgId,
                    CustomerId = customerId,
                    MovementDate = invoice.DocumentDate,
                    SourceType = invoice.TransactionTypeCode,
                    SourceId = invoice.InvoiceId,
                    Lines = stockLines.Select(l => new IssueStockLine
                    {
                        SourceLineId = l.InvoiceDetailId,
                        ItemId = l.ItemId!.Value,
                        Quantity = l.Quantity,
                        WarehouseId = l.WarehouseId,
                        ReleaseReservation = invoice.SalesOrderId.HasValue,
                    }).ToList(),
                };

                var issueResult = await _inventoryClient.IssueAsync(issueRequest, ct);
                if (!issueResult.Success)
                {
                    return new InvoiceResult(InvoiceOutcome.StockRefused, Detail: "Stock issue failed.");
                }

                foreach (var issueLine in issueResult.Lines)
                {
                    var line = invoice.Lines.FirstOrDefault(l => l.InvoiceDetailId == issueLine.SourceLineId);
                    if (line is not null)
                    {
                        line.StockMovementId = issueLine.StockMovementId;
                        line.UnitCost = issueLine.UnitCost;
                    }
                }

                totalCogs = issueResult.TotalValue;
            }
        }

        // 2. Post Ledger
        var baseCurrency = await _baseCurrency.GetBaseCurrencyAsync(ct);
        var postRequest = new PostLedgerRequest
        {
            CustomerId = customerId,
            OrgId = invoice.OrgId,
            TransactionTypeCode = invoice.TransactionTypeCode,
            TransactionId = invoice.InvoiceId,
            LedgerDate = invoice.DocumentDate,
            CurrencyCode = invoice.CurrencyCode == baseCurrency ? null : invoice.CurrencyCode,
            ExchangeRate = invoice.CurrencyCode == baseCurrency ? null : invoice.ExchangeRate,
            ContactId = invoice.ContactId,
            SourceDocumentId = invoice.InvoiceId,
            Legs = new List<LedgerLegRequest>(),
        };

        decimal totalAmount = invoice.TotalAmount;
        decimal totalRevenue = invoice.SubTotal - invoice.DiscountAmount;

        // Debit Accounts Receivable / Cash (CONTROL leg, type 3)
        postRequest.Legs.Add(new LedgerLegRequest
        {
            LedgerTypeId = ControlLedgerType,
            LedgerSourceId = TransactionLedgerSource,
            TransactionDetailId = 0,
            AccountSystemName = invoice.TillId.HasValue ? CashAccount : AccountsReceivableAccount,
            SubAccountReferenceType = invoice.TillId.HasValue ? null : ContactReference,
            SubAccountReferenceId = invoice.TillId.HasValue ? null : invoice.ContactId,
            SubAccountPurpose = 0,
            DebitAmount = totalAmount,
            TransactionDesc = invoice.TillId.HasValue ? "Cash sale" : "Receivable from customer",
        });

        // Credit Sales Revenue (ITEM leg, type 1)
        if (totalRevenue > 0)
        {
            postRequest.Legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = ItemLedgerType,
                LedgerSourceId = TransactionLedgerSource,
                TransactionDetailId = 0,
                AccountSystemName = SalesRevenueAccount,
                CreditAmount = totalRevenue,
                TransactionDesc = "Sales revenue",
            });
        }

        // Credit Tax Payable (TAX legs, type 2)
        var taxGroups = invoice.Lines.SelectMany(l => l.Taxes)
            .GroupBy(t => new { t.SubAccountId, t.TaxComponent })
            .Select(g => new
            {
                g.Key.SubAccountId,
                g.Key.TaxComponent,
                Amount = g.Sum(t => t.Amount),
            });

        foreach (var tax in taxGroups.Where(t => t.Amount > 0))
        {
            int subAccountTaxComp = tax.TaxComponent switch
            {
                TaxComponent.Cgst => 1,
                TaxComponent.Sgst => 2,
                TaxComponent.Igst => 3,
                TaxComponent.Cess => 4,
                _ => 1,
            };

            postRequest.Legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = TaxLedgerType,
                LedgerSourceId = TransactionLedgerSource,
                TransactionDetailId = 0,
                SubAccountReferenceType = TaxReference,
                SubAccountReferenceId = tax.SubAccountId,
                SubAccountTaxComponent = subAccountTaxComp,
                AccountSystemName = TaxPayableAccount,
                CreditAmount = tax.Amount,
                TransactionDesc = $"Output {tax.TaxComponent}",
            });
        }

        // Round Off leg
        if (invoice.RoundOffAmount != 0)
        {
            postRequest.Legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = RoundOffLedgerType,
                LedgerSourceId = TransactionLedgerSource,
                TransactionDetailId = 0,
                AccountSystemName = RoundOffAccount,
                DebitAmount = invoice.RoundOffAmount < 0 ? -invoice.RoundOffAmount : 0m,
                CreditAmount = invoice.RoundOffAmount > 0 ? invoice.RoundOffAmount : 0m,
                TransactionDesc = "Rounding",
            });
        }

        if (totalCogs > 0)
        {
            postRequest.Legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = CogsLedgerType,
                LedgerSourceId = TransactionLedgerSource,
                TransactionDetailId = 0,
                AccountSystemName = CogsAccount,
                DebitAmount = totalCogs,
                TransactionDesc = "Cost of goods sold",
            });

            postRequest.Legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = ControlLedgerType,
                LedgerSourceId = TransactionLedgerSource,
                TransactionDetailId = 0,
                AccountSystemName = invoice.DeliveryChallanId.HasValue ? GdniAccount : InventoryAccount,
                CreditAmount = totalCogs,
                TransactionDesc = "Inventory relief",
            });
        }

        var result = await _ledgerClient.PostAsync(postRequest, ct);
        if (!result.Posted)
        {
            return new InvoiceResult(InvoiceOutcome.PostingRefused, Detail: result.Detail);
        }

        // 3. Sales Register Synchronous Insertion
        foreach (var l in invoice.Lines)
        {
            var rate = l.Taxes.FirstOrDefault()?.Rate ?? 0;
            _db.SalesRegister.Add(new SalesRegister
            {
                OrgId = invoice.OrgId,
                TransactionTypeCode = invoice.TransactionTypeCode,
                SourceId = invoice.InvoiceId,
                DocumentNo = invoice.DocumentNo,
                DocumentDate = invoice.DocumentDate,
                ContactId = invoice.ContactId,
                ContactGstin = invoice.ContactGstin,
                PlaceOfSupplyStateId = invoice.PlaceOfSupplyStateId,
                IsInterState = invoice.IsInterState,
                SupplyType = invoice.ContactGstin != null ? "B2B" : "B2CS",
                ReverseCharge = false,
                HsnSacCode = l.HsnSacCode,
                GstRate = rate,
                Quantity = l.Quantity,
                UqcCode = null,
                TaxableAmount = l.TaxableAmount,
                CgstAmount = l.Taxes.FirstOrDefault(t => t.TaxComponent == TaxComponent.Cgst)?.Amount ?? 0,
                SgstAmount = l.Taxes.FirstOrDefault(t => t.TaxComponent == TaxComponent.Sgst)?.Amount ?? 0,
                IgstAmount = l.Taxes.FirstOrDefault(t => t.TaxComponent == TaxComponent.Igst)?.Amount ?? 0,
                CessAmount = l.Taxes.FirstOrDefault(t => t.TaxComponent == TaxComponent.Cess)?.Amount ?? 0,
                TotalAmount = l.LineTotal,
                CurrencyCode = invoice.CurrencyCode,
                ExchangeRate = invoice.ExchangeRate,
                TaxableAmountBase = l.TaxableAmount * invoice.ExchangeRate,
            });
        }

        invoice.Status = DocumentStatus.Posted;
        invoice.PostedAt = _clock.GetUtcNow();
        invoice.PostedBy = _user.UserId;

        await _db.SaveChangesAsync(ct);
        return new InvoiceResult(InvoiceOutcome.Ok, invoice.InvoiceId);
    }

    public Task<InvoiceResult> VoidAsync(long invoiceId, CancellationToken ct) =>
        VoidAsync(invoiceId, new VoidInvoiceRequest { Reason = "Voided by user." }, ct);

    public async Task<InvoiceResult> VoidAsync(
        long invoiceId, VoidInvoiceRequest request, CancellationToken ct)
    {
        Invoice? invoice = await _db.Invoices
            .FirstOrDefaultAsync(x => x.InvoiceId == invoiceId, ct);

        if (invoice is null)
        {
            return new InvoiceResult(InvoiceOutcome.NotFound);
        }

        bool hasCreditNote = await _db.CreditNotes.AnyAsync(c => c.InvoiceId == invoiceId, ct);

        DocumentTransition transition =
            DocumentLifecycle.CanVoid(invoice.Status, hasCreditNote, request.Reason);

        if (!transition.IsAllowed)
        {
            return transition.Outcome == DocumentTransitionOutcome.HasDownstream
                ? new InvoiceResult(InvoiceOutcome.AlreadyCredited, Detail: transition.Detail)
                : new InvoiceResult(InvoiceOutcome.LifecycleRefused, Detail: transition.Detail);
        }

        invoice.Status = DocumentStatus.Void;
        invoice.VoidedAt = _clock.GetUtcNow();
        invoice.VoidedBy = _user.UserId;
        invoice.VoidReason = request.Reason;

        if (invoice.PostedAt is not null)
        {
            (Guid customerId, Guid orgId) = _tenant.Require();

            var withdrawResult = await _ledgerClient.PostAsync(
                new PostLedgerRequest
                {
                    CustomerId = customerId,
                    OrgId = orgId,
                    TransactionTypeCode = invoice.TransactionTypeCode,
                    TransactionId = invoice.InvoiceId,
                    LedgerDate = invoice.DocumentDate,
                    ContactId = invoice.ContactId,
                    SourceDocumentId = invoice.InvoiceId,
                    WithdrawLedgerTypeIds = [ItemLedgerType, TaxLedgerType, ControlLedgerType, CogsLedgerType, RoundOffLedgerType],
                    Legs = [],
                },
                ct);

            if (!withdrawResult.Posted)
            {
                return new InvoiceResult(InvoiceOutcome.PostingRefused, Detail: withdrawResult.Detail);
            }

            var registers = await _db.SalesRegister
                .Where(r => r.SourceId == invoiceId && r.TransactionTypeCode == invoice.TransactionTypeCode)
                .ToListAsync(ct);
            _db.SalesRegister.RemoveRange(registers);
        }

        await _db.SaveChangesAsync(ct);
        return new InvoiceResult(InvoiceOutcome.Ok, invoice.InvoiceId);
    }

    public async Task<bool> ExistsInOtherOrgAsync(long invoiceId, CancellationToken ct)
    {
        Guid? currentOrgId = _tenant.OrgId;
        if (!currentOrgId.HasValue)
        {
            return false;
        }

        return await _db.Invoices
            .IgnoreQueryFilters()
            .AnyAsync(x => x.InvoiceId == invoiceId && x.OrgId != currentOrgId.Value, ct);
    }
}
