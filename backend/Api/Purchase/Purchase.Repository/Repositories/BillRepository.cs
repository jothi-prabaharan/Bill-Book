using Shared.Kernel.Audit;
using Purchase.Repository.Clients;
using Shared.Kernel.CustomFields;
using Shared.Kernel.Approvals;
using Microsoft.EntityFrameworkCore;
using Purchase.Entity.Models;
using Purchase.Entity.TableEntities;
using Purchase.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tax;
using Shared.Kernel.Tenancy;

namespace Purchase.Repository.Repositories;

/// <summary>
/// The vendor bill — <c>BIL</c>. What is owed, and the document input tax credit
/// is claimed on.
///
/// <b>It has two posting shapes, and which one runs depends on whether a receipt
/// came first.</b> That is the single most important thing about this class:
///
/// <list type="bullet">
/// <item><b>Against a receipt</b> — the goods already reached stock and already
/// credited the clearing account. The bill moves <i>no stock</i> and debits
/// <c>Goods Received Not Invoiced</c>, closing what the receipt opened.</item>
/// <item><b>With no receipt</b> — nothing has arrived in the books yet, so the
/// bill does both jobs: it moves the stock and debits Inventory directly.</item>
/// </list>
///
/// Both shapes then add the Input GST legs and credit Accounts Payable. Input
/// GST is an <i>asset</i> — it is reclaimable — which is the one difference in
/// meaning from the sales side, where the identical arithmetic yields a
/// liability.
/// </summary>
public sealed class BillRepository
{
    private const string InventoryAccount = "Inventory";
    private const string GrniAccount = "Goods Received Not Invoiced";
    private const string InputGstAccount = "Input GST";

    /// <summary>Accounting's SystemAccount.GstPayableReverseCharge (TK-115, D-30).</summary>
    public const string ReverseChargeAccount = "GST Payable (Reverse Charge)";
    private const string AccountsPayableAccount = "Accounts Payable";
    private const string FixedAssetAccount = "Fixed Asset";

    private const int ItemLedgerType = 1;
    private const int TaxLedgerType = 2;
    private const int ControlLedgerType = 3;
    private const int TransactionLedgerSource = 1;

    /// <summary>Accounting's SubAccountReferenceType. Sent as numbers — Purchase does not reference its assemblies.</summary>
    private const int ContactReference = 1;

    private const int TaxReference = 3;

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
    private readonly IPaymentTermClient _paymentTerms;
    private readonly IFixedAssetClient _fixedAssets;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    /// <summary>Approval chains (TK-100). Null in tests that do not exercise them.</summary>
    private readonly PurchaseApprovalRepository? _approvals;

    /// <summary>Checks a line's project through Accounting (TK-105). Null in tests that do not exercise it.</summary>
    private readonly Shared.Kernel.Projects.IProjectDirectory? _projects;

    /// <summary>Custom field values on save (TK-112); absent in tests built without one.</summary>
    private readonly DocumentCustomFieldGate? _customFields;

    /// <summary>What TDS a bill owes, from Accounting (TK-117). Null in tests that do not exercise it.</summary>
    private readonly ITdsQuoteClient? _tdsQuotes;

    /// <summary>Checks contact MSME status through Master (TK-119). Null in tests that do not exercise it.</summary>
    private readonly Shared.Kernel.Contacts.IContactDirectory? _contacts;

    /// <summary>Accounting's SystemAccount.TdsPayable (TK-117).</summary>
    public const string TdsPayableAccount = "TDS Payable";

    public BillRepository(
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
        IPaymentTermClient paymentTerms,
        IFixedAssetClient fixedAssets,
        ICurrentUser user,
        TimeProvider clock,
        PurchaseApprovalRepository? approvals = null,
        Shared.Kernel.Projects.IProjectDirectory? projects = null,
        DocumentCustomFieldGate? customFields = null,
        ITdsQuoteClient? tdsQuotes = null,
        Shared.Kernel.Contacts.IContactDirectory? contacts = null)
    {
        _contacts = contacts;
        _customFields = customFields;
        _tdsQuotes = tdsQuotes;
        _projects = projects;
        _approvals = approvals;
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
        _paymentTerms = paymentTerms;
        _fixedAssets = fixedAssets;
        _user = user;
        _clock = clock;
    }

    public async Task<BillResult> CreateAsync(SaveBillRequest request, CancellationToken ct)
    {
        string? baseCurrency = await _baseCurrency.GetBaseCurrencyAsync(ct);
        if (baseCurrency is null)
        {
            return new BillResult(
                BillOutcome.RatesUnavailable, Detail: "Branch base currency could not be read.");
        }

        // Refused before a number is spent, and before the database has to.
        // The unique index is the real guard; this is the readable one.
        if (await VendorNumberTakenAsync(request, null, ct))
        {
            return new BillResult(
                BillOutcome.DuplicateVendorBillNo,
                Detail: $"This vendor has already billed {request.VendorBillNo} in this "
                    + "financial year. Claiming the same number twice is a duplicate input tax "
                    + "credit — check whether the bill is already entered.");
        }

        Bill bill = new()
        {
            TransactionTypeCode = "BIL",
            PrintTemplateId = request.PrintTemplateId,
            DocumentDate = request.DocumentDate,
            CurrencyCode = request.CurrencyCode ?? baseCurrency,
            ExchangeRate = request.ExchangeRate ?? 1m,
            Status = DocumentStatus.Draft,
        };

        BillResult applied = await ApplyAsync(bill, request, ct);
        if (applied.Outcome != BillOutcome.Ok)
        {
            return applied;
        }

        NumberAllocation alloc = await _numbering.NextAsync("BIL", request.DocumentDate, ct);
        bill.DocumentNo = alloc.Code;

        _db.Bills.Add(bill);
        await _db.SaveChangesAsync(ct);

        return new BillResult(BillOutcome.Ok, bill.BillId);
    }

