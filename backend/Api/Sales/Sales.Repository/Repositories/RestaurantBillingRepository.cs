using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository.Repositories;
using Shared.Kernel.Documents;

namespace Sales.Repository.Repositories;

public class RestaurantBillingRepository
{
    private readonly SalesDbContext _db;
    private readonly InvoiceRepository _invoiceRepo;

    public RestaurantBillingRepository(SalesDbContext db, InvoiceRepository invoiceRepo)
    {
        _db = db;
        _invoiceRepo = invoiceRepo;
    }

    public async Task<InvoiceResult> BillOrderAsync(long restaurantOrderId, SaveInvoiceRequest customRequestFields, CancellationToken ct = default)
    {
        var order = await _db.RestaurantOrders
            .FirstOrDefaultAsync(o => o.RestaurantOrderId == restaurantOrderId, ct);
            
        if (order == null) return new InvoiceResult(InvoiceOutcome.NotFound);
        if (order.Status != RestaurantOrderStatus.Open) return new InvoiceResult(InvoiceOutcome.SourceInvalid, Detail: "Order is not open.");

        var lines = await _db.RestaurantOrderDetails
            .Where(l => l.RestaurantOrderId == restaurantOrderId)
            .ToListAsync(ct);

        var req = new SaveInvoiceRequest
        {
            DocumentDate = customRequestFields.DocumentDate != default ? customRequestFields.DocumentDate : DateOnly.FromDateTime(DateTime.UtcNow),
            ContactId = order.ContactId ?? customRequestFields.ContactId,
            DueDate = customRequestFields.DueDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            PlaceOfSupplyStateCode = customRequestFields.PlaceOfSupplyStateCode,
            Notes = customRequestFields.Notes,
            TillId = customRequestFields.TillId,
            CashierUserId = customRequestFields.CashierUserId,
            TenderedAmount = customRequestFields.TenderedAmount,
            ChangeAmount = customRequestFields.ChangeAmount,
            PaymentMode = customRequestFields.PaymentMode,
            Lines = lines.Select(l => new SaveInvoiceLineRequest
            {
                ItemId = l.MenuItemId,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                IsPriceInclusive = true,
                TaxTreatment = TaxTreatment.Taxable,
                LineType = DocumentLineType.Stock
            }).ToList()
        };
        
        var created = await _invoiceRepo.CreateAsync(req, ct);
        if (created.Outcome != InvoiceOutcome.Ok) return created;

        var posted = await _invoiceRepo.PostAsync(created.InvoiceId, ct);
        
        if (posted.Outcome == InvoiceOutcome.Ok)
        {
            order.Status = RestaurantOrderStatus.Billed;
            await _db.SaveChangesAsync(ct);
        }

        return posted;
    }
}
