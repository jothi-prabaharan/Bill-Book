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
/// The debit note — <c>DBN</c>. The way back out of a purchase.
///
/// <b>It posts <c>Dr Accounts Payable / Cr Purchase Returns</c>, with the input
/// GST reversed.</b> Purchase Returns is a <i>contra Expense</i>: a return
/// reduces what was bought, and booking it as a negative expense would leave
/// every report that groups by account type adding a negative number. The GST
/// leg is a credit because credit cannot be claimed on goods that went back —
/// that is the reversal, not a second claim.
///
/// <b>Only a purchase return moves stock.</b> The other reasons — a price the
/// vendor billed wrong, a discount agreed afterwards, a shortage — are money-only
/// corrections to a bill whose goods stayed on the shelf. Moving stock for those
/// would take away inventory the branch still has.
/// </summary>
public sealed class DebitNoteService
{
    private const string InventoryAccount = "Inventory";
    private const string PurchaseReturnsAccount = "Purchase Returns";
    private const string InputGstAccount = "Input GST";
    private const string AccountsPayableAccount = "Accounts Payable";

    private const int ItemLedgerType = 1;
    private const int TaxLedgerType = 2;
    private const int ControlLedgerType = 3;
    private const int TransactionLedgerSource = 1;

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
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public DebitNoteService(
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

    /// <summary>Whether a reason sends goods back, or only money.</summary>
    private static bool MovesStock(DebitNoteReason reason) =>
        reason == DebitNoteReason.PurchaseReturn;

    public async Task<DebitNoteResult> CreateAsync(
        SaveDebitNoteRequest request, CancellationToken ct)
    {
        string? baseCurrency = await _baseCurrency.GetBaseCurrencyAsync(ct);
        if (baseCurrency is null)
        {
            return new DebitNoteResult(
                DebitNoteOutcome.RatesUnavailable,
                Detail: "Branch base currency could not be read.");
        }

        DebitNote note = new()
        {
            TransactionTypeCode = "DBN",
            DocumentDate = request.DocumentDate,
            CurrencyCode = request.CurrencyCode ?? baseCurrency,
            ExchangeRate = request.ExchangeRate ?? 1m,
            Status = DocumentStatus.Draft,
        };

        DebitNoteResult applied = await ApplyAsync(note, request, ct);
        if (applied.Outcome != DebitNoteOutcome.Ok)
        {
            return applied;
        }

        NumberAllocation alloc = await _numbering.NextAsync("DBN", request.DocumentDate, ct);
        note.DocumentNo = alloc.Code;

        _db.DebitNotes.Add(note);
        await _db.SaveChangesAsync(ct);

        return new DebitNoteResult(DebitNoteOutcome.Ok, note.DebitNoteId);
    }

    public async Task<DebitNoteResult> UpdateAsync(
        long debitNoteId, SaveDebitNoteRequest request, CancellationToken ct)
    {
        DebitNote? note = await _db.DebitNotes
            .Include(n => n.Lines)
            .ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(n => n.DebitNoteId == debitNoteId, ct);

        if (note is null)
        {
            return new DebitNoteResult(DebitNoteOutcome.NotFound);
        }

        DocumentTransition transition = DocumentLifecycle.CanEdit(note.Status);
        if (!transition.IsAllowed)
        {
            return new DebitNoteResult(
                DebitNoteOutcome.LifecycleRefused, Detail: transition.Detail);
        }

        if (request.CurrencyCode is not null)
        {
            note.CurrencyCode = request.CurrencyCode;
        }

        if (request.ExchangeRate.HasValue)
        {
            note.ExchangeRate = request.ExchangeRate.Value;
        }

        note.DocumentDate = request.DocumentDate;

        _db.DebitNoteDetailTaxes.RemoveRange(note.Lines.SelectMany(l => l.Taxes));
        _db.DebitNoteDetails.RemoveRange(note.Lines);
        note.Lines.Clear();

        DebitNoteResult applied = await ApplyAsync(note, request, ct);
        if (applied.Outcome != DebitNoteOutcome.Ok)
        {
            return applied;
        }

        await _db.SaveChangesAsync(ct);
        return new DebitNoteResult(DebitNoteOutcome.Ok, note.DebitNoteId);
    }

    private async Task<DebitNoteResult> ApplyAsync(
        DebitNote note, SaveDebitNoteRequest request, CancellationToken ct)
    {
        BranchSettings? settings = await _branchSettings.GetSettingsAsync(ct);
        if (settings is null)
        {
            return new DebitNoteResult(
                DebitNoteOutcome.RatesUnavailable, Detail: "Branch settings could not be read.");
        }

        PlaceOfSupplyResult pos = PlaceOfSupply.Resolve(
            settings.StateCode, request.PlaceOfSupplyStateCode, request.ContactGstin);

        if (!pos.IsOk)
        {
            return new DebitNoteResult(DebitNoteOutcome.PlaceOfSupplyRefused, Detail: pos.Detail);
        }

        Bill? bill = await _db.Bills
            .Include(b => b.Lines)
            .FirstOrDefaultAsync(b => b.BillId == request.BillId, ct);

        if (bill is null)
        {
            return new DebitNoteResult(
                DebitNoteOutcome.BillInvalid, Detail: "That bill does not exist in this branch.");
        }

        if (bill.ContactId != request.ContactId)
        {
            return new DebitNoteResult(
                DebitNoteOutcome.BillInvalid, Detail: "That bill belongs to a different vendor.");
        }

        // A draft bill owes nothing yet, so there is nothing to debit back.
        if (bill.Status != DocumentStatus.Posted)
        {
            return new DebitNoteResult(
                DebitNoteOutcome.BillInvalid,
                Detail: "That bill has not been posted, so nothing is owed on it yet. Correct "
                    + "the bill itself rather than raising a debit note against it.");
        }

        note.BillId = request.BillId;
        note.ReasonCode = request.ReasonCode;
        note.ContactId = request.ContactId;
        note.ContactGstin = request.ContactGstin;
        note.IsInterState = pos.IsInterState;
        note.Notes = request.Notes;

        TaxContext taxContext = new(pos.IsInterState, settings.DiscountBeforeTax);
        List<TaxLineResult> taxLines = new(request.Lines.Count);

        // What each bill line has left, decremented as this note's own lines are
        // read — so two lines against the same bill line cannot each take the
        // whole remaining quantity.
        Dictionary<long, decimal> remaining = bill.Lines.ToDictionary(
            l => l.BillDetailId,
            l => l.Quantity - l.ReturnedQuantity);

        for (int i = 0; i < request.Lines.Count; i++)
        {
            SaveDebitNoteLineRequest lineReq = request.Lines[i];
            int lineNumber = i + 1;

            BillDetail? billLine = bill.Lines
                .FirstOrDefault(l => l.BillDetailId == lineReq.BillDetailId);

            if (billLine is null)
            {
                return new DebitNoteResult(
                    DebitNoteOutcome.BillInvalid,
                    Detail: $"Line {lineNumber} names a bill line that is not on that bill.");
            }

            if (!remaining.TryGetValue(lineReq.BillDetailId, out decimal left)
                || lineReq.Quantity > left)
            {
                return new DebitNoteResult(
                    DebitNoteOutcome.OverReturned,
                    Detail: $"Line {lineNumber} returns {lineReq.Quantity} but only {left} is "
                        + "left on that bill line. Returning more than was bought would credit "
                        + "input tax twice.");
            }

            remaining[lineReq.BillDetailId] = left - lineReq.Quantity;

            TaxRate? rate = null;
            if (lineReq.TaxGroupId is long taxGroupId)
            {
                rate = await _rates.GetRateAsync(taxGroupId, request.DocumentDate, ct);
                if (rate is null)
                {
                    return new DebitNoteResult(
                        DebitNoteOutcome.RatesUnavailable,
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

            DebitNoteDetail detail = new()
            {
                LineNumber = lineNumber,
                BillDetailId = lineReq.BillDetailId,
                ItemId = lineReq.ItemId ?? billLine.ItemId,
                Description = lineReq.Description ?? billLine.Description,
                HsnSacCode = lineReq.HsnSacCode ?? billLine.HsnSacCode,
                WarehouseId = lineReq.WarehouseId ?? billLine.WarehouseId,
                Quantity = lineReq.Quantity,
                UomId = lineReq.UomId ?? billLine.UomId,
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
                AccountId = lineReq.AccountId ?? billLine.AccountId,
                FixedAssetCategoryId = lineReq.FixedAssetCategoryId,
                LineTotal = computed.LineTotal,
                ItemBatchId = lineReq.ItemBatchId ?? billLine.ItemBatchId,
                LineNotes = lineReq.LineNotes,
            };

            foreach (TaxComponentAmount component in computed.Components)
            {
                detail.Taxes.Add(new DebitNoteDetailTax
                {
                    TaxComponent = component.Component,
                    Rate = component.Rate,
                    TaxableAmount = component.TaxableAmount,
                    Amount = component.Amount,
                    AmountBase = component.Amount * note.ExchangeRate,
                });
            }

            note.Lines.Add(detail);
        }

        TaxDocumentTotals totals = GstCalculator.Totals(taxLines);

        note.SubTotal = totals.SubTotal;
        note.DiscountAmount = totals.DiscountAmount;
        note.TaxableAmount = totals.TaxableAmount;
        note.CgstAmount = totals.CgstAmount;
        note.SgstAmount = totals.SgstAmount;
        note.IgstAmount = totals.IgstAmount;
        note.CessAmount = totals.CessAmount;
        note.RoundOffAmount =
            Math.Round(totals.TotalAmount, 0, MidpointRounding.AwayFromZero) - totals.TotalAmount;
        note.TotalAmount = totals.TotalAmount + note.RoundOffAmount;
        note.TotalAmountBase = note.TotalAmount * note.ExchangeRate;

        return new DebitNoteResult(DebitNoteOutcome.Ok, note.DebitNoteId);
    }

    /// <summary>
    /// Posts the note: stock back if the reason sends it, then the ledger, then
    /// the bill's returned quantities and the note's status.
    ///
    /// The same order as the receipt and the bill, for the same reason: the call
    /// that can genuinely refuse goes first, so a refusal leaves nothing to undo.
    /// </summary>
    public async Task<DebitNoteResult> PostAsync(long debitNoteId, CancellationToken ct)
    {
        DebitNote? note = await _db.DebitNotes
            .Include(n => n.Lines)
            .ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(n => n.DebitNoteId == debitNoteId, ct);

        if (note is null)
        {
            return new DebitNoteResult(DebitNoteOutcome.NotFound);
        }

        DocumentTransition transition = DocumentLifecycle.CanPost(note.Status, note.Lines.Count);
        if (!transition.IsAllowed)
        {
            return new DebitNoteResult(
                DebitNoteOutcome.LifecycleRefused, Detail: transition.Detail);
        }

        (Guid customerId, Guid orgId) = _tenant.Require();

        // Whatever the goods were carrying when they left. Inventory answers it,
        // because only Inventory knows which layers the units came off.
        decimal returnedValue = 0m;

        if (MovesStock(note.ReasonCode))
        {
            List<DebitNoteDetail> stockLines = note.Lines
                .Where(l => l.LineType == DocumentLineType.Stock && l.ItemId.HasValue)
                .OrderBy(l => l.LineNumber)
                .ToList();

            if (stockLines.Count > 0)
            {
                ReturnStockResponse stock = await _inventory.ReturnAsync(
                    new ReturnStockRequest
                    {
                        CustomerId = customerId,
                        OrgId = orgId,
                        MovementDate = note.DocumentDate,
                        SourceType = "DBN",
                        SourceId = note.DebitNoteId,
                        Lines = stockLines
                            .Select(l => new ReturnStockLine
                            {
                                SourceLineId = l.LineNumber,
                                ItemId = l.ItemId!.Value,
                                Quantity = l.Quantity,
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

                    return new DebitNoteResult(
                        DebitNoteOutcome.StockRefused,
                        Detail: refused.Length > 0
                            ? $"Inventory refused the return — {refused}."
                            : "Inventory refused the return and gave no reason.");
                }

                returnedValue = stock.TotalValue;
            }
        }

        PostLedgerOutcomeResult posted = await _ledger.PostAsync(
            new PostLedgerRequest
            {
                CustomerId = customerId,
                OrgId = orgId,
                TransactionTypeCode = "DBN",
                TransactionId = note.DebitNoteId,
                // The number on the document's face, so the ledger can report it
                // without reaching into this service's schema to look it up.
                DocumentNo = note.DocumentNo,
                LedgerDate = note.DocumentDate,
                ContactId = note.ContactId,
                SourceDocumentId = note.DebitNoteId,
                Legs = BuildLegs(note, returnedValue),
            },
            ct);

        if (!posted.Posted)
        {
            return new DebitNoteResult(DebitNoteOutcome.PostingRefused, Detail: posted.Detail);
        }

        await AdvanceBillAsync(note, ct);

        note.Status = DocumentStatus.Posted;
        note.PostedAt = _clock.GetUtcNow();
        note.PostedBy = _user.UserId;

        await _db.SaveChangesAsync(ct);
        return new DebitNoteResult(DebitNoteOutcome.Ok, note.DebitNoteId);
    }

    /// <summary>
    /// <c>Dr Accounts Payable / Cr Purchase Returns</c>, with input GST reversed.
    ///
    /// <paramref name="returnedValue"/> is what the goods were carrying when they
    /// left, which is not the same as what they were billed at — the layer they
    /// came off may have been opened at a different cost. When they differ, the
    /// difference is the stock half against the returns half, and both are
    /// posted rather than one being forced to match the other.
    /// </summary>
    private static List<LedgerLegRequest> BuildLegs(DebitNote note, decimal returnedValue)
    {
        List<LedgerLegRequest> legs = [];

        // The vendor owes us back: their payable falls. Against their own
        // sub-account, so the reduction lands on the right vendor's aging.
        legs.Add(new LedgerLegRequest
        {
            LedgerTypeId = ControlLedgerType,
            LedgerSourceId = TransactionLedgerSource,
            TransactionDetailId = 0,
            AccountSystemName = AccountsPayableAccount,
            SubAccountReferenceType = ContactReference,
            SubAccountReferenceId = note.ContactId,
            DebitAmount = note.TotalAmount,
            TransactionDesc = "Debit note against vendor",
        });

        // The purchase is reduced. Contra Expense, so a report subtracts it
        // rather than adding a negative.
        foreach (DebitNoteDetail line in note.Lines.OrderBy(l => l.LineNumber))
        {
            if (line.TaxableAmount == 0)
            {
                continue;
            }

            legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = ItemLedgerType,
                LedgerSourceId = TransactionLedgerSource,
                TransactionDetailId = line.DebitNoteDetailId,
                AccountSystemName = PurchaseReturnsAccount,
                CreditAmount = line.TaxableAmount,
                TransactionDesc = "Purchase return",
            });
        }

        // Input GST reversed. A credit: credit cannot be claimed on goods that
        // went back, so this undoes the claim rather than making another.
        foreach (DebitNoteDetail line in note.Lines.OrderBy(l => l.LineNumber))
        {
            foreach (DebitNoteDetailTax tax in line.Taxes)
            {
                if (tax.Amount == 0)
                {
                    continue;
                }

                legs.Add(new LedgerLegRequest
                {
                    LedgerTypeId = TaxLedgerType,
                    LedgerSourceId = TransactionLedgerSource,
                    TransactionDetailId = line.DebitNoteDetailId,
                    AccountSystemName = InputGstAccount,
                    SubAccountReferenceType = TaxReference,
                    SubAccountReferenceId = line.TaxMasterId,
                    SubAccountTaxComponent = (int)tax.TaxComponent + 1,
                    CreditAmount = tax.Amount,
                    TransactionDesc = $"Input {tax.TaxComponent} reversed",
                });
            }
        }

        // The stock actually left at what its layers were carrying, which the
        // billed price need not equal. The difference lands on Purchase Returns —
        // the account whose whole job is what a return did to the cost of buying.
        decimal balance = legs.Sum(l => l.DebitAmount) - legs.Sum(l => l.CreditAmount);

        if (returnedValue > 0)
        {
            legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = ItemLedgerType,
                LedgerSourceId = TransactionLedgerSource,
                TransactionDetailId = 0,
                AccountSystemName = InventoryAccount,
                CreditAmount = returnedValue,
                TransactionDesc = "Stock returned",
            });

            legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = ItemLedgerType,
                LedgerSourceId = TransactionLedgerSource,
                TransactionDetailId = 0,
                AccountSystemName = PurchaseReturnsAccount,
                DebitAmount = returnedValue,
                TransactionDesc = "Cost of goods returned",
            });
        }

        if (balance != 0)
        {
            legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = 6,
                LedgerSourceId = TransactionLedgerSource,
                TransactionDetailId = 0,
                AccountSystemName = PurchaseReturnsAccount,
                DebitAmount = balance < 0 ? -balance : 0m,
                CreditAmount = balance > 0 ? balance : 0m,
                TransactionDesc = "Rounding",
            });
        }

