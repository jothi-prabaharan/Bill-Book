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

public sealed class DeliveryChallanService
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

    public DeliveryChallanService(
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

    public async Task<IReadOnlyList<DeliveryChallanListItem>> ListAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var query = _db.DeliveryChallans.AsNoTracking();

        if (from.HasValue) query = query.Where(x => x.DocumentDate >= from.Value);
        if (to.HasValue) query = query.Where(x => x.DocumentDate <= to.Value);

        var list = await query
            .OrderByDescending(x => x.DocumentDate)
            .ThenByDescending(x => x.DeliveryChallanId)
            .Select(x => new
            {
                x.DeliveryChallanId,
                x.SalesOrderId,
                x.DocumentDate,
                x.DocumentNo,
                x.ContactId,
                x.Status,
                x.DispatchDate,
                x.TotalAmount
            })
            .ToListAsync(ct);

        var contactIds = list.Select(x => x.ContactId).Distinct().ToList();
        var contacts = await _contactNames.ResolveAsync(contactIds, ct);

        return list.Select(x => new DeliveryChallanListItem
        {
            DeliveryChallanId = x.DeliveryChallanId,
            SalesOrderId = x.SalesOrderId,
            DocumentDate = x.DocumentDate,
            DocumentNo = x.DocumentNo,
            ContactId = x.ContactId,
            ContactName = contacts.TryGetValue(x.ContactId, out var c) ? c.Name : "Unknown",
            Status = x.Status,
            DispatchDate = x.DispatchDate,
            TotalAmount = x.TotalAmount
        }).ToList();
    }

    public async Task<DeliveryChallanView?> GetAsync(long id, CancellationToken ct)
    {
        var deliveryChallan = await _db.DeliveryChallans
            .Include(x => x.Lines)
                .ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(x => x.DeliveryChallanId == id, ct);

        if (deliveryChallan == null) return null;

        var contactName = (await _contactNames.ResolveAsync([deliveryChallan.ContactId], ct))
            .TryGetValue(deliveryChallan.ContactId, out var contact) ? contact.Name : "Unknown";

        var itemIds = deliveryChallan.Lines.Select(l => l.ItemId).Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
        var itemNames = await _itemNames.ResolveAsync(itemIds, ct);

        return new DeliveryChallanView
        {
            DeliveryChallanId = deliveryChallan.DeliveryChallanId,
            SalesOrderId = deliveryChallan.SalesOrderId,
            DocumentDate = deliveryChallan.DocumentDate,
            DocumentNo = deliveryChallan.DocumentNo,
            ContactId = deliveryChallan.ContactId,
            ContactName = contactName,
            Status = deliveryChallan.Status,
            ChallanType = deliveryChallan.ChallanType,
            VehicleNo = deliveryChallan.VehicleNo,
            TransporterName = deliveryChallan.TransporterName,
            EwayBillNo = deliveryChallan.EwayBillNo,
            EwayBillDate = deliveryChallan.EwayBillDate,
            DispatchDate = deliveryChallan.DispatchDate,
            CurrencyCode = deliveryChallan.CurrencyCode,
            ExchangeRate = deliveryChallan.ExchangeRate,
            Notes = deliveryChallan.Notes,
            BillingAddress = deliveryChallan.BillingAddress,
            ShippingAddress = deliveryChallan.ShippingAddress,
            PlaceOfSupplyStateId = deliveryChallan.PlaceOfSupplyStateId,
            IsInterState = deliveryChallan.IsInterState,
            SubTotal = deliveryChallan.SubTotal,
            DiscountAmount = deliveryChallan.DiscountAmount,
            TaxableAmount = deliveryChallan.TaxableAmount,
            CgstAmount = deliveryChallan.CgstAmount,
            SgstAmount = deliveryChallan.SgstAmount,
            IgstAmount = deliveryChallan.IgstAmount,
            CessAmount = deliveryChallan.CessAmount,
            RoundOffAmount = deliveryChallan.RoundOffAmount,
            TotalAmount = deliveryChallan.TotalAmount,
            TotalAmountBase = deliveryChallan.TotalAmountBase,
            Lines = deliveryChallan.Lines.Select(l => new DeliveryChallanLineView
            {
                DeliveryChallanDetailId = l.DeliveryChallanDetailId,
                ItemId = l.ItemId,
                ItemLabel = l.ItemId.HasValue && itemNames.TryGetValue(l.ItemId.Value, out var itemName) ? itemName.Name : null,
                Description = l.Description,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                DiscountPercent = l.DiscountPercent ?? 0m,
                DiscountAmount = l.DiscountAmount,
                LineTotal = l.LineTotal,
                TaxAmount = l.TaxAmount,
                Taxes = l.Taxes.Select(t => new DeliveryChallanLineTaxView
                {
                    TaxComponent = t.TaxComponent,
                    SubAccountId = t.SubAccountId,
                    Amount = t.Amount
                }).ToList()
            }).ToList()
        };
    }

    public async Task<long> SaveAsync(long? deliveryChallanId, SaveDeliveryChallanRequest request, CancellationToken ct)
    {
        var (customerId, orgId) = _tenant.Require();

        DeliveryChallan deliveryChallan;
        if (deliveryChallanId.HasValue)
        {
            deliveryChallan = await _db.DeliveryChallans
                .Include(x => x.Lines)
                    .ThenInclude(l => l.Taxes)
                .FirstOrDefaultAsync(x => x.DeliveryChallanId == deliveryChallanId.Value, ct)
                ?? throw new InvalidOperationException("DeliveryChallan not found.");
                
            if (deliveryChallan.Status != DocumentStatus.Draft)
                throw new InvalidOperationException("Only draft DeliveryChallans can be edited.");

            _db.DeliveryChallanDetailTaxes.RemoveRange(deliveryChallan.Lines.SelectMany(l => l.Taxes));
            _db.DeliveryChallanDetails.RemoveRange(deliveryChallan.Lines);
        }
        else
        {
            var alloc = await _numbering.NextAsync("DLC", request.DocumentDate, ct);
            deliveryChallan = new DeliveryChallan
            {
                OrgId = orgId,
                DocumentNo = alloc.Code,
                TransactionTypeCode = "DLC",
                Status = DocumentStatus.Draft
            };
            _db.DeliveryChallans.Add(deliveryChallan);
        }

        deliveryChallan.ChallanType = request.ChallanType;
        deliveryChallan.SalesOrderId = request.SalesOrderId;
        deliveryChallan.VehicleNo = request.VehicleNo;
        deliveryChallan.TransporterName = request.TransporterName;
        deliveryChallan.EwayBillNo = request.EwayBillNo;
        deliveryChallan.EwayBillDate = request.EwayBillDate;
        deliveryChallan.DispatchDate = request.DispatchDate;

        deliveryChallan.ContactId = request.ContactId;
        deliveryChallan.DocumentDate = request.DocumentDate;
        deliveryChallan.Notes = request.Notes;
        deliveryChallan.CurrencyCode = request.CurrencyCode ?? "USD";
        deliveryChallan.ExchangeRate = request.ExchangeRate;
        deliveryChallan.BillingAddress = request.BillingAddress;
        deliveryChallan.ShippingAddress = request.ShippingAddress;

        foreach (var reqLine in request.Lines)
        {
            var line = new DeliveryChallanDetail
            {
                OrgId = deliveryChallan.OrgId,
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
                line.Taxes.Add(new DeliveryChallanDetailTax
                {
                    OrgId = deliveryChallan.OrgId,
                    SubAccountId = taxGroupId,
                    Amount = taxAmount
                });
            }

            line.TaxAmount = line.Taxes.Sum(t => t.Amount);
            deliveryChallan.Lines.Add(line);
        }

        deliveryChallan.SubTotal = deliveryChallan.Lines.Sum(l => l.LineTotal + l.DiscountAmount);
        deliveryChallan.DiscountAmount = deliveryChallan.Lines.Sum(l => l.DiscountAmount);
        deliveryChallan.TaxableAmount = deliveryChallan.Lines.Sum(l => l.LineTotal);
        deliveryChallan.CgstAmount = deliveryChallan.Lines.Sum(l => l.TaxAmount) / 2;
        deliveryChallan.SgstAmount = deliveryChallan.Lines.Sum(l => l.TaxAmount) / 2;
        deliveryChallan.TotalAmount = deliveryChallan.Lines.Sum(l => l.LineTotal + l.TaxAmount);
        deliveryChallan.TotalAmountBase = deliveryChallan.TotalAmount * deliveryChallan.ExchangeRate;

        await _db.SaveChangesAsync(ct);
        return deliveryChallan.DeliveryChallanId;
    }

    public async Task PostAsync(long deliveryChallanId, CancellationToken ct)
    {
        var (customerId, orgId) = _tenant.Require();

        var deliveryChallan = await _db.DeliveryChallans
            .Include(x => x.Lines)
                .ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(x => x.DeliveryChallanId == deliveryChallanId, ct)
            ?? throw new InvalidOperationException("DeliveryChallan not found.");

        if (deliveryChallan.Status != DocumentStatus.Draft)
            throw new InvalidOperationException("DeliveryChallan is not in draft status.");

        if (deliveryChallan.Lines.Count == 0)
            throw new InvalidOperationException("DeliveryChallan has no lines.");

        // 1. Issue Stock
        var issueRequest = new IssueStockRequest
        {
            OrgId = deliveryChallan.OrgId,
            CustomerId = customerId,
            MovementDate = deliveryChallan.DocumentDate,
            SourceType = deliveryChallan.TransactionTypeCode,
            SourceId = deliveryChallan.DeliveryChallanId,
            Lines = deliveryChallan.Lines.Select(l => new IssueStockLine
            {
                SourceLineId = l.DeliveryChallanDetailId,
                ItemId = l.ItemId ?? 0,
                Quantity = l.Quantity,
                WarehouseId = null,
                ReleaseReservation = deliveryChallan.SalesOrderId.HasValue
            }).ToList()
        };

        var issueResult = await _inventoryClient.IssueAsync(issueRequest, ct);
        if (!issueResult.Success)
            throw new InvalidOperationException("Stock issue failed.");

        foreach (var issueLine in issueResult.Lines)
        {
            var line = deliveryChallan.Lines.First(l => l.DeliveryChallanDetailId == issueLine.SourceLineId);
            line.StockMovementId = issueLine.StockMovementId;
            line.UnitCost = issueLine.UnitCost;
        }

        // 2. Post Ledger (Only if Sale)
        if (deliveryChallan.ChallanType == ChallanType.Sale)
        {
            var baseCurrency = await _baseCurrency.GetBaseCurrencyAsync(ct);
            var postRequest = new PostLedgerRequest
            {
                CustomerId = customerId,
                OrgId = deliveryChallan.OrgId,
                TransactionTypeCode = deliveryChallan.TransactionTypeCode,
                TransactionId = deliveryChallan.DeliveryChallanId,
                LedgerDate = deliveryChallan.DocumentDate,
                CurrencyCode = deliveryChallan.CurrencyCode == baseCurrency ? null : deliveryChallan.CurrencyCode,
                ExchangeRate = deliveryChallan.CurrencyCode == baseCurrency ? null : deliveryChallan.ExchangeRate,
                ContactId = deliveryChallan.ContactId,
                SourceDocumentId = deliveryChallan.DeliveryChallanId,
                Legs = new List<LedgerLegRequest>()
            };

            decimal totalCogs = issueResult.TotalValue;

            if (totalCogs > 0)
            {
                postRequest.Legs.Add(new LedgerLegRequest
                {
                    LedgerTypeId = 5, // Expense -> GDNI
                    LedgerSourceId = 3,
                    TransactionDetailId = 0,
                    AccountSystemName = "Goods Delivered Not Invoiced",
                    Amount = totalCogs
                });

                postRequest.Legs.Add(new LedgerLegRequest
                {
                    LedgerTypeId = 1, // Asset -> Inventory
                    LedgerSourceId = 3,
                    TransactionDetailId = 0,
                    AccountSystemName = "Inventory",
                    Amount = totalCogs
                });

                await _ledgerClient.PostAsync(postRequest, ct);
            }
        }

        foreach (var l in deliveryChallan.Lines)
        {
            var rate = l.Taxes.FirstOrDefault()?.Rate ?? 0;
            _db.SalesRegister.Add(new SalesRegister
            {
                OrgId = deliveryChallan.OrgId,
                TransactionTypeCode = deliveryChallan.TransactionTypeCode,
                SourceId = deliveryChallan.DeliveryChallanId,
                DocumentNo = deliveryChallan.DocumentNo,
                DocumentDate = deliveryChallan.DocumentDate,
                ContactId = deliveryChallan.ContactId,
                ContactGstin = deliveryChallan.ContactGstin,
                PlaceOfSupplyStateId = deliveryChallan.PlaceOfSupplyStateId,
                IsInterState = deliveryChallan.IsInterState,
                SupplyType = deliveryChallan.ContactGstin != null ? "B2B" : "B2CS",
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
                CurrencyCode = deliveryChallan.CurrencyCode,
                ExchangeRate = deliveryChallan.ExchangeRate,
                TaxableAmountBase = l.LineTotal * deliveryChallan.ExchangeRate
            });
        }

        deliveryChallan.Status = DocumentStatus.Posted;
        deliveryChallan.PostedAt = _clock.GetUtcNow();
        deliveryChallan.PostedBy = _user.UserId;

        await _db.SaveChangesAsync(ct);
    }

    public async Task VoidAsync(long deliveryChallanId, CancellationToken ct)
    {
        var deliveryChallan = await _db.DeliveryChallans
            .FirstOrDefaultAsync(x => x.DeliveryChallanId == deliveryChallanId, ct)
            ?? throw new InvalidOperationException("DeliveryChallan not found.");

        if (deliveryChallan.Status == DocumentStatus.Void)
            return;

        deliveryChallan.Status = DocumentStatus.Void;
        deliveryChallan.VoidedAt = _clock.GetUtcNow();

        var registers = await _db.SalesRegister
            .Where(r => r.SourceId == deliveryChallanId && r.TransactionTypeCode == deliveryChallan.TransactionTypeCode)
            .ToListAsync(ct);
        _db.SalesRegister.RemoveRange(registers);

        await _db.SaveChangesAsync(ct);
    }
}