    public async Task<BillResult> UpdateAsync(
        long billId, SaveBillRequest request, CancellationToken ct)
    {
        Bill? bill = await _db.Bills
            .Include(b => b.Lines)
            .ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(b => b.BillId == billId, ct);

        if (bill is null)
        {
            return new BillResult(BillOutcome.NotFound);
        }

        DocumentTransition transition = DocumentLifecycle.CanEdit(bill.Status);
        if (!transition.IsAllowed)
        {
            return new BillResult(BillOutcome.LifecycleRefused, Detail: transition.Detail);
        }

        // An approval approves what was seen: an edit sends it back (TK-100).
        bool returned = _approvals is not null
            && await _approvals.ReturnToDraftAsync(ApprovalRequestKind.PurchaseBill, billId, bill, ct);

        if (await VendorNumberTakenAsync(request, billId, ct))
        {
            return new BillResult(
                BillOutcome.DuplicateVendorBillNo,
                Detail: $"This vendor has already billed {request.VendorBillNo} in this "
                    + "financial year.");
        }

        if (request.CurrencyCode is not null)
        {
            bill.CurrencyCode = request.CurrencyCode;
        }

        if (request.ExchangeRate.HasValue)
        {
            bill.ExchangeRate = request.ExchangeRate.Value;
        }

        bill.DocumentDate = request.DocumentDate;
        bill.PrintTemplateId = request.PrintTemplateId;

        _db.BillDetailTaxes.RemoveRange(bill.Lines.SelectMany(l => l.Taxes));
        _db.BillDetails.RemoveRange(bill.Lines);
        bill.Lines.Clear();

        BillResult applied = await ApplyAsync(bill, request, ct);
        if (applied.Outcome != BillOutcome.Ok)
        {
            return applied;
        }

        await _db.SaveChangesAsync(ct);
        return new BillResult(BillOutcome.Ok, bill.BillId, returned ? "Edited while under approval, so it is a draft again. Submit it for approval once more." : null);
    }

    /// <summary>
    /// The vendor's number, already used by this vendor this financial year.
    ///
    /// The database has a unique index on exactly this and is the real guard —
    /// but a constraint violation reaches the user as a 500, and the whole point
    /// of the rule is that the person entering the bill finds out.
    /// </summary>
    private async Task<bool> VendorNumberTakenAsync(
        SaveBillRequest request, long? excludingBillId, CancellationToken ct)
    {
        int financialYear = request.VendorBillDate.Month >= 4
            ? request.VendorBillDate.Year
            : request.VendorBillDate.Year - 1;

        return await _db.Bills.AnyAsync(
            b => b.ContactId == request.ContactId
                && b.VendorBillNo == request.VendorBillNo
                && b.VendorBillFinancialYear == financialYear
                && (excludingBillId == null || b.BillId != excludingBillId),
            ct);
    }