        return legs;
    }

    /// <summary>Adds what went back to the bill's running returned quantities.</summary>
    private async Task AdvanceBillAsync(DebitNote note, CancellationToken ct)
    {
        Bill? bill = await _db.Bills
            .Include(b => b.Lines)
            .FirstOrDefaultAsync(b => b.BillId == note.BillId, ct);

        if (bill is null)
        {
            return;
        }

        foreach (DebitNoteDetail line in note.Lines)
        {
            BillDetail? billLine = bill.Lines
                .FirstOrDefault(l => l.BillDetailId == line.BillDetailId);

            if (billLine is not null)
            {
                billLine.ReturnedQuantity += line.Quantity;
            }
        }
    }

    public async Task<DebitNoteResult> VoidAsync(
        long debitNoteId, VoidDebitNoteRequest request, CancellationToken ct)
    {
        DebitNote? note = await _db.DebitNotes
            .FirstOrDefaultAsync(n => n.DebitNoteId == debitNoteId, ct);

        if (note is null)
        {
            return new DebitNoteResult(DebitNoteOutcome.NotFound);
        }

        DocumentTransition transition =
            DocumentLifecycle.CanVoid(note.Status, hasDownstream: false, request.Reason);

        if (!transition.IsAllowed)
        {
            return new DebitNoteResult(
                DebitNoteOutcome.LifecycleRefused, Detail: transition.Detail);
        }

        // A posted note that sent goods back cannot be unwound here: the stock
        // has left. A note that only moved money can be, because withdrawing its
        // rows puts the payable back exactly as it was.
        if (note.Status == DocumentStatus.Posted && MovesStock(note.ReasonCode))
        {
            return new DebitNoteResult(
                DebitNoteOutcome.LifecycleRefused,
                Detail: "This note sent goods back, so the stock has left. Voiding cannot bring "
                    + "it in — receive it again on a new goods receipt if the vendor returns it.");
        }

        note.Status = DocumentStatus.Void;
        note.VoidedAt = _clock.GetUtcNow();
        note.VoidedBy = _user.UserId;
        note.VoidReason = request.Reason;

        if (note.PostedAt is not null)
        {
            (Guid customerId, Guid orgId) = _tenant.Require();

            PostLedgerOutcomeResult withdrawn = await _ledger.PostAsync(
                new PostLedgerRequest
                {
                    CustomerId = customerId,
                    OrgId = orgId,
                    TransactionTypeCode = "DBN",
                    TransactionId = note.DebitNoteId,
                    // The number on the document's face, so the ledger can report it
                    // without reaching into this service's schema to look it up.
                    DocumentNo = note.DocumentNo,
                    LedgerDate = note.DocumentDate,
                    ContactId = note.ContactId,
                    SourceDocumentId = note.DebitNoteId,
                    WithdrawLedgerTypeIds = [ItemLedgerType, TaxLedgerType, ControlLedgerType, 6],
                    Legs = [],
                },
                ct);

            if (!withdrawn.Posted)
            {
                return new DebitNoteResult(
                    DebitNoteOutcome.PostingRefused, Detail: withdrawn.Detail);
            }
        }

        await _db.SaveChangesAsync(ct);
        return new DebitNoteResult(DebitNoteOutcome.Ok, note.DebitNoteId);
    }

    public async Task<DebitNoteView?> GetAsync(long debitNoteId, CancellationToken ct)
    {
        DebitNoteView? note = await _db.DebitNotes
            .Where(n => n.DebitNoteId == debitNoteId)
            .Select(n => new DebitNoteView
            {
                DebitNoteId = n.DebitNoteId,
                DocumentNo = n.DocumentNo,
                DocumentDate = n.DocumentDate,
                BillId = n.BillId,
                BillNo = _db.Bills.Where(b => b.BillId == n.BillId)
                    .Select(b => b.DocumentNo).FirstOrDefault(),
                VendorBillNo = _db.Bills.Where(b => b.BillId == n.BillId)
                    .Select(b => b.VendorBillNo).FirstOrDefault(),
                ReasonCode = n.ReasonCode.ToString(),
                ContactId = n.ContactId,
                CurrencyCode = n.CurrencyCode,
                TaxableAmount = n.TaxableAmount,
                TotalAmount = n.TotalAmount,
                Status = n.Status.ToString(),
                IsInterState = n.IsInterState,
                MovesStock = n.ReasonCode == DebitNoteReason.PurchaseReturn,
                ContactGstin = n.ContactGstin,
                PlaceOfSupplyStateId = n.PlaceOfSupplyStateId,
                ExchangeRate = n.ExchangeRate,
                SubTotal = n.SubTotal,
                DiscountAmount = n.DiscountAmount,
                CgstAmount = n.CgstAmount,
                SgstAmount = n.SgstAmount,
                IgstAmount = n.IgstAmount,
                CessAmount = n.CessAmount,
                RoundOffAmount = n.RoundOffAmount,
                TotalAmountBase = n.TotalAmountBase,
                Notes = n.Notes,
                PostedAt = n.PostedAt,
                VoidedAt = n.VoidedAt,
                VoidReason = n.VoidReason,
                Lines = n.Lines
                    .OrderBy(l => l.LineNumber)
                    .Select(l => new DebitNoteLineView
                    {
                        DebitNoteDetailId = l.DebitNoteDetailId,
                        LineNumber = l.LineNumber,
                        BillDetailId = l.BillDetailId,
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
                        Taxes = l.Taxes
                            .Select(t => new DebitNoteLineTaxView
                            {
                                DebitNoteDetailTaxId = t.DebitNoteDetailTaxId,
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

        if (note is null)
        {
            return null;
        }

        await ResolveContactNamesAsync([note], ct);

        List<long> itemIds = note.Lines
            .Where(l => l.ItemId.HasValue)
            .Select(l => l.ItemId!.Value)
            .Distinct()
            .ToList();

        if (itemIds.Count > 0)
        {
            IReadOnlyDictionary<long, NamedRef> items = await _itemNames.ResolveAsync(itemIds, ct);
            foreach (DebitNoteLineView line in note.Lines)
            {
                if (line.ItemId.HasValue && items.TryGetValue(line.ItemId.Value, out NamedRef? item))
                {
                    line.ItemLabel = $"{item.Code} - {item.Name}";
                }
            }
        }

        return note;
    }

    public async Task<List<DebitNoteListItem>> ListAsync(CancellationToken ct)
    {
        List<DebitNoteListItem> notes = await _db.DebitNotes
            .OrderByDescending(n => n.DocumentDate)
            .ThenByDescending(n => n.DocumentNo)
            .Select(n => new DebitNoteListItem
            {
                DebitNoteId = n.DebitNoteId,
                DocumentNo = n.DocumentNo,
                DocumentDate = n.DocumentDate,
                BillId = n.BillId,
                BillNo = _db.Bills.Where(b => b.BillId == n.BillId)
                    .Select(b => b.DocumentNo).FirstOrDefault(),
                VendorBillNo = _db.Bills.Where(b => b.BillId == n.BillId)
                    .Select(b => b.VendorBillNo).FirstOrDefault(),
                ReasonCode = n.ReasonCode.ToString(),
                ContactId = n.ContactId,
                CurrencyCode = n.CurrencyCode,
                TaxableAmount = n.TaxableAmount,
                TotalAmount = n.TotalAmount,
                Status = n.Status.ToString(),
                IsInterState = n.IsInterState,
                MovesStock = n.ReasonCode == DebitNoteReason.PurchaseReturn,
            })
            .ToListAsync(ct);

        await ResolveContactNamesAsync(notes, ct);
        return notes;
    }

    private async Task ResolveContactNamesAsync(
        IReadOnlyCollection<DebitNoteListItem> notes, CancellationToken ct)
    {
        List<long> contactIds = notes.Select(n => n.ContactId).Distinct().ToList();
        if (contactIds.Count == 0)
        {
            return;
        }

        IReadOnlyDictionary<long, NamedRef> contacts =
            await _contactNames.ResolveAsync(contactIds, ct);

        foreach (DebitNoteListItem note in notes)
        {
            if (contacts.TryGetValue(note.ContactId, out NamedRef? contact))
            {
                note.ContactName = contact.Name;
                note.ContactCode = contact.Code;
            }
        }
    }
}
