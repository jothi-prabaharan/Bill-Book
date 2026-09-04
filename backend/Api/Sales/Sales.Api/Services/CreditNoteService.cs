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
        ILedgerClient ledgerClient)
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
            Status = x.Status,
            TotalAmount = x.TotalAmount
        }).ToList();
    }

    public async Task<CreditNoteView?> GetAsync(long id, CancellationToken ct)
    {
        var creditNote = await _db.CreditNotes
            .Include(x => x.Lines)
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
            Status = creditNote.Status,
            ReasonCode = creditNote.ReasonCode,
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
                Description = l.Description,
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

    public async Task<long> SaveAsync(long? creditNoteId, SaveCreditNoteRequest request, CancellationToken ct)
    {
        var (customerId, orgId) = _tenant.Require();

        CreditNote creditNote;
        if (creditNoteId.HasValue)
        {
            creditNote = await _db.CreditNotes
                .Include(x => x.Lines)
                    .ThenInclude(l => l.Taxes)
                .FirstOrDefaultAsync(x => x.CreditNoteId == creditNoteId.Value, ct)
                ?? throw new InvalidOperationException("CreditNote not found.");
                
            if (creditNote.Status != DocumentStatus.Draft)
                throw new InvalidOperationException("Only draft CreditNotes can be edited.");

            _db.CreditNoteDetailTaxes.RemoveRange(creditNote.Lines.SelectMany(l => l.Taxes));
            _db.CreditNoteDetails.RemoveRange(creditNote.Lines);
        }
        else
        {
            var alloc = await _numbering.NextAsync("CRN", request.DocumentDate, ct);
            creditNote = new CreditNote
            {
                OrgId = orgId,
                DocumentNo = alloc.Code,
                TransactionTypeCode = "CRN",
                Status = DocumentStatus.Draft
            };
            _db.CreditNotes.Add(creditNote);
        }

        BranchSettings? settings = await _branchSettings.GetSettingsAsync(ct);
        if (settings is null)
        {
            throw new InvalidOperationException("Branch settings could not be read.");
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
            throw new InvalidOperationException(pos.Detail);
        }

        TaxContext taxContext = new(pos.IsInterState, settings.DiscountBeforeTax);

        creditNote.InvoiceId = request.InvoiceId;
        creditNote.ReasonCode = request.ReasonCode;
        creditNote.ContactId = request.ContactId;
        creditNote.ContactGstin = request.ContactGstin;
        creditNote.PlaceOfSupplyStateId = 0;
        creditNote.IsInterState = pos.IsInterState;
        creditNote.DocumentDate = request.DocumentDate;
        creditNote.Notes = request.Notes;
        creditNote.CurrencyCode = request.CurrencyCode ?? "USD";
        creditNote.ExchangeRate = request.ExchangeRate;
        creditNote.BillingAddress = request.BillingAddress;
        creditNote.ShippingAddress = request.ShippingAddress;

        var taxLines = new List<TaxLineResult>(request.Lines.Count);

        for (int i = 0; i < request.Lines.Count; i++)
        {
            SaveCreditNoteLineRequest reqLine = request.Lines[i];
            int lineNumber = i + 1;

            long? taxGroupId = reqLine.TaxGroupIds.Count > 0 ? reqLine.TaxGroupIds[0] : null;

            TaxRate? rate = null;
            if (taxGroupId.HasValue)
            {
                rate = await _rates.GetRateAsync(taxGroupId.Value, request.DocumentDate, ct);
                if (rate is null)
                {
                    throw new InvalidOperationException(
                        $"Tax rate for group {taxGroupId.Value} could not be read for date {request.DocumentDate}.");
                }
            }

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
                LineNumber = lineNumber,
                InvoiceDetailId = reqLine.InvoiceDetailId,
                ItemId = reqLine.ItemId,
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
        return creditNote.CreditNoteId;
    }

    public async Task PostAsync(long creditNoteId, CancellationToken ct)
    {
        var (customerId, orgId) = _tenant.Require();

        var creditNote = await _db.CreditNotes
            .Include(x => x.Lines)
                .ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(x => x.CreditNoteId == creditNoteId, ct)
            ?? throw new InvalidOperationException("CreditNote not found.");

        if (creditNote.Status != DocumentStatus.Draft)
            throw new InvalidOperationException("CreditNote is not in draft status.");

        if (creditNote.Lines.Count == 0)
            throw new InvalidOperationException("CreditNote has no lines.");

        // The guard comes first: a refusal must leave the note draft with no
        // stock moved and nothing posted. Allocating after the ledger post
        // would leave a posted note the invoices it names do not recognise.
        string invoiceTypeCode = await _db.Invoices
            .Where(x => x.InvoiceId == creditNote.InvoiceId)
            .Select(x => x.TransactionTypeCode)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("The invoice this credit note corrects no longer exists.");

        // Accounting's own guard re-checks the claim against the invoice's
        // CONTROL net and what has already been allocated to it. The refusal
        // message is the note's rejection reason.
        var allocation = await _ledgerClient.AllocateAsync(new AllocateTransactionRequest
        {
            CustomerId = customerId,
            OrgId = creditNote.OrgId,
            SourceTransactionTypeCode = creditNote.TransactionTypeCode,
            SourceTransactionId = creditNote.CreditNoteId,
            TargetTransactionTypeCode = invoiceTypeCode,
            TargetTransactionId = creditNote.InvoiceId,
            Amount = creditNote.TotalAmount,
        }, ct);

        if (!allocation.Allocated)
            throw new InvalidOperationException($"The credit note was refused: {allocation.Detail}");

        var invoiceDetailIds = creditNote.Lines.Select(l => l.InvoiceDetailId).ToList();
        var invoiceDetails = await _db.InvoiceDetails
            .Where(x => invoiceDetailIds.Contains(x.InvoiceDetailId))
            .ToDictionaryAsync(x => x.InvoiceDetailId, ct);

        decimal totalCogs = 0;
        // 1. Receive Stock
        
        if (creditNote.ReasonCode == CreditNoteReason.SalesReturn)
        {
            var receiveRequest = new ReceiveStockRequest
            {
                OrgId = creditNote.OrgId,
                CustomerId = customerId,
                MovementDate = creditNote.DocumentDate,
                SourceType = creditNote.TransactionTypeCode,
                SourceId = creditNote.CreditNoteId,
                Lines = creditNote.Lines.Select(l => new ReceiveStockLine
                {
                    SourceLineId = l.CreditNoteDetailId,
                    ItemId = l.ItemId ?? 0,
                    Quantity = l.Quantity,
                    WarehouseId = null,
                    UnitCost = l.UnitPrice, // Approximate if the backend derives actual
                    ReturnsStockMovementId = invoiceDetails.TryGetValue(l.InvoiceDetailId, out var invLine) ? invLine.StockMovementId : null
                }).ToList()
            };

            var response = await _inventoryClient.ReceiveAsync(receiveRequest, ct);
            if (!response.Success)
                throw new InvalidOperationException("Failed to return stock to inventory.");
            
            totalCogs = response.TotalValue;
        }

        // 2. Post Ledger (Reverse of Invoice)
        var baseCurrency = await _baseCurrency.GetBaseCurrencyAsync(ct);
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
            Legs = new List<LedgerLegRequest>()
        };

        decimal totalAmount = creditNote.TotalAmount;
        decimal totalRevenue = creditNote.SubTotal - creditNote.DiscountAmount;

        // Credit Accounts Receivable (CONTROL leg, type 3) - decrease AR
        postRequest.Legs.Add(new LedgerLegRequest
        {
            LedgerTypeId = 3, // CONTROL
            LedgerSourceId = 3, // Transaction posting
            TransactionDetailId = 0,
            AccountSystemName = "Accounts Receivable",
            SubAccountReferenceType = 1, // Contact
            SubAccountReferenceId = creditNote.ContactId,
            SubAccountPurpose = 0, // Primary (trade balance)
            CreditAmount = totalAmount
        });

        // Debit Sales Returns (ITEM leg, type 1) - contra revenue
        postRequest.Legs.Add(new LedgerLegRequest
        {
            LedgerTypeId = 1, // ITEM
            LedgerSourceId = 3,
            TransactionDetailId = 0,
            AccountSystemName = "Sales Returns",
            DebitAmount = totalRevenue
        });

        // Debit Tax Payable (TAX legs, type 2) - decrease tax liability, split by component
        var taxes = creditNote.Lines.SelectMany(l => l.Taxes)
            .GroupBy(t => t.SubAccountId)
            .Select(g => new { TaxRateId = g.Key, Amount = g.Sum(t => t.Amount) });
            
        foreach (var tax in taxes)
        {
            // Shared.Kernel.Documents.TaxComponent is 0-based: Cgst=0, Sgst=1, Igst=2
            // Accounting.Entity.Enums.TaxComponent is 1-based: None=0, Cgst=1, Sgst=2, Igst=3
            var cgst = creditNote.Lines.SelectMany(l => l.Taxes)
                .Where(t => t.SubAccountId == tax.TaxRateId && t.TaxComponent == TaxComponent.Cgst)
                .Sum(t => t.Amount);
            var sgst = creditNote.Lines.SelectMany(l => l.Taxes)
                .Where(t => t.SubAccountId == tax.TaxRateId && t.TaxComponent == TaxComponent.Sgst)
                .Sum(t => t.Amount);
            var igst = creditNote.Lines.SelectMany(l => l.Taxes)
                .Where(t => t.SubAccountId == tax.TaxRateId && t.TaxComponent == TaxComponent.Igst)
                .Sum(t => t.Amount);

            if (cgst > 0)
            {
                postRequest.Legs.Add(new LedgerLegRequest
                {
                    LedgerTypeId = 2, // TAX
                    LedgerSourceId = 3,
                    TransactionDetailId = 0,
                    SubAccountReferenceType = 3, // Tax
                    SubAccountReferenceId = tax.TaxRateId,
                    SubAccountTaxComponent = 1, // CGST
                    AccountSystemName = "Tax Payable",
                    DebitAmount = cgst
                });
            }

            if (sgst > 0)
            {
                postRequest.Legs.Add(new LedgerLegRequest
                {
                    LedgerTypeId = 2, // TAX
                    LedgerSourceId = 3,
                    TransactionDetailId = 0,
                    SubAccountReferenceType = 3, // Tax
                    SubAccountReferenceId = tax.TaxRateId,
                    SubAccountTaxComponent = 2, // SGST
                    AccountSystemName = "Tax Payable",
                    DebitAmount = sgst
                });
            }

            if (igst > 0)
            {
                postRequest.Legs.Add(new LedgerLegRequest
                {
                    LedgerTypeId = 2, // TAX
                    LedgerSourceId = 3,
                    TransactionDetailId = 0,
                    SubAccountReferenceType = 3, // Tax
                    SubAccountReferenceId = tax.TaxRateId,
                    SubAccountTaxComponent = 3, // IGST
                    AccountSystemName = "Tax Payable",
                    DebitAmount = igst
                });
            }
        }

        if (totalCogs > 0)
        {
            
            // Debit Inventory (CONTROL leg, type 3) - stock returned
            postRequest.Legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = 3, // CONTROL (stock movement)
                LedgerSourceId = 3,
                TransactionDetailId = 0,
                AccountSystemName = "Inventory",
                DebitAmount = totalCogs
            });

            // Credit COGS (COGS leg, type 4) - reverse the cost
            postRequest.Legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = 4, // COGS
                LedgerSourceId = 3,
                TransactionDetailId = 0,
                AccountSystemName = "Cost of Goods Sold",
                CreditAmount = totalCogs
            });

        }


        foreach (var l in creditNote.Lines)
        {
            if (invoiceDetails.TryGetValue(l.InvoiceDetailId, out var invLine))
            {
                invLine.ReturnedQuantity += l.Quantity;
            }
        }

        var result = await _ledgerClient.PostAsync(postRequest, ct);
        if (!result.Posted)
            throw new InvalidOperationException($"Ledger post failed: {result.Detail}");

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
                CgstAmount = -(l.Taxes.FirstOrDefault(t => t.TaxComponent.ToString() == "Cgst")?.Amount ?? 0),
                SgstAmount = -(l.Taxes.FirstOrDefault(t => t.TaxComponent.ToString() == "Sgst")?.Amount ?? 0),
                IgstAmount = -(l.Taxes.FirstOrDefault(t => t.TaxComponent.ToString() == "Igst")?.Amount ?? 0),
                CessAmount = -(l.Taxes.FirstOrDefault(t => t.TaxComponent.ToString() == "Cess")?.Amount ?? 0),
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

        await _db.SaveChangesAsync(ct);
    }

    public async Task VoidAsync(long creditNoteId, CancellationToken ct)
    {
        var (customerId, orgId) = _tenant.Require();

        var creditNote = await _db.CreditNotes
            .FirstOrDefaultAsync(x => x.CreditNoteId == creditNoteId, ct)
            ?? throw new InvalidOperationException("CreditNote not found.");

        if (creditNote.Status == DocumentStatus.Void)
            return;

        creditNote.Status = DocumentStatus.Void;
        creditNote.VoidedAt = _clock.GetUtcNow();

        var registers = await _db.SalesRegister
            .Where(r => r.SourceId == creditNoteId && r.TransactionTypeCode == creditNote.TransactionTypeCode)
            .ToListAsync(ct);
        _db.SalesRegister.RemoveRange(registers);

        // A voided note takes its claims with it, or the invoices it named stay
        // partially allocated to a document that no longer exists. A failure
        // here aborts the void: the note must not vanish while its claims remain.
        await _ledgerClient.RemoveAllocationsAsync(new RemoveAllocationsRequest
        {
            CustomerId = customerId,
            OrgId = orgId,
            SourceTransactionTypeCode = creditNote.TransactionTypeCode,
            SourceTransactionId = creditNoteId,
        }, ct);

        await _db.SaveChangesAsync(ct);
    }
}



