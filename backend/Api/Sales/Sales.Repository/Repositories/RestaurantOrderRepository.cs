using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sales.Entity.TableEntities;
using Shared.Kernel.Restaurant;
using Shared.Kernel.Numbering;

namespace Sales.Repository.Repositories;

public class RestaurantOrderRepository
{
    private readonly SalesDbContext _db;
    private readonly INumberGenerator _numberGenerator;

    public RestaurantOrderRepository(SalesDbContext db, INumberGenerator numberGenerator)
    {
        _db = db;
        _numberGenerator = numberGenerator;
    }

    public async Task<RestaurantOrder> OpenOrderAsync(RestaurantOrder order)
    {
        var allocation = await _numberGenerator.NextAsync("RST", DateOnly.FromDateTime(DateTime.UtcNow), default);
        order.OrderNumber = allocation.Code;
        order.Status = RestaurantOrderStatus.Open;
        _db.RestaurantOrders.Add(order);
        await _db.SaveChangesAsync();
        return order;
    }

    public async Task AddLineAsync(long orderId, RestaurantOrderDetail line)
    {
        var order = await _db.RestaurantOrders.FindAsync(orderId);
        if (order == null || order.Status != RestaurantOrderStatus.Open) throw new InvalidOperationException("Order not open");

        line.RestaurantOrderId = orderId;
        _db.RestaurantOrderDetails.Add(line);
        order.TotalAmount += line.TotalPrice;
        await _db.SaveChangesAsync();
    }

    public async Task RemoveLineAsync(long orderId, long lineId)
    {
        var line = await _db.RestaurantOrderDetails.FirstOrDefaultAsync(l => l.RestaurantOrderDetailId == lineId && l.RestaurantOrderId == orderId);
        if (line != null)
        {
            var order = await _db.RestaurantOrders.FindAsync(orderId);
            order!.TotalAmount -= line.TotalPrice;
            _db.RestaurantOrderDetails.Remove(line);
            await _db.SaveChangesAsync();
        }
    }

    public async Task ChangeOrderTypeAsync(long orderId, OrderType newType)
    {
        var order = await _db.RestaurantOrders.FindAsync(orderId);
        if (order != null && order.Status == RestaurantOrderStatus.Open)
        {
            order.OrderType = newType;
            await _db.SaveChangesAsync();
        }
    }

    public async Task MoveToTableAsync(long orderId, long newTableId)
    {
        var order = await _db.RestaurantOrders.FindAsync(orderId);
        if (order != null && order.Status == RestaurantOrderStatus.Open)
        {
            order.RestaurantTableId = newTableId;
            await _db.SaveChangesAsync();
        }
    }
}







