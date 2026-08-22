using Microsoft.EntityFrameworkCore;
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
public sealed class BillService
{
    private const string InventoryAccount = "Inventory";
    private const string GrniAccount = "Goods Received Not Invoiced";
    private const string InputGstAccount = "Input GST";
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
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public BillService(
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
        _paymentTerms = paymentTerms;
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

        _db.BillDetailTaxes.RemoveRange(bill.Lines.SelectMany(l => l.Taxes));
        _db.BillDetails.RemoveRange(bill.Lines);
        bill.Lines.Clear();

        BillResult applied = await ApplyAsync(bill, request, ct);
        if (applied.Outcome != BillOutcome.Ok)
        {
            return applied;
        }

        await _db.SaveChangesAsync(ct);
        return new BillResult(BillOutcome.Ok, bill.BillId);
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

        bill.PurchaseOrderId = request.PurchaseOrderId;
        bill.GoodsReceiptId = request.GoodsReceiptId;
        bill.VendorBillNo = request.VendorBillNo;
        bill.VendorBillDate = request.VendorBillDate;
        bill.PaymentTermId = request.PaymentTermId;
        bill.DueDate = dueDate.Value;
        bill.LandedCostAmount = request.LandedCostAmount;
        bill.ContactId = request.ContactId;
        bill.ContactGstin = request.ContactGstin;
        bill.BillingAddress = request.BillingAddress;
        bill.ShippingAddress = request.ShippingAddress;
        bill.IsInterState = pos.IsInterState;
        bill.Notes = request.Notes;
        bill.TermsAndConditions = request.TermsAndConditions;

        TaxContext taxContext = new(pos.IsInterState, settings.DiscountBeforeTax);
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
            if (receipt is not null)
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
                LineTotal = computed.LineTotal,
                ItemBatchId = lineReq.ItemBatchId,
                LineNotes = lineReq.LineNotes,
                ApportionedLandedCost = 0m,
                ReturnedQuantity = 0m,
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
        bill.RoundOffAmount =
            Math.Round(totals.TotalAmount, 0, MidpointRounding.AwayFromZero) - totals.TotalAmount;
        bill.TotalAmount = totals.TotalAmount + bill.RoundOffAmount;
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

        (Guid customerId, Guid orgId) = _tenant.Require();

        bool againstReceipt = bill.GoodsReceiptId.HasValue;

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

        List<LedgerLegRequest> legs = BuildLegs(bill, againstReceipt);

        PostLedgerOutcomeResult posted = await _ledger.PostAsync(
            new PostLedgerRequest
            {
                CustomerId = customerId,
                OrgId = orgId,
                TransactionTypeCode = "BIL",
                TransactionId = bill.BillId,
                LedgerDate = bill.DocumentDate,
                ContactId = bill.ContactId,
                SourceDocumentId = bill.BillId,
                Legs = legs,
            },
            ct);

        if (!posted.Posted)
        {
            return new BillResult(BillOutcome.PostingRefused, Detail: posted.Detail);
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
    private static List<LedgerLegRequest> BuildLegs(Bill bill, bool againstReceipt)
    {
        List<LedgerLegRequest> legs = [];

        foreach (BillDetail line in bill.Lines.OrderBy(l => l.LineNumber))
        {
            decimal value = line.TaxableAmount;
            if (value == 0)
            {
                continue;
            }

            // Against a receipt every line clears the clearing account, whatever
            // kind of line it is — that is what the receipt credited.
            string account = againstReceipt
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
                TransactionDesc = againstReceipt ? "Billed against receipt" : "Purchased",
            });
        }

        // Input GST, one leg per component per line, each against the sub-account
        // for that rate. An asset: it is reclaimable, which is the whole
        // difference from the sales side.
        foreach (BillDetail line in bill.Lines.OrderBy(l => l.LineNumber))
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

    private static decimal NetUnitCost(BillDetail line) =>
        line.Quantity == 0 ? 0m : line.TaxableAmount / line.Quantity;

    public async Task<BillResult> VoidAsync(
        long billId, VoidBillRequest request, CancellationToken ct)
    {
        Bill? bill = await _db.Bills.FirstOrDefaultAsync(b => b.BillId == billId, ct);

        if (bill is null)
        {
            return new BillResult(BillOutcome.NotFound);
        }

        bool hasDebitNote = await _db.DebitNotes.AnyAsync(d => d.BillId == billId, ct);

        DocumentTransition transition =
            DocumentLifecycle.CanVoid(bill.Status, hasDebitNote, request.Reason);

        if (!transition.IsAllowed)
        {
            return transition.Outcome == DocumentTransitionOutcome.HasDownstream
                ? new BillResult(BillOutcome.AlreadyCredited, Detail: transition.Detail)
                : new BillResult(BillOutcome.LifecycleRefused, Detail: transition.Detail);
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
                Notes = b.Notes,
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
                        LineTotal = l.LineTotal,
                        ItemBatchId = l.ItemBatchId,
                        LineNotes = l.LineNotes,
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
