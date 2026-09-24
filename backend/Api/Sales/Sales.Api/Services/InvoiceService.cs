using Microsoft.EntityFrameworkCore;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tax;
using Shared.Kernel.Tenancy;
using Shared.Kernel.Storage;

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
    // The chart's own names — SystemAccountNames in Accounting. The ledger finds
    // an account by exact name, and "Sales" and "Tax Payable", which these were,
    // exist in no chart, so every invoice post was refused (TK-77).
    // SalesAccountNameTests holds every name here to Accounting's seed.
    private const string SalesRevenueAccount = "Sales Revenue";
    private const string TaxPayableAccount = "Output GST";
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
    private const int ItemReference = 2;
    private const int TaxReference = 3;

    /// <summary>
    /// "Document posting" in <c>mst.LedgerSources</c> — what the costing worker
    /// files a sale's cost under. The provisional cost of sales uses it too, so
    /// the rows it writes and the settled rows that replace them read the same.
    /// </summary>
    private const int DocumentLedgerSource = 1;

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
    private readonly Shared.Kernel.Storage.IFileStorage _storage;
    private readonly Sales.Api.Services.Pdf.IInvoicePdfRenderer _pdfRenderer;

    /// <summary>
    /// Who this branch is, for the seller block. Read from Master rather than
    /// written here: the two lines it replaces were a hard-coded company name
    /// and a hard-coded GSTIN, printed on every customer's tax invoices alike.
    /// </summary>
    private readonly IOrgIdentityProvider _orgIdentity;

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
        ICreditCheckClient creditCheckClient,
        Shared.Kernel.Storage.IFileStorage storage,
        Sales.Api.Services.Pdf.IInvoicePdfRenderer pdfRenderer,
        IOrgIdentityProvider orgIdentity)
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
        _storage = storage;
        _pdfRenderer = pdfRenderer;
        _orgIdentity = orgIdentity;
    }

    /// <summary>
    /// A confirmed sales order, turned into an invoice.
    ///
    /// <b>The lines are read from the order, never sent by the caller</b>, and
    /// they go through <see cref="CreateAsync"/> like any other invoice — so the
    /// tax is recomputed at the rates in force on the invoice's own date rather
    /// than copied from an order that may have been taken months ago. That is
    /// also why nothing here touches <c>GstCalculator</c>: one path computes an
    /// invoice, and this is not a second one.
    ///
    /// Each invoice line keeps the <c>SalesOrderDetailId</c> it came from, which
    /// is what lets the order's reservation be released line by line when the
    /// invoice posts.
    /// </summary>
    public async Task<InvoiceResult> CreateFromSalesOrderAsync(
        long salesOrderId, CreateInvoiceFromOrderRequest request, CancellationToken ct)
    {
        SalesOrder? order = await _db.SalesOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.SalesOrderId == salesOrderId, ct);

        InvoiceResult? refusal = CheckBillable(order);
        if (refusal is not null)
        {
            return refusal;
        }

        // What is left to bill on each line — the whole order the first time,
        // the remainder after a partial invoice. It used to refuse any order
        // that had an invoice at all, so an order billed in part could never
        // be billed for the rest this way.
        List<(SalesOrderDetail Line, decimal Quantity)> lines = order!.Lines
            .Select(l => (Line: l, Quantity: Billable(order, l)))
            .Where(x => x.Quantity > 0m)
            .ToList();

        if (lines.Count == 0)
        {
            return new InvoiceResult(
                InvoiceOutcome.AlreadyFulfilled,
                Detail: "Everything on this sales order has already been invoiced.");
        }

        DateOnly documentDate = request.DocumentDate
            ?? DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        return await CreateAsync(
            FromOrder(order, lines, documentDate, request.DueDate, request.PaymentTermId,
                request.PlaceOfSupplyStateCode, request.Notes),
            ct);
    }

    /// <summary>
    /// Bills some or all of what a confirmed order has left, and posts the
    /// invoice, in the caller's transaction.
    ///
    /// Moved here from <c>SalesOrdersController</c>, which held all of it and
    /// opened its own transaction. It also counted only what had been
    /// <i>invoiced</i>, so an order delivered in part on a challan and then
    /// fulfilled here issued the challan's goods a second time. The posting
    /// now bills delivered-but-unbilled goods without issuing them, and moves
    /// the order's delivered, reserved and invoiced quantities itself — so this
    /// method only chooses the lines.
    ///
    /// Empty <c>Lines</c> means everything still unbilled.
    /// </summary>
    public async Task<(InvoiceResult Result, FulfillSalesOrderResult? Fulfilled)> FulfillSalesOrderAsync(
        long salesOrderId, FulfillSalesOrderRequest request, CancellationToken ct)
    {
        SalesOrder? order = await _db.SalesOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.SalesOrderId == salesOrderId, ct);

        InvoiceResult? refusal = CheckBillable(order);
        if (refusal is not null)
        {
            return (refusal, null);
        }

        if (request.DueDate is null)
        {
            return (new InvoiceResult(
                InvoiceOutcome.DueDateMissing,
                Detail: "An invoice requires a due date. Set the due date or payment term before fulfilment."), null);
        }

        Dictionary<long, decimal> requested = request.Lines
            .GroupBy(l => l.SalesOrderDetailId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        foreach (long lineId in requested.Keys)
        {
            if (order!.Lines.All(l => l.SalesOrderDetailId != lineId))
            {
                return (new InvoiceResult(
                    InvoiceOutcome.LineInvalid,
                    Detail: $"Sales order line {lineId} does not belong to this order."), null);
            }
        }

        var lines = new List<(SalesOrderDetail Line, decimal Quantity, decimal PreviouslyInvoiced)>();

        foreach (SalesOrderDetail line in order!.Lines.OrderBy(l => l.LineNumber))
        {
            decimal billable = Billable(order, line);

            if (requested.Count == 0)
            {
                if (billable > 0m)
                {
                    lines.Add((line, billable, line.InvoicedQuantity));
                }

                continue;
            }

            if (!requested.TryGetValue(line.SalesOrderDetailId, out decimal quantity))
            {
                continue;
            }

            if (quantity <= 0m)
            {
                return (new InvoiceResult(
                    InvoiceOutcome.LineInvalid,
                    Detail: $"Line {line.LineNumber} quantity must be greater than zero."), null);
            }

            if (quantity > billable)
            {
                return (new InvoiceResult(
                    InvoiceOutcome.AlreadyFulfilled,
                    Detail: $"Line {line.LineNumber} has {billable:0.####} left to bill; "
                        + $"{quantity:0.####} were requested."), null);
            }

            lines.Add((line, quantity, line.InvoicedQuantity));
        }

        if (lines.Count == 0)
        {
            return (new InvoiceResult(
                InvoiceOutcome.AlreadyFulfilled,
                Detail: "There is nothing left to bill on this sales order."), null);
        }

        DateOnly documentDate = request.DocumentDate
            ?? DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        InvoiceResult created = await CreateAsync(
            FromOrder(order, lines.Select(x => (x.Line, x.Quantity)), documentDate, request.DueDate,
                request.PaymentTermId, request.PlaceOfSupplyStateCode, request.Notes),
            ct);
        if (created.Outcome != InvoiceOutcome.Ok)
        {
            return (created, null);
        }

        InvoiceResult posted = await PostAsync(created.InvoiceId, ct);
        if (posted.Outcome != InvoiceOutcome.Ok)
        {
            return (posted, null);
        }

        // The same tracked order the posting moved, so this is its new status.
        return (posted, new FulfillSalesOrderResult
        {
            SalesOrderId = order.SalesOrderId,
            InvoiceId = created.InvoiceId,
            Status = order.FulfilmentStatus.ToString(),
            Lines = lines.Select(x => new FulfilledSalesOrderLine
            {
                SalesOrderDetailId = x.Line.SalesOrderDetailId,
                OrderedQuantity = x.Line.Quantity,
                PreviouslyInvoicedQuantity = x.PreviouslyInvoiced,
                FulfilledQuantity = x.Quantity,
                RemainingQuantity = Math.Max(0m, x.Line.Quantity - x.PreviouslyInvoiced - x.Quantity),
            }).ToList(),
        });
    }

    /// <summary>Why an order cannot be billed, or null when it can.</summary>
    private InvoiceResult? CheckBillable(SalesOrder? order)
    {
        if (order is null || _tenant.OrgId is not Guid callerOrgId || order.OrgId != callerOrgId)
        {
            return new InvoiceResult(InvoiceOutcome.NotFound, Detail: "No such sales order.");
        }

        // Confirmed, not merely keyed. An order that has not been confirmed is
        // holding no stock, so invoicing it would issue goods nobody reserved.
        if (order.Status != DocumentStatus.Posted)
        {
            return new InvoiceResult(
                InvoiceOutcome.SourceInvalid,
                Detail: "Only a confirmed sales order becomes an invoice. Confirm the order first.");
        }

        if (order.FulfilmentStatus == FulfilmentStatus.Cancelled)
        {
            return new InvoiceResult(
                InvoiceOutcome.SourceInvalid, Detail: "This sales order has been cancelled.");
        }

        return null;
    }

    /// <summary>
    /// What an order line has left to bill: all of it not yet invoiced — or,
    /// on an order closed short, only what was actually delivered, since the
    /// rest was agreed never to go.
    /// </summary>
    private static decimal Billable(SalesOrder order, SalesOrderDetail line) =>
        Math.Max(0m, (order.ShortCloseReason is null ? line.Quantity : line.DeliveredQuantity)
            - line.InvoicedQuantity);

    /// <summary>
    /// An invoice request built from order lines. <b>The lines are read from the
    /// order, never sent by the caller</b>, and they go through
    /// <see cref="CreateAsync"/> like any other invoice — so the tax is
    /// recomputed at the rates in force on the invoice's own date rather than
    /// copied from an order that may have been taken months ago.
    ///
    /// The place of supply is re-resolved from the GSTIN rather than copied:
    /// the order stored the answer (IsInterState), and copying an answer is how
    /// a branch that has since changed state files the wrong return.
    /// </summary>
    private static SaveInvoiceRequest FromOrder(
        SalesOrder order,
        IEnumerable<(SalesOrderDetail Line, decimal Quantity)> lines,
        DateOnly documentDate,
        DateOnly? dueDate,
        long? paymentTermId,
        string? placeOfSupplyStateCode,
        string? notes) =>
        new()
        {
            DocumentDate = documentDate,
            DueDate = dueDate,
            PaymentTermId = paymentTermId,
            ContactId = order.ContactId,
            QuoteId = order.QuoteId,
            SalesOrderId = order.SalesOrderId,
            ContactGstin = order.ContactGstin,
            PlaceOfSupplyStateCode = placeOfSupplyStateCode,
            BillingAddress = order.BillingAddress,
            ShippingAddress = order.ShippingAddress,
            CurrencyCode = order.CurrencyCode,
            ExchangeRate = order.ExchangeRate,
            Notes = notes ?? order.Notes,
            TermsAndConditions = order.TermsAndConditions,
            Lines = [.. lines
                .OrderBy(x => x.Line.LineNumber)
                .Select(x => new SaveInvoiceLineRequest
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

                    // The thread back to the order line. Posting reads it to
                    // bill the line, and to release exactly what it issues.
                    SalesOrderDetailId = x.Line.SalesOrderDetailId,
                })],
        };

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
            PrintTemplateId = request.PrintTemplateId,
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
        invoice.PrintTemplateId = request.PrintTemplateId;
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

    /// <summary>How many rows one page may ask for, however large a number it sends.</summary>
    private const int MaxPageSize = 200;

    /// <summary>
    /// One page of invoices, newest first, with the total that matched.
    ///
    /// <b>Both bounds are clamped rather than trusted.</b> <c>skip</c> comes off
    /// a query string, and a negative one is not merely odd — <c>Skip(-1)</c>
    /// throws on some providers and silently returns the first page on others,
    /// so a hand-edited URL either 500s or quietly shows page one while the
    /// pager says otherwise. <c>take</c> is clamped at both ends for the same
    /// reason and a second one: <c>take=1000000</c> is a way to ask for every
    /// invoice in the branch in a single response.
    ///
    /// Same shape and the same reasoning as the sales order list; an invoice
    /// list is the one that grows fastest, so paging it here rather than in the
    /// browser matters most on this screen.
    /// </summary>
    public async Task<InvoiceListPage> ListPageAsync(
        int skip,
        int take,
        string? status,
        string? search,
        DateOnly? from,
        DateOnly? to,
        bool overdueOnly,
        CancellationToken ct)
    {
        int safeSkip = Math.Max(skip, 0);
        int safeTake = Math.Clamp(take, 1, MaxPageSize);

        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        IQueryable<Invoice> query = _db.Invoices.AsNoTracking();

        if (from.HasValue)
        {
            query = query.Where(x => x.DocumentDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.DocumentDate <= to.Value);
        }

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse(status.Trim(), ignoreCase: true, out DocumentStatus wanted))
        {
            query = query.Where(x => x.Status == wanted);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(x => x.DocumentNo.Contains(term));
        }

        // Only a posted invoice can be overdue: a draft owes nothing yet, and a
        // voided one never will.
        if (overdueOnly)
        {
            query = query.Where(x =>
                x.Status == DocumentStatus.Posted && x.DueDate.HasValue && x.DueDate.Value < today);
        }

        // Counted before paging: the screen has to say how many matched, not how
        // many fitted on the page.
        int total = await query.CountAsync(ct);

        List<InvoiceListItem> rows = await query
            .OrderByDescending(x => x.DocumentDate)
            .ThenByDescending(x => x.DocumentNo)
            .Skip(safeSkip)
            .Take(safeTake)
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

        // One call for the whole page, never one per row — Contacts is another
        // database and this is the list screen that would make it an N+1.
        List<long> contactIds = [.. rows.Select(r => r.ContactId).Distinct()];
        if (contactIds.Count > 0)
        {
            IReadOnlyDictionary<long, NamedRef> contacts = await _contactNames.ResolveAsync(contactIds, ct);
            foreach (InvoiceListItem row in rows)
            {
                if (contacts.TryGetValue(row.ContactId, out NamedRef? contact))
                {
                    row.ContactName = contact.Name;
                    row.ContactCode = contact.Code;
                }
            }
        }

        await ApplySettlementAsync(rows, ct);

        return new InvoiceListPage
        {
            Total = total,
            Skip = safeSkip,
            Take = safeTake,
            Rows = rows,
        };
    }

    /// <summary>
    /// How much of each invoice on the page has been received, from Accounting.
    ///
    /// <b>One call for the page, and only for the posted rows.</b> A draft has no
    /// ledger rows, so asking about it would be a wasted id and reading zero back
    /// would say "unpaid" about something that is not yet owed.
    ///
    /// The money is Accounting's fact, not the invoice's. Storing a paid figure
    /// on <c>sal.Invoices</c> would be a copy that drifts the first time an
    /// allocation is undone, and the two would then disagree about what the
    /// customer owes — which is the disagreement nobody notices until a
    /// statement goes out.
    /// </summary>
    private async Task ApplySettlementAsync(List<InvoiceListItem> rows, CancellationToken ct)
    {
        List<long> postedIds = [.. rows
            .Where(r => r.Status == nameof(DocumentStatus.Posted))
            .Select(r => r.InvoiceId)];

        if (postedIds.Count == 0 || _tenant.OrgId is not Guid orgId
            || _tenant.CustomerId is not Guid customerId)
        {
            return;
        }

        IReadOnlyDictionary<long, Settlement> settlements = await _ledgerClient.GetSettlementsAsync(
            new SettlementQueryRequest
            {
                CustomerId = customerId,
                OrgId = orgId,
                TransactionTypeCode = "INV",
                TransactionIds = postedIds,
            },
            ct);

        foreach (InvoiceListItem row in rows)
        {
            if (!settlements.TryGetValue(row.InvoiceId, out Settlement? settlement))
            {
                continue;
            }

            row.PaidAmount = settlement.PaidAmount;
            row.OutstandingAmount = settlement.OutstandingAmount;
            row.SettlementStatus = SettlementStatusOf(settlement);
        }
    }

    /// <summary>
    /// Paid, PartPaid or Unpaid.
    ///
    /// <b>Compared against a rounding tolerance rather than zero.</b> A payment
    /// in another currency settles at its own rate, so an invoice can come out a
    /// few paise short of square and is settled for every practical purpose;
    /// calling that PartPaid would leave a chasing list full of invoices nobody
    /// can collect on. A paisa is the unit money is kept in, so that is the
    /// tolerance.
    /// </summary>
    private static string SettlementStatusOf(Settlement settlement)
    {
        const decimal tolerance = 0.01m;

        if (Math.Abs(settlement.OutstandingAmount) <= tolerance)
        {
            return "Paid";
        }

        return settlement.PaidAmount > tolerance ? "PartPaid" : "Unpaid";
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

        // Resolved first, not where the PDF is written. Posting calls Accounting
        // and Inventory over HTTP before it archives, so a scope that could only
        // fail at the end would leave the ledger and the stock posted while this
        // invoice rolled back. Here it fails before anything has happened.
        StorageScope archiveScope = StorageScope.For(_tenant, StorageApp.RetailErp, StorageModule.Sales);

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

        // The order lines this invoice bills, checked before anything moves.
        (SalesOrder? order, InvoiceResult? orderRefusal) = await ReadBilledOrderAsync(invoice, ct);
        if (orderRefusal is not null)
        {
            return orderRefusal;
        }

        // How much of each line leaves stock on this invoice. Against an order
        // line, goods already delivered on a challan and not yet billed are
        // billed first and issued never; only the rest is issued. Against a
        // named challan, nothing is issued at all.
        Dictionary<long, decimal> issued = IssueQuantities(invoice, order);

        decimal totalCogs = 0;

        // Each issued line's cost as the request path valued it: provisional,
        // until the costing worker settles the movement behind it (TK-10).
        List<(long LineId, long ItemId, decimal Value)> provisionalCogs = [];

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
                        challanLine.InvoicedQuantity += line.Quantity;
                    }
                }

            }
        }
        else
        {
            var stockLines = invoice.Lines
                .Where(l => l.LineType == DocumentLineType.Stock
                    && l.ItemId.HasValue
                    && issued.GetValueOrDefault(l.InvoiceDetailId) > 0m)
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
                        Quantity = issued[l.InvoiceDetailId],
                        WarehouseId = l.WarehouseId,

                        // Per line: only a line billed against an order line
                        // has a hold to release. Keyed on the header's order,
                        // a line added beside the order's lines released a
                        // reservation nobody had taken.
                        ReleaseReservation = l.SalesOrderDetailId.HasValue,
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

                    if (issueLine.LineValue > 0m)
                    {
                        provisionalCogs.Add((issueLine.SourceLineId, issueLine.ItemId, issueLine.LineValue));
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
            // The number on the document's face, so the ledger can report it
            // without reaching into this service's schema to look it up.
            DocumentNo = invoice.DocumentNo,
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

        if (invoice.DeliveryChallanId.HasValue && totalCogs > 0)
        {
            // Against a challan, unchanged by TK-10 and left to TK-90: the owner kept the challan's
            // postings out of that card, and GDNI is still not seeded, so this is
            // refused whenever it is non-zero, as it was before.
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
                AccountSystemName = GdniAccount,
                CreditAmount = totalCogs,
                TransactionDesc = "Inventory relief",
            });
        }

        // The cost of what this invoice issued, provisionally, on the key the
        // costing worker settles it on: this document, the line, the COGS leg
        // type — the same pair of accounts and item sub-accounts the worker
        // writes. The worker's posting replaces these rows with the settled
        // cost; if it got there first, Accounting keeps its rows and drops these
        // (ProvisionalLedgerTypeIds). Either way the sale's cost is in the
        // ledger once, at the settled figure (TK-10).
        //
        // Before TK-10 the invoice posted one aggregate pair at line 0 and the
        // worker posted a pair per line, on different keys, so both stood and
        // every direct sale's cost of goods was booked twice.
        foreach ((long lineId, long itemId, decimal value) in provisionalCogs)
        {
            postRequest.Legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = CogsLedgerType,
                LedgerSourceId = DocumentLedgerSource,
                TransactionDetailId = lineId,
                AccountSystemName = CogsAccount,
                SubAccountReferenceType = ItemReference,
                SubAccountReferenceId = itemId,
                DebitAmount = value,
                TransactionDesc = "Cost of goods sold (provisional)",
            });

            postRequest.Legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = CogsLedgerType,
                LedgerSourceId = DocumentLedgerSource,
                TransactionDetailId = lineId,
                AccountSystemName = InventoryAccount,
                SubAccountReferenceType = ItemReference,
                SubAccountReferenceId = itemId,
                CreditAmount = value,
                TransactionDesc = "Inventory relief (provisional)",
            });
        }

        if (provisionalCogs.Count > 0)
        {
            postRequest.ProvisionalLedgerTypeIds = [CogsLedgerType];
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

        // The order catches up only once the ledger has accepted the invoice:
        // billed on every line, delivered by what this invoice actually issued.
        if (order is not null)
        {
            foreach (InvoiceDetail line in invoice.Lines.Where(l => l.SalesOrderDetailId.HasValue))
            {
                SalesOrderDetail orderLine = order.Lines.First(o => o.SalesOrderDetailId == line.SalesOrderDetailId);
                decimal issuedHere = issued.GetValueOrDefault(line.InvoiceDetailId);

                orderLine.InvoicedQuantity += line.Quantity;
                orderLine.DeliveredQuantity += issuedHere;
                orderLine.ReservedQuantity = Math.Max(0m, orderLine.ReservedQuantity - issuedHere);
            }

            SalesOrderFulfilment.Refresh(order);
        }

        invoice.Status = DocumentStatus.Posted;
        invoice.PostedAt = _clock.GetUtcNow();
        invoice.PostedBy = _user.UserId;

        // 4. Generate and Archive PDF
        //
        // The seller comes from Master, and a branch that cannot be resolved
        // stops the posting. A tax invoice names the supplier whose GSTIN the
        // buyer claims an input credit against; printing a placeholder there is
        // worse than printing nothing, because the document looks filed. This
        // used to print "Our Company" and a made-up registration on every
        // customer's invoices alike.
        OrgIdentity? seller = await _orgIdentity.GetIdentityAsync(ct)
            ?? throw new InvalidOperationException(
                "The branch issuing this invoice could not be identified, so its seller "
                + "details cannot be printed. The invoice was not posted.");

        var contactNames = await _contactNames.ResolveAsync([invoice.ContactId], ct);
        string customerName = contactNames.TryGetValue(invoice.ContactId, out var contactRef) ? contactRef.Name : "Unknown";

        var pdfModel = new Sales.Api.Services.Pdf.PdfInvoiceModel
        {
            Invoice = invoice,
            OrgName = seller.Name,
            OrgGstin = seller.Gstin,
            CustomerName = customerName,
            ItemNames = (await _itemNames.ResolveAsync(invoice.Lines.Where(l => l.ItemId.HasValue).Select(l => (long)l.ItemId!).Distinct().ToArray(), ct)).ToDictionary(k => k.Key, v => v.Value.Name)
        };
        
        byte[] pdfBytes = _pdfRenderer.Render(pdfModel);
        string objectKey = StorageKey.DocumentKey(archiveScope, "invoices", $"{invoice.InvoiceId}.pdf");
        // Replace, deliberately. The archive is written before the commit below,
        // so a post whose commit fails leaves its PDF behind; a strict
        // create-only save would then find that leftover on the retry and stop
        // this invoice ever posting. The same key is the same invoice, and blob
        // versioning keeps whatever is replaced.
        await _storage.SaveAsync(objectKey, new MemoryStream(pdfBytes), "application/pdf", FileWriteMode.Replace, ct);

        await _db.SaveChangesAsync(ct);
        return new InvoiceResult(InvoiceOutcome.Ok, invoice.InvoiceId);
    }

    public Task<InvoiceResult> VoidAsync(long invoiceId, CancellationToken ct) =>
        VoidAsync(invoiceId, new VoidInvoiceRequest { Reason = "Voided by user." }, ct);

    public async Task<InvoiceResult> VoidAsync(
        long invoiceId, VoidInvoiceRequest request, CancellationToken ct)
    {
        // With its lines: the challan and order reversals below walk them, and
        // without the Include they walked an empty list and reversed nothing.
        Invoice? invoice = await _db.Invoices
            .Include(x => x.Lines)
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
                        challanLine.InvoicedQuantity -= line.Quantity;
                    }
                }
            }
        }


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
                    // The number on the document's face, so the ledger can report it
                    // without reaching into this service's schema to look it up.
                    DocumentNo = invoice.DocumentNo,
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

            // The bill is withdrawn; the goods it issued are not. So the order
            // lines give back what this invoice billed and keep what it
            // delivered — delivered and not billed again, which the next
            // invoice against them bills without issuing a second time.
            if (invoice.SalesOrderId.HasValue)
            {
                SalesOrder? order = await _db.SalesOrders
                    .Include(o => o.Lines)
                    .FirstOrDefaultAsync(o => o.SalesOrderId == invoice.SalesOrderId.Value, ct);

                if (order is not null)
                {
                    foreach (InvoiceDetail line in invoice.Lines.Where(l => l.SalesOrderDetailId.HasValue))
                    {
                        SalesOrderDetail? orderLine = order.Lines
                            .FirstOrDefault(o => o.SalesOrderDetailId == line.SalesOrderDetailId);
                        if (orderLine is not null)
                        {
                            orderLine.InvoicedQuantity = Math.Max(0m, orderLine.InvoicedQuantity - line.Quantity);
                        }
                    }
                }
            }

            var registers = await _db.SalesRegister
                .Where(r => r.SourceId == invoiceId && r.TransactionTypeCode == invoice.TransactionTypeCode)
                .ToListAsync(ct);
            _db.SalesRegister.RemoveRange(registers);
        }

        await _db.SaveChangesAsync(ct);
        return new InvoiceResult(InvoiceOutcome.Ok, invoice.InvoiceId);
    }

    /// <summary>
    /// The sales order an invoice bills, checked line by line: the order is
    /// confirmed and for this customer, every line naming an order line names
    /// one of its lines for the same item, and none bills more than the line
    /// has left to bill. Null order when the invoice names none.
    /// </summary>
    private async Task<(SalesOrder? Order, InvoiceResult? Refusal)> ReadBilledOrderAsync(
        Invoice invoice, CancellationToken ct)
    {
        List<InvoiceDetail> billed = invoice.Lines.Where(l => l.SalesOrderDetailId.HasValue).ToList();

        if (!invoice.SalesOrderId.HasValue)
        {
            return billed.Count == 0
                ? (null, null)
                : (null, new InvoiceResult(
                    InvoiceOutcome.LineInvalid,
                    Detail: "A line names a sales order line, but the invoice names no sales order."));
        }

        SalesOrder? order = await _db.SalesOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.SalesOrderId == invoice.SalesOrderId.Value, ct);

        if (order is null)
        {
            return (null, new InvoiceResult(
                InvoiceOutcome.SourceInvalid,
                Detail: "The sales order this invoice bills could not be found in this branch."));
        }

        if (order.Status != DocumentStatus.Posted)
        {
            return (null, new InvoiceResult(
                InvoiceOutcome.SourceInvalid, Detail: "Only a confirmed sales order can be billed."));
        }

        if (order.ContactId != invoice.ContactId)
        {
            return (null, new InvoiceResult(
                InvoiceOutcome.SourceInvalid,
                Detail: "The invoice's customer is not the sales order's customer."));
        }

        foreach (var byOrderLine in billed.GroupBy(l => l.SalesOrderDetailId!.Value))
        {
            SalesOrderDetail? orderLine = order.Lines.FirstOrDefault(o => o.SalesOrderDetailId == byOrderLine.Key);
            if (orderLine is null)
            {
                return (null, new InvoiceResult(
                    InvoiceOutcome.LineInvalid,
                    Detail: "A line on this invoice is not a line of its sales order."));
            }

            if (byOrderLine.Any(l => l.ItemId != orderLine.ItemId))
            {
                return (null, new InvoiceResult(
                    InvoiceOutcome.LineInvalid,
                    Detail: $"A line's item is not the item on order line {orderLine.LineNumber}."));
            }

            decimal left = orderLine.Quantity - orderLine.InvoicedQuantity;
            decimal billing = byOrderLine.Sum(l => l.Quantity);
            if (billing > left)
            {
                return (null, new InvoiceResult(
                    InvoiceOutcome.AlreadyFulfilled,
                    Detail: $"Order line {orderLine.LineNumber} has {left:0.####} left to bill, "
                        + $"and this invoice bills {billing:0.####}."));
            }
        }

        return (order, null);
    }

    /// <summary>
    /// How much of each stock line this invoice issues, by invoice line.
    ///
    /// Against a named challan: nothing — the challan already took the goods out.
    /// Against an order line: the line's quantity less what that order line has
    /// delivered and not yet billed, taken in line order when two invoice lines
    /// bill the same order line. That second rule is what stops an invoice raised
    /// after a challan, without naming it, from issuing the goods again.
    /// </summary>
    private static Dictionary<long, decimal> IssueQuantities(Invoice invoice, SalesOrder? order)
    {
        var issued = new Dictionary<long, decimal>();

        if (invoice.DeliveryChallanId.HasValue)
        {
            return issued;
        }

        Dictionary<long, decimal> deliveredNotBilled = order?.Lines.ToDictionary(
            o => o.SalesOrderDetailId, SalesOrderFulfilment.DeliveredNotInvoiced) ?? [];

        foreach (InvoiceDetail line in invoice.Lines.OrderBy(l => l.LineNumber))
        {
            if (line.LineType != DocumentLineType.Stock || !line.ItemId.HasValue)
            {
                continue;
            }

            decimal quantity = line.Quantity;

            if (line.SalesOrderDetailId is long orderLineId
                && deliveredNotBilled.TryGetValue(orderLineId, out decimal alreadyOut))
            {
                decimal covered = Math.Min(quantity, alreadyOut);
                deliveredNotBilled[orderLineId] = alreadyOut - covered;
                quantity -= covered;
            }

            issued[line.InvoiceDetailId] = quantity;
        }

        return issued;
    }
}



