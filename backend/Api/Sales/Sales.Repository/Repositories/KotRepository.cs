using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sales.Entity.TableEntities;
using Shared.Kernel.Restaurant;
using Shared.Kernel.Approvals;
using Shared.Kernel.Numbering;

namespace Sales.Repository.Repositories;

public class KotRepository
{
    private readonly SalesDbContext _db;
    private readonly INumberGenerator _numberGen;

    public KotRepository(SalesDbContext db, INumberGenerator numberGen)
    {
        _db = db;
        _numberGen = numberGen;
    }

    public async Task<List<Kot>> SendOrderLinesAsync(long restaurantOrderId)
    {
        // This is a simplified version. In reality we check which lines are new
        var lines = await _db.RestaurantOrderDetails
            .Where(l => l.RestaurantOrderId == restaurantOrderId)
            .ToListAsync();
            
        // Group by station and create KOT
        var kots = new List<Kot>();
        var kot = new Kot
        {
            RestaurantOrderId = restaurantOrderId,
            Station = MenuStation.Kitchen,
            KotNumber = (await _numberGen.NextAsync("KOT", DateOnly.FromDateTime(DateTime.UtcNow), default)).Number.ToString()
        };
        _db.Kots.Add(kot);
        await _db.SaveChangesAsync();

        foreach (var l in lines)
        {
            _db.KotLines.Add(new KotLine
            {
                KotId = kot.KotId,
                RestaurantOrderDetailId = l.RestaurantOrderDetailId,
                Quantity = l.Quantity,
                Status = KotLineStatus.Sent
            });
        }
        await _db.SaveChangesAsync();
        kots.Add(kot);
        return kots;
    }

    public async Task ReprintAsync(long kotId)
    {
        var kot = await _db.Kots.FindAsync(kotId);
        if (kot != null)
        {
            kot.PrintedCount++;
            await _db.SaveChangesAsync();
        }
    }

    public async Task CancelLineAsync(long kotLineId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason is mandatory");
        
        var line = await _db.KotLines.FindAsync(kotLineId);
        if (line != null)
        {
            // Simplified approval check
            if (line.Status == KotLineStatus.Preparing || line.Status == KotLineStatus.Ready)
            {
                // Approval request would be triggered here
            }
            line.Status = KotLineStatus.Cancelled;
            line.CancelReason = reason;
            await _db.SaveChangesAsync();
        }
    }

    public async Task MoveLineStatusAsync(long kotLineId, KotLineStatus newStatus)
    {
        var line = await _db.KotLines.FindAsync(kotLineId);
        if (line != null)
        {
            line.Status = newStatus;
            await _db.SaveChangesAsync();
        }
    }
    public async Task<List<Kot>> GetActiveTicketsByStationAsync(MenuStation station)
    {
        return await _db.Kots
            .Where(k => k.Station == station)
            .ToListAsync();
    }
}
