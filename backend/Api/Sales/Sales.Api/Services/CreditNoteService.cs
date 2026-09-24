using Shared.Kernel.Storage;
using Microsoft.EntityFrameworkCore;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tax;
using Shared.Kernel.Tenancy;
using Shared.Kernel.Interfaces;
using Sales.Entity.Enums;

namespace Sales.Api.Services;

public sealed class CreditNoteService
{
    // The chart's own names — SystemAccountNames in Accounting. The ledger finds
    // an account by exact name. "Sales Returns" and "Round Off" were not seeded
    // and "Tax Payable" exists in no chart, so every credit note was refused
    // (TK-77). SalesAccountNameTests holds every name here to Accounting's seed.
    private const string AccountsReceivableAccount = "Accounts Receivable";
    private const string SalesReturnsAccount = "Sales Returns";
    private const string OutputGstAccount = "Output GST";
    private const string RoundOffAccount = "Round Off";

    private const int ItemLedgerType = 1;
    private const int TaxLedgerType = 2;
    private const int ControlLedgerType = 3;
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
    private readonly Sales.Api.Services.Pdf.SalesDocumentArchive _archive;

    public CreditNoteService(
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
        Sales.Api.Services.Pdf.SalesDocumentArchive archive)
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
        _archive = archive;
    }

    public async Task<IReadOnlyList<CreditNoteListItem>> ListAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var query = _db.CreditNotes.AsNoTracking();

        if (from.HasValue) query = query.Where(x => x.DocumentDate >= from.Value);
        if (to.HasValue) query = query.Where(x => x.DocumentDate <= to.Value);

        var list = await query
            .OrderByDescending(x => x.DocumentDate)
            .ThenByDescending(x => x.CreditNoteId)
            .Select(x => new
            {
                x.CreditNoteId,
                x.InvoiceId,
                x.DocumentDate,
                x.DocumentNo,
                x.ContactId,
                x.Status,
                x.TotalAmount
            })
            .ToListAsync(ct);

        var contactIds = list.Select(x => x.ContactId).Distinct().ToList();
        var contacts = await _contactNames.ResolveAsync(contactIds, ct);

        return list.Select(x => new CreditNoteListItem
        {
            CreditNoteId = x.CreditNoteId,
            InvoiceId = x.InvoiceId,
            DocumentDate = x.DocumentDate,
            DocumentNo = x.DocumentNo,
            ContactId = x.ContactId,
            ContactName = contacts.TryGetValue(x.ContactId, out var c) ? c.Name : "Unknown",
            Status = x.Status.ToString(),
            TotalAmount = x.TotalAmount
        }).ToList();
    }

    public async Task<CreditNoteView?> GetAsync(long id, CancellationToken ct)
    {
        var creditNote = await _db.CreditNotes
            .Include(x => x.Lines.OrderBy(l => l.LineNumber))
                .ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(x => x.CreditNoteId == id, ct);

        if (creditNote == null) return null;

        var contactName = (await _contactNames.ResolveAsync([creditNote.ContactId], ct))
            .TryGetValue(creditNote.ContactId, out var contact) ? contact.Name : "Unknown";

        var itemIds = creditNote.Lines.Select(l => l.ItemId).Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
        var itemNames = await _itemNames.ResolveAsync(itemIds, ct);

        return new CreditNoteView
        {
            CreditNoteId = creditNote.CreditNoteId,
            InvoiceId = creditNote.InvoiceId,
            DocumentDate = creditNote.DocumentDate,
            DocumentNo = creditNote.DocumentNo,
            ContactId = creditNote.ContactId,
            ContactName = contactName,
            Status = creditNote.Status.ToString(),
            ReasonCode = creditNote.ReasonCode,
            ContactGstin = creditNote.ContactGstin,
            VoidReason = creditNote.VoidReason,
            CurrencyCode = creditNote.CurrencyCode,
            ExchangeRate = creditNote.ExchangeRate,
            Notes = creditNote.Notes,
            BillingAddress = creditNote.BillingAddress,
            ShippingAddress = creditNote.ShippingAddress,
            PlaceOfSupplyStateId = creditNote.PlaceOfSupplyStateId,
            IsInterState = creditNote.IsInterState,
            SubTotal = creditNote.SubTotal,
            DiscountAmount = creditNote.DiscountAmount,
            TaxableAmount = creditNote.TaxableAmount,
            CgstAmount = creditNote.CgstAmount,
            SgstAmount = creditNote.SgstAmount,
            IgstAmount = creditNote.IgstAmount,
            CessAmount = creditNote.CessAmount,
            RoundOffAmount = creditNote.RoundOffAmount,
            TotalAmount = creditNote.TotalAmount,
            TotalAmountBase = creditNote.TotalAmountBase,
            Lines = creditNote.Lines.Select(l => new CreditNoteLineView
            {
                CreditNoteDetailId = l.CreditNoteDetailId,
                InvoiceDetailId = l.InvoiceDetailId,
                ItemId = l.ItemId,
                ItemLabel = l.ItemId.HasValue && itemNames.TryGetValue(l.ItemId.Value, out var itemName) ? itemName.Name : null,
                HsnSacCode = l.HsnSacCode,
                Description = l.Description,
                TaxGroupId = l.TaxGroupId,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                DiscountPercent = l.DiscountPercent ?? 0m,
                DiscountAmount = l.DiscountAmount,
                LineTotal = l.LineTotal,
                TaxAmount = l.TaxAmount,
                Taxes = l.Taxes.Select(t => new CreditNoteLineTaxView
                {
                    TaxComponent = t.TaxComponent,
                    SubAccountId = t.SubAccountId,
                    Amount = t.Amount
                }).ToList()
            }).ToList()
        };
    }

    public async Task<CreditNoteResult> SaveAsync(
        long? creditNoteId, SaveCreditNoteRequest request, CancellationToken ct)
    {
        var (_, orgId) = _tenant.Require();

        CreditNote creditNote;
        if (creditNoteId.HasValue)
        {
            CreditNote? existing = await _db.CreditNotes
                .Include(x => x.Lines)
                    .ThenInclude(l => l.Taxes)
                .FirstOrDefaultAsync(x => x.CreditNoteId == creditNoteId.Value, ct);

            if (existing is null)
            {
                return new CreditNoteResult(CreditNoteOutcome.NotFound);
            }

            DocumentTransition edit = DocumentLifecycle.CanEdit(existing.Status);
            if (!edit.IsAllowed)
            {
                return new CreditNoteResult(
                    CreditNoteOutcome.LifecycleRefused, existing.CreditNoteId, edit.Detail);
            }

            creditNote = existing;
        }
        else
        {
            creditNote = new CreditNote();
        }

        // Everything that can refuse is checked before anything changes, so a
        // refused edit leaves the draft as it was.
        Invoice? invoice = await _db.Invoices
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.InvoiceId == request.InvoiceId, ct);

        CreditNoteResult? linkRefusal = CheckInvoiceLink(
            invoice, request.ContactId, request.Lines.Select(l => (l.InvoiceDetailId, l.ItemId)));
        if (linkRefusal is not null)
        {
            return linkRefusal with { CreditNoteId = creditNoteId ?? 0 };
        }

        BranchSettings? settings = await _branchSettings.GetSettingsAsync(ct);
        if (settings is null)
        {
            return new CreditNoteResult(
                CreditNoteOutcome.RatesUnavailable,
                Detail: "The branch's settings could not be read. Try again in a moment.");
        }

        // Resolved once, the same way Invoice and SalesOrder resolve it — a
        // GSTIN that disagrees with the stated place of supply is refused
        // rather than guessed at. A credit note reverses what the invoice
        // charged, so getting the head of tax wrong here is getting the
        // reversal wrong too.
        PlaceOfSupplyResult pos = PlaceOfSupply.Resolve(
            settings.StateCode, request.PlaceOfSupplyStateCode, request.ContactGstin);
        if (!pos.IsOk)
        {
            return new CreditNoteResult(CreditNoteOutcome.PlaceOfSupplyRefused, Detail: pos.Detail);
        }

        var rates = new List<TaxRate?>(request.Lines.Count);
        foreach (SaveCreditNoteLineRequest reqLine in request.Lines)
        {
            long? taxGroupId = TaxGroupOf(reqLine);
            TaxRate? rate = null;

            if (taxGroupId.HasValue)
            {
                rate = await _rates.GetRateAsync(taxGroupId.Value, request.DocumentDate, ct);
                if (rate is null)
                {
                    return new CreditNoteResult(
                        CreditNoteOutcome.RatesUnavailable,
                        Detail: "A tax rate on this credit note could not be read for its date. "
                            + "Try again in a moment.");
                }
            }

            rates.Add(rate);
        }

        string? baseCurrency = await _baseCurrency.GetBaseCurrencyAsync(ct);
        if (baseCurrency is null)
        {
            return new CreditNoteResult(
                CreditNoteOutcome.RatesUnavailable,
                Detail: "The branch's base currency could not be read. Try again in a moment.");
        }

        if (creditNoteId.HasValue)
        {
            _db.CreditNoteDetailTaxes.RemoveRange(creditNote.Lines.SelectMany(l => l.Taxes));
            _db.CreditNoteDetails.RemoveRange(creditNote.Lines);
            creditNote.Lines.Clear();
        }
        else
        {
            var alloc = await _numbering.NextAsync("CRN", request.DocumentDate, ct);
            creditNote.OrgId = orgId;
            creditNote.DocumentNo = alloc.Code;
            creditNote.TransactionTypeCode = "CRN";
            creditNote.Status = DocumentStatus.Draft;
            _db.CreditNotes.Add(creditNote);
        }

        TaxContext taxContext = new(pos.IsInterState, settings.DiscountBeforeTax);

        creditNote.InvoiceId = request.InvoiceId;
        creditNote.ReasonCode = request.ReasonCode;
        creditNote.ContactId = request.ContactId;
        creditNote.ContactGstin = request.ContactGstin;
        creditNote.PlaceOfSupplyStateId = 0;
        creditNote.IsInterState = pos.IsInterState;
        creditNote.DocumentDate = request.DocumentDate;
        creditNote.PrintTemplateId = request.PrintTemplateId;
        creditNote.Notes = request.Notes;

        // The branch's own currency when none is given. This defaulted to "USD".
        creditNote.CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode)
            ? baseCurrency
            : request.CurrencyCode;
        creditNote.ExchangeRate = request.ExchangeRate;
        creditNote.BillingAddress = request.BillingAddress;
        creditNote.ShippingAddress = request.ShippingAddress;

        var taxLines = new List<TaxLineResult>(request.Lines.Count);

        for (int i = 0; i < request.Lines.Count; i++)
        {
            SaveCreditNoteLineRequest reqLine = request.Lines[i];
            TaxRate? rate = rates[i];
            long? taxGroupId = TaxGroupOf(reqLine);
            InvoiceDetail invoiceLine = invoice!.Lines.First(l => l.InvoiceDetailId == reqLine.InvoiceDetailId);

            TaxLineInput taxInput = new()
            {
                Quantity = reqLine.Quantity,
                UnitPrice = reqLine.UnitPrice,
                DiscountPercent = reqLine.DiscountPercent > 0 ? reqLine.DiscountPercent : null,
                Rate = rate,
            };

            // BaseQuantity, GrossAmount, TaxableAmount and LineTotal all come
            // from here — computing them by hand next to this call is exactly
            // how they drifted out of step with "chk_creditnotedetails_*" before.
            TaxLineResult computed = GstCalculator.Compute(taxInput, taxContext);
            taxLines.Add(computed);

            var line = new CreditNoteDetail
            {
                OrgId = creditNote.OrgId,
                LineNumber = i + 1,
                InvoiceDetailId = reqLine.InvoiceDetailId,
                ItemId = reqLine.ItemId,
                HsnSacCode = invoiceLine.HsnSacCode,
                Description = invoiceLine.Description,
                Quantity = reqLine.Quantity,
                ConversionFactor = 1m,
                BaseQuantity = computed.BaseQuantity,
                UnitPrice = reqLine.UnitPrice,
                DiscountPercent = reqLine.DiscountPercent,
                DiscountAmount = computed.DiscountAmount,
                GrossAmount = computed.GrossAmount,
                TaxableAmount = computed.TaxableAmount,
                TaxMasterId = rate?.TaxMasterId,
                TaxGroupId = rate?.TaxGroupId ?? taxGroupId,
                TaxAmount = computed.TaxAmount,
                LineTotal = computed.LineTotal,
            };

            foreach (var comp in computed.Components)
            {
                line.Taxes.Add(new CreditNoteDetailTax
                {
                    OrgId = creditNote.OrgId,
                    TaxComponent = comp.Component,
                    SubAccountId = taxGroupId ?? 0,
                    Rate = comp.Rate,
                    TaxableAmount = comp.TaxableAmount,
                    Amount = comp.Amount,
                    AmountBase = comp.Amount * creditNote.ExchangeRate,
                });
            }

            creditNote.Lines.Add(line);
        }

        TaxDocumentTotals totals = GstCalculator.Totals(taxLines);

        creditNote.SubTotal = totals.SubTotal;
        creditNote.DiscountAmount = totals.DiscountAmount;
        creditNote.TaxableAmount = totals.TaxableAmount;
        creditNote.CgstAmount = totals.CgstAmount;
        creditNote.SgstAmount = totals.SgstAmount;
        creditNote.IgstAmount = totals.IgstAmount;
        creditNote.CessAmount = totals.CessAmount;
        creditNote.RoundOffAmount =
            Math.Round(totals.TotalAmount, 0, MidpointRounding.AwayFromZero) - totals.TotalAmount;
        creditNote.TotalAmount = totals.TotalAmount + creditNote.RoundOffAmount;
        creditNote.TotalAmountBase = creditNote.TotalAmount * creditNote.ExchangeRate;

        await _db.SaveChangesAsync(ct);
        return new CreditNoteResult(CreditNoteOutcome.Ok, creditNote.CreditNoteId);
    }

    /// <summary>
    /// Posts the note: claims it against its invoice, takes returned goods back
    /// into stock, and reverses the revenue and the tax.
    ///
    /// <b>The goods come back at what they cost, onto the layers they left.</b>
    /// Each returned line names the invoice line's own issue movement and its
    /// unit cost, and Inventory records the movement as a sales return, which is
    /// what makes the costing engine walk it back to those layers. It used to
    /// send the <i>selling</i> price, and Inventory recorded every line as a
    /// plain receipt, so a return opened a fresh layer at the selling price and
    /// the id naming the original issue was never read.
    ///
    /// <b>It posts no Inventory or cost-of-sales legs.</b> Inventory's costing
    /// worker posts a sourced sales return as Dr Inventory / Cr Cost of Goods Sold
    /// at the returned layers' cost; the note posting its own pair as well would
    /// count the stock back twice.
    ///
    /// Accounting, Inventory and the ledger are three calls to two services, and
    /// none of them is in this transaction. The claim is taken first because it
    /// is the check most likely to refuse; if a later step refuses, the claim is
    /// released again, and a retry is safe — Inventory treats a second receipt for
    /// the same line as already done, and the ledger replaces a document's rows.
    /// </summary>
    public async Task<CreditNoteResult> PostAsync(long creditNoteId, CancellationToken ct)
    {
        var (customerId, _) = _tenant.Require();

        // Where the PDF is filed, resolved before Inventory or Accounting is
        // called: both are over HTTP and outside this transaction, so a token
        // that cannot name a folder is refused before anything is posted (TK-22).
        StorageScope archiveScope = _archive.Scope();

        CreditNote? creditNote = await _db.CreditNotes
            .Include(x => x.Lines)
                .ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(x => x.CreditNoteId == creditNoteId, ct);

        if (creditNote is null)
        {
            return new CreditNoteResult(CreditNoteOutcome.NotFound);
        }

        DocumentTransition post = DocumentLifecycle.CanPost(creditNote.Status, creditNote.Lines.Count);
        if (!post.IsAllowed)
        {
            return new CreditNoteResult(CreditNoteOutcome.LifecycleRefused, creditNoteId, post.Detail);
        }

        // Read again rather than trusted from the save: the invoice may have been
        // voided, or other notes may have returned its goods, since this was drafted.
        Invoice? invoice = await _db.Invoices
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.InvoiceId == creditNote.InvoiceId, ct);

        CreditNoteResult? linkRefusal = CheckInvoiceLink(
            invoice, creditNote.ContactId, creditNote.Lines.Select(l => (l.InvoiceDetailId, l.ItemId)));
        if (linkRefusal is not null)
        {
            return linkRefusal with { CreditNoteId = creditNoteId };
        }

        bool returnsGoods = creditNote.ReasonCode == CreditNoteReason.SalesReturn;
        Dictionary<long, InvoiceDetail> invoiceLines = invoice!.Lines.ToDictionary(l => l.InvoiceDetailId);

        if (returnsGoods)
        {
            foreach (var byInvoiceLine in creditNote.Lines.GroupBy(l => l.InvoiceDetailId))
            {
                InvoiceDetail invoiceLine = invoiceLines[byInvoiceLine.Key];
                decimal left = invoiceLine.Quantity - invoiceLine.ReturnedQuantity;
                decimal returning = byInvoiceLine.Sum(l => l.Quantity);

                if (returning > left)
                {
                    return new CreditNoteResult(
                        CreditNoteOutcome.OverReturned, creditNoteId,
                        $"Invoice line {invoiceLine.LineNumber} has {left:0.####} left that can come back, "
                            + $"and this credit note returns {returning:0.####}.");
                }
            }
        }

        string? baseCurrency = await _baseCurrency.GetBaseCurrencyAsync(ct);
        if (baseCurrency is null)
        {
            return new CreditNoteResult(
                CreditNoteOutcome.RatesUnavailable, creditNoteId,
                "The branch's base currency could not be read. Try again in a moment.");
        }

        // Accounting's own guard re-checks the claim against the invoice's
        // CONTROL net and what has already been allocated to it.
        var allocation = await _ledgerClient.AllocateAsync(new AllocateTransactionRequest
        {
            CustomerId = customerId,
            OrgId = creditNote.OrgId,
            SourceTransactionTypeCode = creditNote.TransactionTypeCode,
            SourceTransactionId = creditNote.CreditNoteId,
            TargetTransactionTypeCode = invoice.TransactionTypeCode,
            TargetTransactionId = creditNote.InvoiceId,
            Amount = creditNote.TotalAmount,
        }, ct);

        if (!allocation.Allocated)
        {
            return new CreditNoteResult(
                CreditNoteOutcome.AllocationRefused, creditNoteId,
                $"The credit note was refused: {allocation.Detail}");
        }

        if (returnsGoods)
        {
            List<ReceiveStockLine> stockLines = creditNote.Lines
                .Where(l => l.ItemId.HasValue
                    && invoiceLines[l.InvoiceDetailId].LineType == DocumentLineType.Stock)
                .Select(l =>
                {
                    InvoiceDetail invoiceLine = invoiceLines[l.InvoiceDetailId];
                    return new ReceiveStockLine
                    {
                        SourceLineId = l.CreditNoteDetailId,
                        ItemId = l.ItemId!.Value,
                        Quantity = l.Quantity,
                        WarehouseId = invoiceLine.WarehouseId,
                        UnitCost = invoiceLine.UnitCost,
                        ReturnsStockMovementId = invoiceLine.StockMovementId,
                    };
                })
                .ToList();

            if (stockLines.Count > 0)
            {
                var received = await _inventoryClient.ReceiveAsync(new ReceiveStockRequest
                {
                    OrgId = creditNote.OrgId,
                    CustomerId = customerId,
                    MovementDate = creditNote.DocumentDate,
                    SourceType = creditNote.TransactionTypeCode,
                    SourceId = creditNote.CreditNoteId,
                    Lines = stockLines,
                }, ct);

                if (!received.Success)
                {
                    await ReleaseClaimAsync(creditNote, customerId, ct);
                    return new CreditNoteResult(
                        CreditNoteOutcome.StockRefused, creditNoteId,
                        "Inventory refused to take the goods on this credit note back. Nothing was posted.");
                }
            }
        }

        var postResult = await _ledgerClient.PostAsync(
            BuildPosting(creditNote, customerId, baseCurrency), ct);
        if (!postResult.Posted)
        {
            await ReleaseClaimAsync(creditNote, customerId, ct);
            return new CreditNoteResult(
                CreditNoteOutcome.PostingRefused, creditNoteId, $"Ledger post failed: {postResult.Detail}");
        }

        if (returnsGoods)
        {
            foreach (CreditNoteDetail l in creditNote.Lines)
            {
                invoiceLines[l.InvoiceDetailId].ReturnedQuantity += l.Quantity;
            }
        }

        foreach (var l in creditNote.Lines)
        {
            var rate = l.Taxes.FirstOrDefault()?.Rate ?? 0;
            _db.SalesRegister.Add(new SalesRegister
            {
                OrgId = creditNote.OrgId,
                TransactionTypeCode = creditNote.TransactionTypeCode,
                SourceId = creditNote.CreditNoteId,
                DocumentNo = creditNote.DocumentNo,
                DocumentDate = creditNote.DocumentDate,
                ContactId = creditNote.ContactId,
                ContactGstin = creditNote.ContactGstin,
                PlaceOfSupplyStateId = creditNote.PlaceOfSupplyStateId,
                IsInterState = creditNote.IsInterState,
                SupplyType = creditNote.ContactGstin != null ? "B2B" : "B2CS",
                ReverseCharge = false,
                HsnSacCode = l.HsnSacCode,
                GstRate = rate,
                // Negative amounts for Credit Notes in SalesRegister
                Quantity = -l.Quantity,
                UqcCode = null,
                TaxableAmount = -l.TaxableAmount,
                CgstAmount = -(l.Taxes.FirstOrDefault(t => t.TaxComponent == TaxComponent.Cgst)?.Amount ?? 0),
                SgstAmount = -(l.Taxes.FirstOrDefault(t => t.TaxComponent == TaxComponent.Sgst)?.Amount ?? 0),
                IgstAmount = -(l.Taxes.FirstOrDefault(t => t.TaxComponent == TaxComponent.Igst)?.Amount ?? 0),
                CessAmount = -(l.Taxes.FirstOrDefault(t => t.TaxComponent == TaxComponent.Cess)?.Amount ?? 0),
                TotalAmount = -(l.LineTotal + l.TaxAmount),
                CurrencyCode = creditNote.CurrencyCode,
                ExchangeRate = creditNote.ExchangeRate,
                TaxableAmountBase = -(l.TaxableAmount * creditNote.ExchangeRate),
                OriginalInvoiceId = creditNote.InvoiceId
            });
        }

        creditNote.Status = DocumentStatus.Posted;
        creditNote.PostedAt = _clock.GetUtcNow();
        creditNote.PostedBy = _user.UserId;

        await _archive.ArchiveAsync(
            archiveScope,
            Sales.Api.Services.Pdf.ArchivedSalesDocument.CreditNote,
            creditNote.CreditNoteId,
            "CREDIT NOTE",
            "Credit Note No",
            creditNote,
            creditNote.Lines,
            invoice is null ? null : $"Against invoice {invoice.DocumentNo} dated {invoice.DocumentDate:dd-MMM-yyyy}",
            ct);

        await _db.SaveChangesAsync(ct);
        return new CreditNoteResult(CreditNoteOutcome.Ok, creditNoteId);
    }

    /// <summary>
    /// Withdraws a credit note, with a reason.
    ///
    /// A draft is simply stamped. A posted note that corrected a price, a
    /// discount or a deficiency withdraws its ledger rows, releases its claim on
    /// the invoice and leaves the GST register — it moved nothing physical.
    ///
    /// <b>A posted sales return is refused.</b> Its goods are back on the shelf;
    /// voiding the paperwork would leave them there with nothing to explain them.
    /// Sell them again on an invoice instead — the same rule a dispatched delivery
    /// challan follows.
    /// </summary>
    public async Task<CreditNoteResult> VoidAsync(
        long creditNoteId, string reason, CancellationToken ct)
    {
        var (customerId, orgId) = _tenant.Require();

        CreditNote? creditNote = await _db.CreditNotes
            .FirstOrDefaultAsync(x => x.CreditNoteId == creditNoteId, ct);

        if (creditNote is null)
        {
            return new CreditNoteResult(CreditNoteOutcome.NotFound);
        }

        bool posted = creditNote.Status == DocumentStatus.Posted;

        if (posted && creditNote.ReasonCode == CreditNoteReason.SalesReturn)
        {
            return new CreditNoteResult(
                CreditNoteOutcome.LifecycleRefused, creditNoteId,
                "The goods on this credit note are back in stock. Voiding it would leave them "
                    + "there with nothing to explain them. Sell them again on an invoice instead.");
        }

        DocumentTransition transition = DocumentLifecycle.CanVoid(creditNote.Status, false, reason);
        if (!transition.IsAllowed)
        {
            return new CreditNoteResult(
                CreditNoteOutcome.LifecycleRefused, creditNoteId, transition.Detail);
        }

        if (posted)
        {
            var withdraw = await _ledgerClient.PostAsync(new PostLedgerRequest
            {
                CustomerId = customerId,
                OrgId = orgId,
                TransactionTypeCode = creditNote.TransactionTypeCode,
                TransactionId = creditNote.CreditNoteId,
                DocumentNo = creditNote.DocumentNo,
                LedgerDate = creditNote.DocumentDate,
                ContactId = creditNote.ContactId,
                SourceDocumentId = creditNote.CreditNoteId,
                WithdrawLedgerTypeIds = [ItemLedgerType, TaxLedgerType, ControlLedgerType, RoundOffLedgerType],
                Legs = [],
            }, ct);

            if (!withdraw.Posted)
            {
                return new CreditNoteResult(
                    CreditNoteOutcome.PostingRefused, creditNoteId, withdraw.Detail);
            }

            var registers = await _db.SalesRegister
                .Where(r => r.SourceId == creditNoteId && r.TransactionTypeCode == creditNote.TransactionTypeCode)
                .ToListAsync(ct);
            _db.SalesRegister.RemoveRange(registers);

            // A voided note takes its claims with it, or the invoice it named stays
            // partly settled by a document that no longer counts.
            await ReleaseClaimAsync(creditNote, customerId, ct);
        }

        creditNote.Status = DocumentStatus.Void;
        creditNote.VoidedAt = _clock.GetUtcNow();

        // Set together with the timestamp: chk_creditnotes_void_stamp requires
        // both or neither, and the void used to write only the stamp.
        creditNote.VoidReason = reason.Trim();
        creditNote.VoidedBy = _user.UserId;

        await _db.SaveChangesAsync(ct);
        return new CreditNoteResult(CreditNoteOutcome.Ok, creditNoteId);
    }

    /// <summary>
    /// The note's link to its invoice: the invoice is posted and for this
    /// customer, and every line is one of its lines, for the same item. Null
    /// when it holds.
    /// </summary>
    private static CreditNoteResult? CheckInvoiceLink(
        Invoice? invoice, long contactId, IEnumerable<(long InvoiceDetailId, long? ItemId)> lines)
    {
        if (invoice is null)
        {
            return new CreditNoteResult(
                CreditNoteOutcome.SourceInvalid,
                Detail: "The invoice this credit note corrects could not be found in this branch.");
        }

        if (invoice.Status != DocumentStatus.Posted)
        {
            return new CreditNoteResult(
                CreditNoteOutcome.SourceInvalid,
                Detail: "A credit note can only correct a posted invoice.");
        }

        if (invoice.ContactId != contactId)
        {
            return new CreditNoteResult(
                CreditNoteOutcome.SourceInvalid,
                Detail: "The credit note's customer is not the invoice's customer.");
        }

        foreach ((long invoiceDetailId, long? itemId) in lines)
        {
            InvoiceDetail? invoiceLine = invoice.Lines.FirstOrDefault(l => l.InvoiceDetailId == invoiceDetailId);
            if (invoiceLine is null)
            {
                return new CreditNoteResult(
                    CreditNoteOutcome.LineInvalid,
                    Detail: "Every line on a credit note must be one of its invoice's lines.");
            }

            if (invoiceLine.ItemId != itemId)
            {
                return new CreditNoteResult(
                    CreditNoteOutcome.LineInvalid,
                    Detail: $"A line's item is not the item on invoice line {invoiceLine.LineNumber}.");
            }
        }

        return null;
    }

    /// <summary>
    /// The reversal of the invoice: Accounts Receivable credited, Sales Returns
    /// and Output GST debited, and the rounding on whichever side balances it.
    /// </summary>
    private static PostLedgerRequest BuildPosting(CreditNote creditNote, Guid customerId, string baseCurrency)
    {
        var postRequest = new PostLedgerRequest
        {
            CustomerId = customerId,
            OrgId = creditNote.OrgId,
            TransactionTypeCode = creditNote.TransactionTypeCode,
            TransactionId = creditNote.CreditNoteId,
            // The number on the document's face, so the ledger can report it
            // without reaching into this service's schema to look it up.
            DocumentNo = creditNote.DocumentNo,
            LedgerDate = creditNote.DocumentDate,
            CurrencyCode = creditNote.CurrencyCode == baseCurrency ? null : creditNote.CurrencyCode,
            ExchangeRate = creditNote.CurrencyCode == baseCurrency ? null : creditNote.ExchangeRate,
            ContactId = creditNote.ContactId,
            SourceDocumentId = creditNote.CreditNoteId,
            Legs = [],
        };

        postRequest.Legs.Add(new LedgerLegRequest
        {
            LedgerTypeId = ControlLedgerType,
            LedgerSourceId = TransactionLedgerSource,
            TransactionDetailId = 0,
            AccountSystemName = AccountsReceivableAccount,
            SubAccountReferenceType = ContactReference,
            SubAccountReferenceId = creditNote.ContactId,
            SubAccountPurpose = 0, // the trade balance
            CreditAmount = creditNote.TotalAmount,
            TransactionDesc = "Credited to customer",
        });

        decimal returned = creditNote.SubTotal - creditNote.DiscountAmount;
        if (returned > 0)
        {
            postRequest.Legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = ItemLedgerType,
                LedgerSourceId = TransactionLedgerSource,
                TransactionDetailId = 0,
                AccountSystemName = SalesReturnsAccount,
                DebitAmount = returned,
                TransactionDesc = "Sales returned or reduced",
            });
        }

        // Grouped the way the invoice groups its own, so each reverses the
        // sub-account the invoice credited: the tax group and the component.
        var taxGroups = creditNote.Lines.SelectMany(l => l.Taxes)
            .GroupBy(t => new { t.SubAccountId, t.TaxComponent })
            .Select(g => new { g.Key.SubAccountId, g.Key.TaxComponent, Amount = g.Sum(t => t.Amount) });

        foreach (var tax in taxGroups.Where(t => t.Amount > 0))
        {
            postRequest.Legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = TaxLedgerType,
                LedgerSourceId = TransactionLedgerSource,
                TransactionDetailId = 0,
                SubAccountReferenceType = TaxReference,
                SubAccountReferenceId = tax.SubAccountId,

                // Shared.Kernel's TaxComponent is 0-based; Accounting's is 1-based
                // with None at 0. Cgst → 1, Sgst → 2, Igst → 3, Cess → 4.
                SubAccountTaxComponent = (int)tax.TaxComponent + 1,
                AccountSystemName = OutputGstAccount,
                DebitAmount = tax.Amount,
                TransactionDesc = $"Output {tax.TaxComponent} reversed",
            });
        }

        // Receivable was credited with the rounded total, so rounding up is
        // debited here and rounding down credited — the mirror of the invoice.
        if (creditNote.RoundOffAmount != 0)
        {
            postRequest.Legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = RoundOffLedgerType,
                LedgerSourceId = TransactionLedgerSource,
                TransactionDetailId = 0,
                AccountSystemName = RoundOffAccount,
                DebitAmount = creditNote.RoundOffAmount > 0 ? creditNote.RoundOffAmount : 0m,
                CreditAmount = creditNote.RoundOffAmount < 0 ? -creditNote.RoundOffAmount : 0m,
                TransactionDesc = "Rounding",
            });
        }

        return postRequest;
    }

    /// <summary>Releases the note's claim on its invoice. Safe when there is none.</summary>
    private Task ReleaseClaimAsync(CreditNote creditNote, Guid customerId, CancellationToken ct) =>
        _ledgerClient.RemoveAllocationsAsync(new RemoveAllocationsRequest
        {
            CustomerId = customerId,
            OrgId = creditNote.OrgId,
            SourceTransactionTypeCode = creditNote.TransactionTypeCode,
            SourceTransactionId = creditNote.CreditNoteId,
        }, ct);

    private static long? TaxGroupOf(SaveCreditNoteLineRequest line) =>
        line.TaxGroupId ?? (line.TaxGroupIds.Count > 0 ? line.TaxGroupIds[0] : null);
}
