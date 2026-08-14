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

        creditNote.InvoiceId = request.InvoiceId;
        creditNote.ReasonCode = request.ReasonCode;
        creditNote.ContactId = request.ContactId;
        creditNote.DocumentDate = request.DocumentDate;
        creditNote.Notes = request.Notes;
        creditNote.CurrencyCode = request.CurrencyCode ?? "USD";
        creditNote.ExchangeRate = request.ExchangeRate;
        creditNote.BillingAddress = request.BillingAddress;
        creditNote.ShippingAddress = request.ShippingAddress;

        foreach (var reqLine in request.Lines)
        {
            var line = new CreditNoteDetail
            {
                OrgId = creditNote.OrgId,
                InvoiceDetailId = reqLine.InvoiceDetailId,
                ItemId = reqLine.ItemId,
                Quantity = reqLine.Quantity,
                UnitPrice = reqLine.UnitPrice,
                DiscountPercent = reqLine.DiscountPercent,
                DiscountAmount = Math.Round(reqLine.Quantity * reqLine.UnitPrice * reqLine.DiscountPercent / 100, 2),
            };
            
            line.LineTotal = (reqLine.Quantity * reqLine.UnitPrice) - line.DiscountAmount;

            foreach (var taxGroupId in reqLine.TaxGroupIds)
            {
                var rate = await _rates.GetRateAsync(taxGroupId, request.DocumentDate, ct);
                var taxAmount = Math.Round(line.LineTotal * (rate?.TotalRate ?? 0) / 100, 2);
                line.Taxes.Add(new CreditNoteDetailTax
                {
                    OrgId = creditNote.OrgId,
                    SubAccountId = taxGroupId,
                    Amount = taxAmount
                });
            }

            line.TaxAmount = line.Taxes.Sum(t => t.Amount);
            creditNote.Lines.Add(line);
        }

        creditNote.SubTotal = creditNote.Lines.Sum(l => l.LineTotal + l.DiscountAmount);
        creditNote.DiscountAmount = creditNote.Lines.Sum(l => l.DiscountAmount);
        creditNote.TaxableAmount = creditNote.Lines.Sum(l => l.LineTotal);
        creditNote.CgstAmount = creditNote.Lines.Sum(l => l.TaxAmount) / 2;
        creditNote.SgstAmount = creditNote.Lines.Sum(l => l.TaxAmount) / 2;
        creditNote.TotalAmount = creditNote.Lines.Sum(l => l.LineTotal + l.TaxAmount);
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
                ReturnsStockMovementId = creditNote.InvoiceId, // Return from original invoice movement
                Lines = creditNote.Lines.Select(l => new ReceiveStockLine
                {
                    SourceLineId = l.CreditNoteDetailId,
                    ItemId = l.ItemId ?? 0,
                    Quantity = l.Quantity,
                    WarehouseId = null,
                    UnitCost = l.UnitPrice // Approximate if the backend derives actual
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
            LedgerDate = creditNote.DocumentDate,
            CurrencyCode = creditNote.CurrencyCode == baseCurrency ? null : creditNote.CurrencyCode,
            ExchangeRate = creditNote.CurrencyCode == baseCurrency ? null : creditNote.ExchangeRate,
            ContactId = creditNote.ContactId,
            SourceDocumentId = creditNote.CreditNoteId,
            Legs = new List<LedgerLegRequest>()
        };

        decimal totalAmount = creditNote.TotalAmount;
        decimal totalRevenue = creditNote.SubTotal - creditNote.DiscountAmount;

        // Decrease Accounts Receivable (Credit)
        postRequest.Legs.Add(new LedgerLegRequest
        {
            LedgerTypeId = 1, // Asset
            LedgerSourceId = 3, // Invoice related
            TransactionDetailId = 0,
            AccountSystemName = "Accounts Receivable",
            Amount = totalAmount // Ledger might interpret based on direction, let's keep it consistent
        }); // TODO: Needs direction or negative amount depending on system design. Usually Sales Returns debit Sales Returns and credit AR. Let's make it positive and see if Ledger expects natural balances.
            // If AR is naturally DR, we need to Credit it. If we use positive amounts for increasing balance, we need a way to decrease it.
            // Actually, in InvoiceService, there is no Direction flag. 

        // Let's assume negative for contra/decreases.
        postRequest.Legs.Add(new LedgerLegRequest
        {
            LedgerTypeId = 1, // Asset -> AR (Credit)
            LedgerSourceId = 3,
            TransactionDetailId = 0,
            AccountSystemName = "Accounts Receivable",
            Amount = -totalAmount
        });

        postRequest.Legs.Add(new LedgerLegRequest
        {
            LedgerTypeId = 4, // Revenue -> Sales Returns (Contra Revenue -> Debit)
            LedgerSourceId = 3,
            TransactionDetailId = 0,
            AccountSystemName = "Sales Returns",
            Amount = -totalRevenue // Contra revenue decreases revenue balance
        });

        // Taxes
        var taxes = creditNote.Lines.SelectMany(l => l.Taxes)
            .GroupBy(t => t.SubAccountId)
            .Select(g => new { TaxRateId = g.Key, Amount = g.Sum(t => t.Amount) });
            
        foreach (var tax in taxes)
        {
            postRequest.Legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = 2, // Liability -> Tax Payable (Debit)
                LedgerSourceId = 3,
                TransactionDetailId = 0,
                SubAccountReferenceId = tax.TaxRateId,
                AccountSystemName = "Tax Payable",
                Amount = -tax.Amount // Decrease liability
            });
        }

                if (totalCogs > 0)
        {
            postRequest.Legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = 1, // Asset -> Inventory (Debit)
                LedgerSourceId = 3,
                TransactionDetailId = 0,
                AccountSystemName = "Inventory",
                Amount = totalCogs
            });
            postRequest.Legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = 5, // Expense -> COGS (Credit)
                LedgerSourceId = 3,
                TransactionDetailId = 0,
                AccountSystemName = "Cost of Goods Sold",
                Amount = -totalCogs
            });
        }

        await _ledgerClient.PostAsync(postRequest, ct);

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

        await _db.SaveChangesAsync(ct);
    }
}