    /// <summary>Everything create and update do identically. One copy, called twice.</summary>
    private async Task<BillResult> ApplyAsync(Bill bill, SaveBillRequest request, CancellationToken ct)
    {
        if (request.Lines == null || request.Lines.Count == 0 || !request.Lines.Any(l => l.Quantity > 0))
        {
            return new BillResult(
                BillOutcome.LineInvalid,
                Detail: "A bill needs at least one line with a quantity greater than zero.");
        }

        if (request.VendorBillDate > request.DocumentDate)
        {
            return new BillResult(
                BillOutcome.LineInvalid,
                Detail: "The vendor cannot have dated their bill after you received it.");
        }

        BranchSettings? settings = await _branchSettings.GetSettingsAsync(ct);
        if (settings is null)
        {
            return new BillResult(
                BillOutcome.RatesUnavailable, Detail: "Branch settings could not be read.");
        }

        PlaceOfSupplyResult pos = PlaceOfSupply.Resolve(
            settings.StateCode, request.PlaceOfSupplyStateCode, request.ContactGstin);

        if (!pos.IsOk)
        {
            return new BillResult(BillOutcome.PlaceOfSupplyRefused, Detail: pos.Detail);
        }

        GoodsReceipt? receipt = null;

        if (request.GoodsReceiptId is long receiptId)
        {
            receipt = await _db.GoodsReceipts
                .Include(r => r.Lines)
                .FirstOrDefaultAsync(r => r.GoodsReceiptId == receiptId, ct);

            if (receipt is null)
            {
                return new BillResult(
                    BillOutcome.SourceInvalid,
                    Detail: "That goods receipt does not exist in this branch.");
            }

            if (receipt.ContactId != request.ContactId)
            {
                return new BillResult(
                    BillOutcome.SourceInvalid,
                    Detail: "That goods receipt belongs to a different vendor.");
            }

            // An unposted receipt has credited nothing, so there is nothing for
            // the bill to clear and it would debit a balance that does not exist.
            if (receipt.Status != DocumentStatus.Posted)
            {
                return new BillResult(
                    BillOutcome.SourceInvalid,
                    Detail: "That goods receipt has not been posted, so nothing is sitting in "
                        + "Goods Received Not Invoiced for this bill to clear. Post the receipt "
                        + "first, or bill without it.");
            }
        }

        DateOnly? dueDate = request.DueDate;

        if (dueDate is null && request.PaymentTermId is long termId)
        {
            (Guid customerId, Guid orgId) = _tenant.Require();
            dueDate = await _paymentTerms.DueDateAsync(
                termId, customerId, orgId, request.DocumentDate, ct);
        }

        if (dueDate is null)
        {
            return new BillResult(
                BillOutcome.DueDateMissing,
                Detail: "A bill needs a due date. Choose a payment term or set the date "
                    + "directly — payables aging is only as good as this column.");
        }

        // MSMED Act 2006 / Sec 43B(h): Payment terms for an MSME vendor cannot exceed 45 days from acceptance (TK-119).
        if (_contacts != null)
        {
            var contactMap = await _contacts.FindAsync([request.ContactId], ct);
            if (contactMap.TryGetValue(request.ContactId, out var contact) && contact.IsMsme)
            {
                if (dueDate.Value.DayNumber - request.DocumentDate.DayNumber > 45)
                {
                    return new BillResult(
                        BillOutcome.MsmeDueDateExceeded45Days,
                        Detail: "Payment terms for an MSME vendor cannot exceed 45 days from acceptance (MSMED Act / Sec 43B(h)).");
                }
            }
        }

        bill.PurchaseOrderId = request.PurchaseOrderId;
        bill.GoodsReceiptId = request.GoodsReceiptId;
        bill.VendorBillNo = request.VendorBillNo;
        bill.VendorBillDate = request.VendorBillDate;
        bill.PaymentTermId = request.PaymentTermId;
        bill.DueDate = dueDate.Value;
        bill.LandedCostAmount = request.LandedCostAmount;
        bill.IsReverseCharge = request.IsReverseCharge;
        bill.ContactId = request.ContactId;
        bill.ContactGstin = request.ContactGstin;
        bill.BillingAddress = request.BillingAddress;
        bill.ShippingAddress = request.ShippingAddress;
        bill.IsInterState = pos.IsInterState;
        bill.Notes = request.Notes;
        bill.TermsAndConditions = request.TermsAndConditions;

        // The nearest source: its goods receipt, else its purchase order.
        CustomFieldSource? customFieldSource = bill.BillId != 0
            ? null
            : request.GoodsReceiptId is long sourceReceiptId
                ? new CustomFieldSource(CustomFieldEntityKind.GoodsReceipt, await _db.GoodsReceipts.Where(r => r.GoodsReceiptId == sourceReceiptId).Select(r => r.CustomFields).FirstOrDefaultAsync(ct))
                : request.PurchaseOrderId is long sourceOrderId
                    ? new CustomFieldSource(CustomFieldEntityKind.PurchaseOrder, await _db.PurchaseOrders.Where(o => o.PurchaseOrderId == sourceOrderId).Select(o => o.CustomFields).FirstOrDefaultAsync(ct))
                    : null;

        // Custom fields (TK-112): validated, kept when left out of an update, and carried from the source document.
        if (await DocumentCustomFieldGate.ApplyAsync(_customFields, bill, CustomFieldEntityKind.Bill, request.CustomFields, isNew: bill.BillId == 0, customFieldSource, ct) is string customFieldsRefused)
        {
            return new BillResult(BillOutcome.CustomFieldsRefused, Detail: customFieldsRefused);
        }

        TaxContext taxContext = new(pos.IsInterState, settings.DiscountBeforeTax);
        if (await Shared.Kernel.Projects.ProjectCheck.RefusalAsync(_projects, request.Lines.Select(l => l.ProjectId), ct) is string projectRefusal)
        {
            return new BillResult(BillOutcome.LineInvalid, Detail: projectRefusal);
        }

        List<TaxLineResult> taxLines = new(request.Lines.Count);

        for (int i = 0; i < request.Lines.Count; i++)
        {
            SaveBillLineRequest lineReq = request.Lines[i];
            int lineNumber = i + 1;

            if (lineReq.ItemId is null && string.IsNullOrWhiteSpace(lineReq.Description))
            {
                return new BillResult(
                    BillOutcome.LineInvalid,
                    Detail: $"Line {lineNumber} has no item, so it needs a description.");
            }

            if (lineReq.LineType == DocumentLineType.Stock && lineReq.ItemId is null)
            {
                return new BillResult(
                    BillOutcome.LineInvalid,
                    Detail: $"Line {lineNumber} is a stock line, so it needs an item.");
            }

            if (lineReq.LineType == DocumentLineType.Expense && lineReq.AccountId is null)
            {
                return new BillResult(
                    BillOutcome.LineInvalid,
                    Detail: $"Line {lineNumber} is an expense line, so it needs an account.");
            }

            if (lineReq.LineType == DocumentLineType.Capital
                && lineReq.FixedAssetCategoryId is null)
            {
                return new BillResult(
                    BillOutcome.LineInvalid,
                    Detail: $"Line {lineNumber} is a capital line, so it needs a fixed asset "
                        + "category.");
            }

            TaxRate? rate = null;
            if (lineReq.TaxGroupId is long taxGroupId)
            {
                rate = await _rates.GetRateAsync(taxGroupId, request.DocumentDate, ct);
                if (rate is null)
                {
                    return new BillResult(
                        BillOutcome.RatesUnavailable,
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

            // Against a receipt, the bill's line has to be worth what the receipt
            // put into the clearing account, or clearing it leaves a residue.
            // What to do with a genuine difference — revalue the cost layer, or
            // post a purchase price variance — is Purchase.md §8 and is not
            // decided, so a mismatch is refused rather than silently absorbed.
            if (receipt is not null && lineReq.LineType != DocumentLineType.Capital)
            {
                BillResult matched = MatchesReceipt(
                    receipt, lineReq, computed, lineNumber);

                if (matched.Outcome != BillOutcome.Ok)
                {
                    return matched;
                }
            }

            taxLines.Add(computed);

            BillDetail detail = new()
            {
                LineNumber = lineNumber,
                GoodsReceiptDetailId = lineReq.GoodsReceiptDetailId,
                PurchaseOrderDetailId = lineReq.PurchaseOrderDetailId,
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
                ProjectId = lineReq.ProjectId,

                // Only a line with a project can be charged on to its client (TK-106).
                IsBillable = lineReq.IsBillable && lineReq.ProjectId is not null,
                MarkupPercent = lineReq.IsBillable && lineReq.ProjectId is not null ? lineReq.MarkupPercent : null,
                LineTotal = computed.LineTotal,
                ItemBatchId = lineReq.ItemBatchId,
                LineNotes = lineReq.LineNotes,
                ApportionedLandedCost = 0m,
                ReturnedQuantity = 0m,
                ItcEligibility = lineReq.ItcEligibility,
            };

            foreach (TaxComponentAmount component in computed.Components)
            {
                detail.Taxes.Add(new BillDetailTax
                {
                    TaxComponent = component.Component,
                    Rate = component.Rate,
                    TaxableAmount = component.TaxableAmount,
                    Amount = component.Amount,
                    AmountBase = component.Amount * bill.ExchangeRate,
                });
            }

            bill.Lines.Add(detail);
        }

        TaxDocumentTotals totals = GstCalculator.Totals(taxLines);

        bill.SubTotal = totals.SubTotal;
        bill.DiscountAmount = totals.DiscountAmount;
        bill.TaxableAmount = totals.TaxableAmount;
        bill.CgstAmount = totals.CgstAmount;
        bill.SgstAmount = totals.SgstAmount;
        bill.IgstAmount = totals.IgstAmount;
        bill.CessAmount = totals.CessAmount;
        // Under reverse charge the vendor charged no tax, so what they are owed
        // is the total without it (D-30). The tax is still computed and kept on
        // the lines: this business owes it to the government instead.
        decimal payable = bill.IsReverseCharge
            ? totals.TotalAmount - (totals.CgstAmount + totals.SgstAmount + totals.IgstAmount + totals.CessAmount)
            : totals.TotalAmount;

        bill.RoundOffAmount =
            Math.Round(payable, 0, MidpointRounding.AwayFromZero) - payable;
        bill.TotalAmount = payable + bill.RoundOffAmount;
        bill.TotalAmountBase = bill.TotalAmount * bill.ExchangeRate;

        return new BillResult(BillOutcome.Ok, bill.BillId);
    }

    /// <summary>
    /// Whether a bill line agrees with the receipt line it bills.
    ///
    /// <b>The quantity may be less — a bill can cover part of a delivery — but
    /// the price may not differ.</b> A price difference is purchase price
    /// variance, and whether it revalues the cost layer or posts to a variance
    /// account is an open decision. Refusing keeps the money accounted for
    /// instead of quietly leaving a residue in the clearing account.
    /// </summary>
    private static BillResult MatchesReceipt(
        GoodsReceipt receipt,
        SaveBillLineRequest lineReq,
        TaxLineResult computed,
        int lineNumber)
    {
        if (lineReq.GoodsReceiptDetailId is not long receiptLineId)
        {
            return new BillResult(
                BillOutcome.LineInvalid,
                Detail: $"Line {lineNumber} does not say which receipt line it bills. Every line "
                    + "of a bill raised against a receipt has to name one, or the clearing "
                    + "account cannot be cleared.");
        }

        GoodsReceiptDetail? receiptLine = receipt.Lines
            .FirstOrDefault(l => l.GoodsReceiptDetailId == receiptLineId);

        if (receiptLine is null)
        {
            return new BillResult(
                BillOutcome.SourceInvalid,
                Detail: $"Line {lineNumber} names a receipt line that is not on that receipt.");
        }

        if (lineReq.Quantity > receiptLine.AcceptedQuantity)
        {
            return new BillResult(
                BillOutcome.LineInvalid,
                Detail: $"Line {lineNumber} bills {lineReq.Quantity} but only "
                    + $"{receiptLine.AcceptedQuantity} was accepted on that receipt. Rejected "
                    + "goods were never taken into stock and are not owed for.");
        }

        decimal receiptUnitCost = receiptLine.Quantity == 0
            ? 0m
            : receiptLine.TaxableAmount / receiptLine.Quantity;

        decimal billUnitCost = lineReq.Quantity == 0
            ? 0m
            : computed.TaxableAmount / lineReq.Quantity;

        // To the paise. Anything finer is rounding noise between two figures the
        // same calculator produced.
        if (Math.Round(receiptUnitCost, 2) != Math.Round(billUnitCost, 2))
        {
            return new BillResult(
                BillOutcome.PriceVarianceUndecided,
                Detail: $"Line {lineNumber} is billed at {Math.Round(billUnitCost, 2)} a unit but "
                    + $"was received at {Math.Round(receiptUnitCost, 2)}. Purchase price variance "
                    + "is not implemented yet — how the difference is treated is an open "
                    + "decision, so the bill is refused rather than leaving a residue in Goods "
                    + "Received Not Invoiced. Correct the price, or bill without the receipt.");
        }

        return new BillResult(BillOutcome.Ok);
    }

    /// <summary>
    /// Posts the bill.
    ///
    /// Stock first when there is any to move, then the ledger, then the status —
    /// the same order and for the same reasons as the goods receipt.
    /// </summary>
    public async Task<BillResult> PostAsync(long billId, CancellationToken ct)
    {
        Bill? bill = await _db.Bills
            .Include(b => b.Lines)
            .ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(b => b.BillId == billId, ct);

        if (bill is null)
        {
            return new BillResult(BillOutcome.NotFound);
        }

        DocumentTransition transition = DocumentLifecycle.CanPost(bill.Status, bill.Lines.Count);
        if (!transition.IsAllowed)
        {
            return new BillResult(BillOutcome.LifecycleRefused, Detail: transition.Detail);
        }

        // A bill has no separate approve step, so posting a draft is gated (TK-100).
        if (_approvals is not null && bill.Status == DocumentStatus.Draft
            && await _approvals.GateAsync(ApprovalRequestKind.PurchaseBill, bill, ct) is string gated)
        {
            return new BillResult(BillOutcome.LifecycleRefused, Detail: gated);
        }

        (Guid customerId, Guid orgId) = _tenant.Require();

        bool againstReceipt = bill.GoodsReceiptId.HasValue;

        // TDS (TK-117), asked before anything else moves: a bill that cannot be
        // priced for TDS must not have received its stock first.
        (TdsQuote? tds, BillResult? tdsRefusal) = await QuoteTdsAsync(bill, customerId, orgId, ct);
        if (tdsRefusal is not null)
        {
            return tdsRefusal;
        }

        bill.TdsSectionCode = tds is { Applies: true } ? tds.SectionCode : null;
        bill.TdsBase = tds is { Applies: true } ? tds.TdsBase : 0m;
        bill.TdsAmount = tds is { Applies: true } ? tds.TdsAmount : 0m;
        bill.TdsNote = tds is { Applies: true, Note: { } note } ? (note.Length <= 300 ? note : note[..300]) : null;

        List<BillDetail> stockLines = bill.Lines
            .Where(l => l.LineType == DocumentLineType.Stock && l.ItemId.HasValue)
            .OrderBy(l => l.LineNumber)
            .ToList();

        // Only a bill with no receipt behind it moves stock. Against a receipt
        // the goods are already on the shelf, and moving them again would double
        // both the quantity and the inventory asset.
        if (!againstReceipt && stockLines.Count > 0)
        {
            ReceiveStockResponse stock = await _inventory.ReceiveAsync(
                new ReceiveStockRequest
                {
                    CustomerId = customerId,
                    OrgId = orgId,
                    MovementDate = bill.DocumentDate,
                    SourceType = "BIL",
                    SourceId = bill.BillId,
                    Lines = stockLines
                        .Select(l => new ReceiveStockLine
                        {
                            SourceLineId = l.LineNumber,
                            ItemId = l.ItemId!.Value,
                            Quantity = l.Quantity,
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
                    stock.Lines.Where(l => !l.Success)
                        .Select(l => $"line {l.SourceLineId}: {l.Outcome}"));

                return new BillResult(
                    BillOutcome.StockRefused,
                    Detail: refused.Length > 0
                        ? $"Inventory refused the goods — {refused}."
                        : "Inventory refused the goods and gave no reason.");
            }
        }

        // Each line's leg carries its project, the payable and tax the project
        // every line shares (TK-105).
        List<LedgerLegRequest> legs = BuildLegs(bill, againstReceipt)
            .TagProjects(bill.Lines.Select(l => (l.BillDetailId, l.ProjectId)));

        PostLedgerOutcomeResult posted = await _ledger.PostAsync(
            new PostLedgerRequest
            {
                CustomerId = customerId,
                OrgId = orgId,
                TransactionTypeCode = "BIL",
                TransactionId = bill.BillId,
                // The number on the document's face, so the ledger can report it
                // without reaching into this service's schema to look it up.
                DocumentNo = bill.DocumentNo,
                LedgerDate = bill.DocumentDate,
                ContactId = bill.ContactId,
                SourceDocumentId = bill.BillId,
                Legs = legs,

                // The TDS register row, written in the ledger's transaction.
                Tds = tds,
            },
            ct);

        if (!posted.Posted)
        {
            return new BillResult(BillOutcome.PostingRefused, Detail: posted.Detail);
        }

        // The GST register, in this transaction (TK-115). A retried post
        // replaces what an earlier attempt left, as the ledger does.
        await _db.PurchaseRegister
            .Where(r => r.TransactionTypeCode == "BIL" && r.SourceId == bill.BillId)
            .ExecuteDeleteLoggedAsync(_db, ct);
        _db.PurchaseRegister.AddRange(PurchaseRegisterRowsRepository.ForBill(bill));

        // Each capital line goes onto the fixed asset register, and Accounting
        // reclassifies its cost out of the shared Fixed Asset account into its
        // category's (D-19). After the ledger, because the reclassification
        // moves what the bill's own leg put there. A refusal leaves the bill a
        // draft: posting again replaces the ledger legs, and the register adds
        // only the lines it does not already hold.
        List<BillDetail> capitalLines = bill.Lines
            .Where(l => l.LineType == DocumentLineType.Capital
                && l.FixedAssetCategoryId.HasValue
                && l.TaxableAmount > 0)
            .OrderBy(l => l.LineNumber)
            .ToList();

        if (capitalLines.Count > 0)
        {
            CapitaliseBillOutcome registered = await _fixedAssets.CapitaliseBillAsync(
                new CapitaliseBillRequest
                {
                    CustomerId = customerId,
                    OrgId = orgId,
                    PurchaseBillId = bill.BillId,
                    DocumentNo = bill.DocumentNo ?? $"BIL-{bill.BillId}",
                    DocumentDate = bill.DocumentDate,
                    Lines = capitalLines
                        .Select(l => new CapitaliseBillLine
                        {
                            BillDetailId = l.BillDetailId,
                            LineNumber = l.LineNumber,
                            FixedAssetCategoryId = l.FixedAssetCategoryId!.Value,
                            Description = string.IsNullOrWhiteSpace(l.Description)
                                ? $"Line {l.LineNumber}"
                                : l.Description.Trim(),
                            // The asset's cost, GST included when it cannot be claimed (D-31).
                            Amount = LineCost(l),
                        })
                        .ToList(),
                },
                ct);

            if (!registered.Registered)
            {
                return new BillResult(BillOutcome.PostingRefused, Detail: registered.Detail);
            }
        }

        bill.Status = DocumentStatus.Posted;
        bill.PostedAt = _clock.GetUtcNow();
        bill.PostedBy = _user.UserId;

        await _db.SaveChangesAsync(ct);
        return new BillResult(BillOutcome.Ok, bill.BillId);
    }

    /// <summary>
    /// The legs, in whichever of the two shapes applies.
    ///
    /// Both end the same way: Input GST as an asset, and Accounts Payable
    /// credited against the vendor's own sub-account — without which payables
    /// aging is one number with no vendors in it.
    /// </summary>
    public static List<LedgerLegRequest> BuildLegs(Bill bill, bool againstReceipt)
    {
        List<LedgerLegRequest> legs = [];

        foreach (BillDetail line in bill.Lines.OrderBy(l => l.LineNumber))
        {
            decimal value = line.TaxableAmount;
            if (value == 0)
            {
                continue;
            }

            // GST that cannot be claimed is part of what the line cost (D-31).
            decimal unclaimed = ClaimsCredit(line) ? 0m : TaxOf(line);

            // Against a receipt a line clears the clearing account — that is
            // what the receipt credited. Except a capital line: a receipt takes
            // no capital lines, so one on the same bill was never credited to
            // the clearing account. It lands on Fixed Asset as it would with no
            // receipt, which is also the balance the register reclassifies from;
            // clearing GRNI with it would leave the clearing account short.
            bool clearsReceipt = againstReceipt && line.LineType != DocumentLineType.Capital;

            // Against a receipt the clearing account takes what the receipt put
            // there, the taxable value; the unclaimable tax goes straight to the
            // stock it was paid on. Otherwise it rides on the line's own leg.
            if (clearsReceipt && unclaimed != 0)
            {
                legs.Add(new LedgerLegRequest
                {
                    LedgerTypeId = ItemLedgerType,
                    LedgerSourceId = TransactionLedgerSource,
                    TransactionDetailId = line.BillDetailId,
                    AccountSystemName = InventoryAccount,
                    DebitAmount = unclaimed,
                    TransactionDesc = "GST not claimable, added to stock cost",
                });
            }
            else
            {
                value += unclaimed;
            }

            string account = clearsReceipt
                ? GrniAccount
                : line.LineType switch
                {
                    // A capital line moves no stock and lands on the asset
                    // account. The category will own that mapping once the fixed
                    // asset register exists; until then there is one account.
                    DocumentLineType.Capital => FixedAssetAccount,

                    // An expense line names its own account, so the leg has no
                    // system name to give and Accounting resolves the id.
                    DocumentLineType.Expense => string.Empty,

                    _ => InventoryAccount,
                };

            legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = ItemLedgerType,
                LedgerSourceId = TransactionLedgerSource,
                TransactionDetailId = line.BillDetailId,
                AccountSystemName = account.Length == 0 ? null : account,
                DebitAmount = value,
                TransactionDesc = clearsReceipt ? "Billed against receipt" : "Purchased",
            });
        }

        // Input GST, one leg per component per line, each against the sub-account
        // for that rate. An asset: it is reclaimable, which is the whole
        // difference from the sales side. Only for a line whose credit can be
        // claimed; the rest went into the line's cost above.
        foreach (BillDetail line in bill.Lines.OrderBy(l => l.LineNumber).Where(ClaimsCredit))
        {
            foreach (BillDetailTax tax in line.Taxes)
            {
                if (tax.Amount == 0)
                {
                    continue;
                }

                legs.Add(new LedgerLegRequest
                {
                    LedgerTypeId = TaxLedgerType,
                    LedgerSourceId = TransactionLedgerSource,
                    TransactionDetailId = line.BillDetailId,
                    AccountSystemName = InputGstAccount,
                    SubAccountReferenceType = TaxReference,
                    SubAccountReferenceId = line.TaxMasterId,
                    SubAccountTaxComponent = (int)tax.TaxComponent + 1,
                    DebitAmount = tax.Amount,
                    TransactionDesc = $"Input {tax.TaxComponent}",
                });
            }
        }

        decimal owed = legs.Sum(l => l.DebitAmount);

        // Rounding, when the foot moved. Signed on the document but a leg is
        // never negative, so which side it lands on is the sign.
        if (bill.RoundOffAmount != 0)
        {
            legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = 6,
                LedgerSourceId = TransactionLedgerSource,
                TransactionDetailId = 0,
                AccountSystemName = InputGstAccount,
                DebitAmount = bill.RoundOffAmount > 0 ? bill.RoundOffAmount : 0m,
                CreditAmount = bill.RoundOffAmount < 0 ? -bill.RoundOffAmount : 0m,
                TransactionDesc = "Rounding",
            });

            owed += bill.RoundOffAmount;
        }

        // Reverse charge (D-30): the tax is owed to the government, not the
        // vendor. One credit per component per line, whether or not the line's
        // credit is claimable — the liability is the same either way.
        if (bill.IsReverseCharge)
        {
            foreach (BillDetail line in bill.Lines.OrderBy(l => l.LineNumber))
            {
                foreach (BillDetailTax tax in line.Taxes.Where(t => t.Amount != 0))
                {
                    legs.Add(new LedgerLegRequest
                    {
                        LedgerTypeId = TaxLedgerType,
                        LedgerSourceId = TransactionLedgerSource,
                        TransactionDetailId = line.BillDetailId,
                        AccountSystemName = ReverseChargeAccount,
                        CreditAmount = tax.Amount,
                        TransactionDesc = $"{tax.TaxComponent} payable under reverse charge",
                    });

                    owed -= tax.Amount;
                }
            }
        }

        // TDS (TK-117): withheld from the vendor and owed to the government, so
        // the vendor is owed the net. A tax leg rather than a control one, so
        // the bill's open balance — the sum of its control legs — is the net.
        if (bill.TdsAmount > 0)
        {
            legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = TaxLedgerType,
                LedgerSourceId = TransactionLedgerSource,
                TransactionDetailId = 0,
                AccountSystemName = TdsPayableAccount,
                CreditAmount = bill.TdsAmount,
                TransactionDesc = $"TDS under {bill.TdsSectionCode}",
            });

            owed -= bill.TdsAmount;
        }

