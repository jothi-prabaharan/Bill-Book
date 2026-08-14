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

public sealed class InvoiceService
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

    public async Task<IReadOnlyList<InvoiceListItem>> ListAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var query = _db.Invoices.AsNoTracking();

        if (from.HasValue) query = query.Where(x => x.DocumentDate >= from.Value);
        if (to.HasValue) query = query.Where(x => x.DocumentDate <= to.Value);

        var list = await query
            .OrderByDescending(x => x.DocumentDate)
            .ThenByDescending(x => x.InvoiceId)
            .Select(x => new
            {
                x.InvoiceId,
                x.SalesOrderId,
                x.DocumentDate,
                x.DocumentNo,
                x.ContactId,
                x.Status,
                x.DueDate,
                x.TotalAmount
            })
            .ToListAsync(ct);

        var contactIds = list.Select(x => x.ContactId).Distinct().ToList();
        var contacts = await _contactNames.ResolveAsync(contactIds, ct);

        return list.Select(x => new InvoiceListItem
        {
            InvoiceId = x.InvoiceId,
            SalesOrderId = x.SalesOrderId,
            DocumentDate = x.DocumentDate,
            DocumentNo = x.DocumentNo,
            ContactId = x.ContactId,
            ContactName = contacts.TryGetValue(x.ContactId, out var c) ? c.Name : "Unknown",
            Status = x.Status,
            DueDate = x.DueDate,
            TotalAmount = x.TotalAmount
        }).ToList();
    }

    public async Task<InvoiceView?> GetAsync(long id, CancellationToken ct)
    {
        var invoice = await _db.Invoices
            .Include(x => x.Lines)
                .ThenInclude(l => l.Taxes)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.InvoiceId == id, ct);

        if (invoice is null) return null;

        var contacts = await _contactNames.ResolveAsync([invoice.ContactId], ct);
        var itemIds = invoice.Lines.Where(l => l.ItemId.HasValue).Select(l => l.ItemId!.Value).Distinct().ToList();
        var items = await _itemNames.ResolveAsync(itemIds, ct);

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
            Status = invoice.Status,
            CurrencyCode = invoice.CurrencyCode,
            ExchangeRate = invoice.ExchangeRate,
            ContactId = invoice.ContactId,
            ContactName = contacts.TryGetValue(invoice.ContactId, out var c) ? c.Name : "Unknown",
            BillingAddress = invoice.BillingAddress,
            ShippingAddress = invoice.ShippingAddress
        };

        foreach (var line in invoice.Lines)
        {
            var lineView = new InvoiceLineView
            {
                InvoiceLineId = line.InvoiceDetailId,
                ItemId = line.ItemId ?? 0,
                ItemName = (line.ItemId.HasValue && items.TryGetValue(line.ItemId.Value, out var item)) ? item.Name : "Unknown",
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                DiscountPercent = line.DiscountPercent ?? 0,
                DiscountAmount = line.DiscountAmount,
                LineTotal = line.LineTotal,
                TaxAmount = line.TaxAmount,
                NetTotal = line.LineTotal + line.TaxAmount
            };

            foreach (var tax in line.Taxes)
            {
                lineView.Taxes.Add(new InvoiceLineTaxView
                {
                    SubAccountId = tax.SubAccountId,
                    Amount = tax.Amount
                });
            }

            view.Lines.Add(lineView);
        }

        return view;
    }

    public async Task<long> SaveAsync(SaveInvoiceRequest request, long? invoiceId, CancellationToken ct)
    {
        var (customerId, orgId) = _tenant.Require();

        Invoice invoice;
        if (invoiceId.HasValue)
        {
            invoice = await _db.Invoices
                .Include(x => x.Lines)
                    .ThenInclude(l => l.Taxes)
                .FirstOrDefaultAsync(x => x.InvoiceId == invoiceId.Value, ct)
                ?? throw new InvalidOperationException("Invoice not found.");
                
            if (invoice.Status != DocumentStatus.Draft)
                throw new InvalidOperationException("Only draft invoices can be edited.");

            _db.InvoiceDetailTaxes.RemoveRange(invoice.Lines.SelectMany(l => l.Taxes));
            _db.InvoiceDetails.RemoveRange(invoice.Lines);
        }
        else
        {
            var alloc = await _numbering.NextAsync("INV", request.DocumentDate, ct);
            invoice = new Invoice
            {
                OrgId = orgId,
                DocumentNo = alloc.Code,
                TransactionTypeCode = request.TillId.HasValue ? "POS" : "INV",
                Status = DocumentStatus.Draft
            };
            _db.Invoices.Add(invoice);
        }

        invoice.QuoteId = request.QuoteId;
        invoice.SalesOrderId = request.SalesOrderId;
        invoice.DeliveryChallanId = request.DeliveryChallanId;
        invoice.PaymentTermId = request.PaymentTermId;
        invoice.DueDate = request.DueDate;
        invoice.TillId = request.TillId;
        invoice.CashierUserId = request.CashierUserId;
        invoice.PaymentMode = request.PaymentMode;
        invoice.TenderedAmount = request.TenderedAmount;
        invoice.ChangeAmount = request.ChangeAmount;

        invoice.ContactId = request.ContactId;
        invoice.DocumentDate = request.DocumentDate;
        invoice.Notes = request.Notes;
        invoice.CurrencyCode = request.CurrencyCode ?? "USD";
        invoice.ExchangeRate = request.ExchangeRate;
        invoice.BillingAddress = request.BillingAddress;
        invoice.ShippingAddress = request.ShippingAddress;

        foreach (var reqLine in request.Lines)
        {
            var line = new InvoiceDetail
            {
                OrgId = invoice.OrgId,
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
                line.Taxes.Add(new InvoiceDetailTax
                {
                    OrgId = invoice.OrgId,
                    SubAccountId = taxGroupId,
                    Amount = taxAmount
                });
            }

            line.TaxAmount = line.Taxes.Sum(t => t.Amount);
            invoice.Lines.Add(line);
        }

        invoice.SubTotal = invoice.Lines.Sum(l => l.LineTotal + l.DiscountAmount);
        invoice.DiscountAmount = invoice.Lines.Sum(l => l.DiscountAmount);
        invoice.TaxableAmount = invoice.Lines.Sum(l => l.LineTotal);
        invoice.CgstAmount = invoice.Lines.Sum(l => l.TaxAmount) / 2;
        invoice.SgstAmount = invoice.Lines.Sum(l => l.TaxAmount) / 2;
        invoice.TotalAmount = invoice.Lines.Sum(l => l.LineTotal + l.TaxAmount);
        invoice.TotalAmountBase = invoice.TotalAmount * invoice.ExchangeRate;

        await _db.SaveChangesAsync(ct);
        return invoice.InvoiceId;
    }

    public async Task PostAsync(long invoiceId, CancellationToken ct)
    {
        var (customerId, orgId) = _tenant.Require();
        

        var invoice = await _db.Invoices
            .Include(x => x.Lines)
                .ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(x => x.InvoiceId == invoiceId, ct)
            ?? throw new InvalidOperationException("Invoice not found.");

        if (invoice.Status != DocumentStatus.Draft)
            throw new InvalidOperationException("Invoice is not in draft status.");

        if (invoice.Lines.Count == 0)
            throw new InvalidOperationException("Invoice has no lines.");

        // 1. Issue Stock
        var issueRequest = new IssueStockRequest
        {
            OrgId = invoice.OrgId,
            CustomerId = customerId,
            MovementDate = invoice.DocumentDate,
            SourceType = invoice.TransactionTypeCode,
            SourceId = invoice.InvoiceId,
            Lines = invoice.Lines.Select(l => new IssueStockLine
            {
                SourceLineId = l.InvoiceDetailId,
                ItemId = l.ItemId ?? 0,
                Quantity = l.Quantity,
                WarehouseId = null,
                ReleaseReservation = invoice.SalesOrderId.HasValue
            }).ToList()
        };

        var issueResult = await _inventoryClient.IssueAsync(issueRequest, ct);
        if (!issueResult.Success)
            throw new InvalidOperationException("Stock issue failed.");

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
            Legs = new List<LedgerLegRequest>()
        };

        decimal totalAmount = invoice.TotalAmount;
        decimal totalRevenue = invoice.SubTotal - invoice.DiscountAmount;
        decimal totalCogs = issueResult.TotalValue;

        postRequest.Legs.Add(new LedgerLegRequest
        {
            LedgerTypeId = invoice.TillId.HasValue ? 1 : 1, // AR/Cash
            LedgerSourceId = 3,
            TransactionDetailId = 0,
            AccountSystemName = invoice.TillId.HasValue ? "Cash" : "Accounts Receivable",
            Amount = totalAmount
        });

        postRequest.Legs.Add(new LedgerLegRequest
        {
            LedgerTypeId = 4, // Revenue
            LedgerSourceId = 3,
            TransactionDetailId = 0,
            AccountSystemName = "Sales",
            Amount = totalRevenue
        });

        var taxes = invoice.Lines.SelectMany(l => l.Taxes)
            .GroupBy(t => t.SubAccountId)
            .Select(g => new { TaxRateId = g.Key, Amount = g.Sum(t => t.Amount) });
            
        foreach (var tax in taxes)
        {
            postRequest.Legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = 2, // Liability
                LedgerSourceId = 3,
                TransactionDetailId = 0,
                SubAccountReferenceId = tax.TaxRateId,
                AccountSystemName = "Tax Payable",
                Amount = tax.Amount
            });
        }

        if (totalCogs > 0)
        {
            postRequest.Legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = 5, // Expense
                LedgerSourceId = 3,
                TransactionDetailId = 0,
                AccountSystemName = "Cost of Goods Sold",
                Amount = totalCogs
            });

            postRequest.Legs.Add(new LedgerLegRequest
            {
                LedgerTypeId = 1, // Asset
                LedgerSourceId = 3,
                TransactionDetailId = 0,
                AccountSystemName = "Inventory",
                Amount = totalCogs
            });
        }

        bool posted = await _ledgerClient.PostAsync(postRequest, ct);
        if (!posted)
            throw new InvalidOperationException("Ledger post failed.");

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
                TaxableAmount = l.LineTotal,
                CgstAmount = l.Taxes.FirstOrDefault(t => t.TaxComponent.ToString() == "Cgst")?.Amount ?? 0,
                SgstAmount = l.Taxes.FirstOrDefault(t => t.TaxComponent.ToString() == "Sgst")?.Amount ?? 0,
                IgstAmount = l.Taxes.FirstOrDefault(t => t.TaxComponent.ToString() == "Igst")?.Amount ?? 0,
                CessAmount = l.Taxes.FirstOrDefault(t => t.TaxComponent.ToString() == "Cess")?.Amount ?? 0,
                TotalAmount = l.LineTotal + l.TaxAmount,
                CurrencyCode = invoice.CurrencyCode,
                ExchangeRate = invoice.ExchangeRate,
                TaxableAmountBase = l.LineTotal * invoice.ExchangeRate
            });
        }

        invoice.Status = DocumentStatus.Posted;
        invoice.PostedAt = _clock.GetUtcNow();
        await _db.SaveChangesAsync(ct);
    }

    public async Task VoidAsync(long invoiceId, CancellationToken ct)
    {
        var invoice = await _db.Invoices
            .FirstOrDefaultAsync(x => x.InvoiceId == invoiceId, ct)
            ?? throw new InvalidOperationException("Invoice not found.");

        if (invoice.Status == DocumentStatus.Void)
            return;

        invoice.Status = DocumentStatus.Void;
        invoice.VoidedAt = _clock.GetUtcNow();

        var registers = await _db.SalesRegister
            .Where(r => r.SourceId == invoiceId && r.TransactionTypeCode == invoice.TransactionTypeCode)
            .ToListAsync(ct);
        _db.SalesRegister.RemoveRange(registers);

        await _db.SaveChangesAsync(ct);
    }
}