        legs.Add(new LedgerLegRequest
        {
            LedgerTypeId = ControlLedgerType,
            LedgerSourceId = TransactionLedgerSource,
            TransactionDetailId = 0,
            AccountSystemName = AccountsPayableAccount,
            SubAccountReferenceType = ContactReference,
            SubAccountReferenceId = bill.ContactId,
            CreditAmount = owed,
            TransactionDesc = "Payable to vendor",
        });

        return legs;
    }

    /// <summary>
    /// What TDS the bill owes (TK-117), on its value before GST. Nothing when
    /// TDS is not wired, or on a foreign-currency bill — a non-resident vendor
    /// is paid under section 195 and returned on 27Q, neither of which is built.
    /// </summary>
    private async Task<(TdsQuote? Quote, BillResult? Refusal)> QuoteTdsAsync(
        Bill bill, Guid customerId, Guid orgId, CancellationToken ct)
    {
        if (_tdsQuotes is null || bill.ExchangeRate != 1m)
        {
            return (null, null);
        }

        decimal taxable = bill.Lines.Sum(l => l.TaxableAmount);
        if (taxable <= 0)
        {
            return (null, null);
        }

        TdsQuoteResult result = await _tdsQuotes.QuoteAsync(new TdsQuoteRequest
        {
            CustomerId = customerId,
            OrgId = orgId,
            ContactId = bill.ContactId,
            TransactionTypeCode = "BIL",
            TransactionId = bill.BillId,
            Date = bill.DocumentDate,
            Amount = taxable,
            Kind = 1,
        }, ct);

        return result.Outcome switch
        {
            TdsQuoteOutcome.Ok => (result.Quote, null),
            TdsQuoteOutcome.Refused => (null, new BillResult(BillOutcome.PostingRefused, bill.BillId, result.Detail)),
            _ => (null, new BillResult(BillOutcome.PostingRefused, bill.BillId,
                "The vendor's TDS could not be worked out, so the bill was not posted. Try again.")),
        };
    }

    /// <summary>What a received unit cost, GST included where it cannot be claimed (D-31).</summary>
    private static decimal NetUnitCost(BillDetail line) =>
        line.Quantity == 0 ? 0m : LineCost(line) / line.Quantity;

    /// <summary>The line's taxable value, plus its GST when that cannot be claimed (D-31).</summary>
    public static decimal LineCost(BillDetail line) =>
        line.TaxableAmount + (ClaimsCredit(line) ? 0m : TaxOf(line));

    public static bool ClaimsCredit(BillDetail line) => line.ItcEligibility == ItcEligibility.Eligible;

    private static decimal TaxOf(BillDetail line) => line.Taxes.Sum(t => t.Amount);

    public async Task<BillResult> VoidAsync(
        long billId, VoidBillRequest request, CancellationToken ct)
    {
        Bill? bill = await _db.Bills.FirstOrDefaultAsync(b => b.BillId == billId, ct);

        if (bill is null)
        {
            return new BillResult(BillOutcome.NotFound);
        }

        bool hasDebitNote = await _db.DebitNotes.AnyAsync(d => d.BillId == billId, ct);

        // A line charged on to a client stands behind that invoice's line (TK-106).
        if (await _db.BillDetails.AnyAsync(l => l.BillId == billId && l.BilledInvoiceId != null, ct))
        {
            return new BillResult(BillOutcome.BilledToClient, billId,
                "A line on this bill has been charged on to a client. Void that invoice first.");
        }

        DocumentTransition transition =
            DocumentLifecycle.CanVoid(bill.Status, hasDebitNote, request.Reason);

        if (!transition.IsAllowed)
        {
            return transition.Outcome == DocumentTransitionOutcome.HasDownstream
                ? new BillResult(BillOutcome.AlreadyCredited, Detail: transition.Detail)
                : new BillResult(BillOutcome.LifecycleRefused, Detail: transition.Detail);
        }

        // A posted bill's capital lines are on the fixed asset register, with
        // their cost reclassified to each category's account. Withdrawing the
        // bill's legs would leave the register and that reclassification
        // standing on nothing, so the asset is disposed of instead.
        if (bill.Status == DocumentStatus.Posted
            && await _db.BillDetails.AnyAsync(
                d => d.BillId == billId && d.LineType == DocumentLineType.Capital, ct))
        {
            return new BillResult(
                BillOutcome.LifecycleRefused,
                Detail: "This bill put fixed assets on the register. Voiding it would leave "
                    + "them there with nothing behind them — dispose of the assets, or raise a "
                    + "debit note to send them back.");
        }

        // A posted bill that moved stock cannot be unwound here, for the same
        // reason a posted receipt cannot: the goods are on the shelf.
        if (bill.Status == DocumentStatus.Posted && !bill.GoodsReceiptId.HasValue)
        {
            return new BillResult(
                BillOutcome.LifecycleRefused,
                Detail: "This bill brought the goods in itself, so the stock is on the shelf. "
                    + "Voiding cannot un-receive it — raise a debit note to send it back.");
        }

        bill.Status = DocumentStatus.Void;
        bill.VoidedAt = _clock.GetUtcNow();
        bill.VoidedBy = _user.UserId;
        bill.VoidReason = request.Reason;

        // A posted bill's ledger rows are withdrawn rather than left behind. The
        // empty leg list with the types named is Accounting's withdrawal shape.
        if (bill.PostedAt is not null)
        {
            (Guid customerId, Guid orgId) = _tenant.Require();

            PostLedgerOutcomeResult withdrawn = await _ledger.PostAsync(
                new PostLedgerRequest
                {
                    CustomerId = customerId,
                    OrgId = orgId,
                    TransactionTypeCode = "BIL",
                    TransactionId = bill.BillId,
                    // The number on the document's face, so the ledger can report it
                    // without reaching into this service's schema to look it up.
                    DocumentNo = bill.DocumentNo,
                    LedgerDate = bill.DocumentDate,
                    ContactId = bill.ContactId,
                    SourceDocumentId = bill.BillId,
                    WithdrawLedgerTypeIds = [ItemLedgerType, TaxLedgerType, ControlLedgerType, 6],
                    Legs = [],
                },
                ct);

            if (!withdrawn.Posted)
            {
                return new BillResult(BillOutcome.PostingRefused, Detail: withdrawn.Detail);
            }

            // Out of the GST register with the ledger rows, as a voided invoice
            // leaves the sales register.
            await _db.PurchaseRegister
                .Where(r => r.TransactionTypeCode == "BIL" && r.SourceId == bill.BillId)
                .ExecuteDeleteLoggedAsync(_db, ct);
        }

        await _db.SaveChangesAsync(ct);
        return new BillResult(BillOutcome.Ok, bill.BillId);
    }

    public async Task<BillView?> GetAsync(long billId, CancellationToken ct)
    {
        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        BillView? bill = await _db.Bills
            .Where(b => b.BillId == billId)
            .Select(b => new BillView
            {
                BillId = b.BillId,
                DocumentNo = b.DocumentNo,
                DocumentDate = b.DocumentDate,
                VendorBillNo = b.VendorBillNo,
                VendorBillDate = b.VendorBillDate,
                DueDate = b.DueDate,
                PurchaseOrderId = b.PurchaseOrderId,
                GoodsReceiptId = b.GoodsReceiptId,
                GoodsReceiptNo = _db.GoodsReceipts
                    .Where(r => r.GoodsReceiptId == b.GoodsReceiptId)
                    .Select(r => r.DocumentNo)
                    .FirstOrDefault(),
                ContactId = b.ContactId,
                CurrencyCode = b.CurrencyCode,
                TaxableAmount = b.TaxableAmount,
                TotalAmount = b.TotalAmount,
                Status = b.Status.ToString(),
                IsInterState = b.IsInterState,
                DaysOverdue = b.Status == DocumentStatus.Posted
                    ? today.DayNumber - b.DueDate.DayNumber
                    : 0,
                ContactGstin = b.ContactGstin,
                PrintTemplateId = b.PrintTemplateId,
                PlaceOfSupplyStateId = b.PlaceOfSupplyStateId,
                PaymentTermId = b.PaymentTermId,
                BillingAddress = b.BillingAddress,
                ShippingAddress = b.ShippingAddress,
                ExchangeRate = b.ExchangeRate,
                SubTotal = b.SubTotal,
                DiscountAmount = b.DiscountAmount,
                CgstAmount = b.CgstAmount,
                SgstAmount = b.SgstAmount,
                IgstAmount = b.IgstAmount,
                CessAmount = b.CessAmount,
                RoundOffAmount = b.RoundOffAmount,
                TotalAmountBase = b.TotalAmountBase,
                LandedCostAmount = b.LandedCostAmount,
                IsReverseCharge = b.IsReverseCharge,
                TdsSectionCode = b.TdsSectionCode,
                TdsBase = b.TdsBase,
                TdsAmount = b.TdsAmount,
                TdsNote = b.TdsNote,
                Notes = b.Notes,
                CustomFields = b.CustomFields,
                TermsAndConditions = b.TermsAndConditions,
                PostedAt = b.PostedAt,
                VoidedAt = b.VoidedAt,
                VoidReason = b.VoidReason,
                Lines = b.Lines
                    .OrderBy(l => l.LineNumber)
                    .Select(l => new BillLineView
                    {
                        BillDetailId = l.BillDetailId,
                        LineNumber = l.LineNumber,
                        GoodsReceiptDetailId = l.GoodsReceiptDetailId,
                        PurchaseOrderDetailId = l.PurchaseOrderDetailId,
                        ItemId = l.ItemId,
                        Description = l.Description,
                        HsnSacCode = l.HsnSacCode,
                        WarehouseId = l.WarehouseId,
                        Quantity = l.Quantity,
                        UomId = l.UomId,
                        ConversionFactor = l.ConversionFactor,
                        BaseQuantity = l.BaseQuantity,
                        ReturnedQuantity = l.ReturnedQuantity,
                        ApportionedLandedCost = l.ApportionedLandedCost,
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
                        ProjectId = l.ProjectId,
                        IsBillable = l.IsBillable,
                        MarkupPercent = l.MarkupPercent,
                        BilledInvoiceId = l.BilledInvoiceId,
                        LineTotal = l.LineTotal,
                        ItemBatchId = l.ItemBatchId,
                        LineNotes = l.LineNotes,
                        ItcEligibility = l.ItcEligibility.ToString(),
                        Taxes = l.Taxes
                            .Select(t => new BillLineTaxView
                            {
                                BillDetailTaxId = t.BillDetailTaxId,
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

        if (bill is null)
        {
            return null;
        }

        await ResolveContactNamesAsync([bill], ct);

        List<long> itemIds = bill.Lines
            .Where(l => l.ItemId.HasValue)
            .Select(l => l.ItemId!.Value)
            .Distinct()
            .ToList();

        if (itemIds.Count > 0)
        {
            IReadOnlyDictionary<long, NamedRef> items = await _itemNames.ResolveAsync(itemIds, ct);
            foreach (BillLineView line in bill.Lines)
            {
                if (line.ItemId.HasValue && items.TryGetValue(line.ItemId.Value, out NamedRef? item))
                {
                    line.ItemLabel = $"{item.Code} - {item.Name}";
                }
            }
        }

        return bill;
    }

    public async Task<List<BillListItem>> ListAsync(CancellationToken ct)
    {
        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        List<BillListItem> bills = await _db.Bills
            .OrderByDescending(b => b.DocumentDate)
            .ThenByDescending(b => b.DocumentNo)
            .Select(b => new BillListItem
            {
                BillId = b.BillId,
                DocumentNo = b.DocumentNo,
                DocumentDate = b.DocumentDate,
                VendorBillNo = b.VendorBillNo,
                VendorBillDate = b.VendorBillDate,
                DueDate = b.DueDate,
                PurchaseOrderId = b.PurchaseOrderId,
                GoodsReceiptId = b.GoodsReceiptId,
                GoodsReceiptNo = _db.GoodsReceipts
                    .Where(r => r.GoodsReceiptId == b.GoodsReceiptId)
                    .Select(r => r.DocumentNo)
                    .FirstOrDefault(),
                ContactId = b.ContactId,
                CurrencyCode = b.CurrencyCode,
                TaxableAmount = b.TaxableAmount,
                TotalAmount = b.TotalAmount,
                ItemCount = b.Lines.Count,
                Status = b.Status.ToString(),
                IsInterState = b.IsInterState,
                DaysOverdue = b.Status == DocumentStatus.Posted
                    ? today.DayNumber - b.DueDate.DayNumber
                    : 0,
            })
            .ToListAsync(ct);

        await ResolveContactNamesAsync(bills, ct);
        return bills;
    }

    private async Task ResolveContactNamesAsync(
        IReadOnlyCollection<BillListItem> bills, CancellationToken ct)
    {
        List<long> contactIds = bills.Select(b => b.ContactId).Distinct().ToList();
        if (contactIds.Count == 0)
        {
            return;
        }

        IReadOnlyDictionary<long, NamedRef> contacts =
            await _contactNames.ResolveAsync(contactIds, ct);

        foreach (BillListItem bill in bills)
        {
            if (contacts.TryGetValue(bill.ContactId, out NamedRef? contact))
            {
                bill.ContactName = contact.Name;
                bill.ContactCode = contact.Code;
            }
        }
    }
}
