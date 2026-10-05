using System.Linq.Expressions;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Apps;
using Shared.Kernel.Documents;
using Shared.Kernel.Internal;

namespace Reporting.Repository.Repositories;

// -----------------------------------------------------------------------------
// Restaurant reports (TK-150): day-end, item and category sales, waiter-wise
// sales, table turnover, food cost, wastage and KOT cancellations.
//
// Sales figures are read off posted sales invoices, because a restaurant bill
// is one (TK-148): the day-end report's total is the day's posted invoices,
// to the paisa, and the item and category reports add up to its item sales.
// The queries are functions of plain IQueryables so the arithmetic can be
// tested over lists; each source only hands them its context's sets.
// -----------------------------------------------------------------------------

/// <summary>The codes the restaurant reports read by name.</summary>
public static class RestaurantReportCodes
{
    /// <summary>An order whose dishes were billed.</summary>
    public const string Billed = "Billed";

    /// <summary>A dish or kitchen ticket line taken back.</summary>
    public const string Cancelled = "Cancelled";

    /// <summary>The stock movement a recipe's ingredients go out on (TK-149).</summary>
    public const string RecipeConsumption = "RCN";

    /// <summary>The stock adjustment a wastage entry goes out on.</summary>
    public const string StockAdjustment = "STA";

    public const string Cash = "Cash";
    public const string Card = "Card";
    public const string Upi = "Upi";

    /// <summary>What a dish with no category is grouped under.</summary>
    public const string Uncategorised = "Uncategorised";
}

/// <summary>The restaurant reports' queries, composed and never executed here.</summary>
public static class RestaurantReports
{
    /// <summary>The invoices a period's sales are read from: posted, and in the period.</summary>
    public static IQueryable<InvoiceRead> Posted(IQueryable<InvoiceRead> invoices, DateOnly? fromDate, DateOnly? toDate) =>
        invoices.Where(i =>
            i.Status == DocumentStatus.Posted
            && (fromDate == null || i.DocumentDate >= fromDate)
            && (toDate == null || i.DocumentDate <= toDate));

    /// <summary>
    /// One row per trading day: the day's posted invoices, split into item
    /// sales, service charge, other lines and tax, with the counter tenders and
    /// the tips beside them. <c>Total</c> is the invoices' own base total, so a
    /// day's row equals what the ledger was posted; the split is in base
    /// currency at each invoice's rate, and any difference from rounding is
    /// shown as round-off rather than hidden in a line.
    /// </summary>
    public static IQueryable<RestaurantDayEndRow> DayEnd(
        IQueryable<InvoiceRead> invoices,
        IQueryable<InvoiceDetailRead> details,
        IQueryable<RestaurantBillRead> bills,
        IQueryable<InvoiceTenderRead> tenders,
        IQueryable<RestaurantOrderRead> orders,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        IQueryable<InvoiceRead> posted = Posted(invoices, fromDate, toDate);

        IQueryable<RestaurantDayEndRow> raw =
            from day in posted.Select(i => i.DocumentDate).Distinct()
            let dayInvoices = posted.Where(i => i.DocumentDate == day)
            select new RestaurantDayEndRow
            {
                Date = day,
                Bills = dayInvoices.Count(),
                Covers = orders
                    .Where(o => o.OrderDate == day && o.Status == RestaurantReportCodes.Billed)
                    .Sum(o => (decimal)(o.Guests ?? 0)),
                ItemSales = (from d in details
                             join i in dayInvoices on d.InvoiceId equals i.InvoiceId
                             where d.ItemId != null
                             select d.TaxableAmount * i.ExchangeRate).Sum(),
                Taxable = (from d in details
                           join i in dayInvoices on d.InvoiceId equals i.InvoiceId
                           select d.TaxableAmount * i.ExchangeRate).Sum(),
                ServiceCharge = (from b in bills
                                 join i in dayInvoices on b.InvoiceId equals i.InvoiceId
                                 select b.ServiceChargeAmount * i.ExchangeRate).Sum(),
                Tax = (from d in details
                       join i in dayInvoices on d.InvoiceId equals i.InvoiceId
                       select d.TaxAmount * i.ExchangeRate).Sum(),
                Total = dayInvoices.Sum(i => i.TotalAmountBase),
                Cash = (from t in tenders
                        join i in dayInvoices on t.InvoiceId equals i.InvoiceId
                        where t.Mode == RestaurantReportCodes.Cash
                        select t.Amount * i.ExchangeRate).Sum(),
                Card = (from t in tenders
                        join i in dayInvoices on t.InvoiceId equals i.InvoiceId
                        where t.Mode == RestaurantReportCodes.Card
                        select t.Amount * i.ExchangeRate).Sum(),
                Upi = (from t in tenders
                       join i in dayInvoices on t.InvoiceId equals i.InvoiceId
                       where t.Mode == RestaurantReportCodes.Upi
                       select t.Amount * i.ExchangeRate).Sum(),
                Tips = (from b in bills
                        join i in dayInvoices on b.InvoiceId equals i.InvoiceId
                        select b.TipAmount).Sum(),
            };

        return raw.Select(r => new RestaurantDayEndRow
        {
            Date = r.Date,
            Bills = r.Bills,
            Covers = r.Covers,
            ItemSales = r.ItemSales,
            Taxable = r.Taxable,
            ServiceCharge = r.ServiceCharge,
            OtherSales = r.Taxable - r.ItemSales - r.ServiceCharge,
            Tax = r.Tax,
            RoundOff = r.Total - r.Taxable - r.Tax,
            Total = r.Total,
            Cash = r.Cash,
            Card = r.Card,
            Upi = r.Upi,
            OnAccount = r.Total - r.Cash - r.Card - r.Upi,
            Tips = r.Tips,
            AverageBill = r.Bills != 0 ? Math.Round(r.Total / r.Bills, 2) : null,
            AveragePerCover = r.Covers != 0m ? Math.Round(r.Total / r.Covers, 2) : null,
        });
    }

    /// <summary>
    /// One row per item sold on a posted invoice in the period: the quantity,
    /// its taxable value and tax in base currency. Lines with no item — the
    /// service charge, an account line — are not here; they are on the day-end.
    /// </summary>
    public static IQueryable<RestaurantItemSalesRow> ItemSales(
        IQueryable<InvoiceRead> invoices,
        IQueryable<InvoiceDetailRead> details,
        IQueryable<ItemRead> items,
        IQueryable<ItemCategoryRead> categories,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        IQueryable<InvoiceRead> posted = Posted(invoices, fromDate, toDate);

        var sold =
            from d in details
            join i in posted on d.InvoiceId equals i.InvoiceId
            where d.ItemId != null
            group new { d.Quantity, Taxable = d.TaxableAmount * i.ExchangeRate, Tax = d.TaxAmount * i.ExchangeRate }
                by d.ItemId!.Value into g
            select new
            {
                ItemId = g.Key,
                Quantity = g.Sum(x => x.Quantity),
                Taxable = g.Sum(x => x.Taxable),
                Tax = g.Sum(x => x.Tax),
            };

        return from s in sold
               join it in items on s.ItemId equals it.ItemId
               join c in categories on it.ItemCategoryId equals (long?)c.ItemCategoryId into gc
               from c in gc.DefaultIfEmpty()
               select new RestaurantItemSalesRow
               {
                   ItemId = s.ItemId,
                   ItemCode = it.ItemCode,
                   ItemName = it.ItemName,
                   Category = c != null ? c.CategoryName : RestaurantReportCodes.Uncategorised,
                   Quantity = s.Quantity,
                   NetSales = s.Taxable,
                   Tax = s.Tax,
                   Total = s.Taxable + s.Tax,
                   AveragePrice = s.Quantity != 0m ? Math.Round(s.Taxable / s.Quantity, 2) : null,
               };
    }

    /// <summary>The item sales, added up per category.</summary>
    public static IQueryable<RestaurantCategorySalesRow> CategorySales(
        IQueryable<InvoiceRead> invoices,
        IQueryable<InvoiceDetailRead> details,
        IQueryable<ItemRead> items,
        IQueryable<ItemCategoryRead> categories,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        IQueryable<InvoiceRead> posted = Posted(invoices, fromDate, toDate);

        var sold =
            from d in details
            join i in posted on d.InvoiceId equals i.InvoiceId
            join it in items on d.ItemId equals (long?)it.ItemId
            group new { d.Quantity, Taxable = d.TaxableAmount * i.ExchangeRate, Tax = d.TaxAmount * i.ExchangeRate }
                by it.ItemCategoryId into g
            select new
            {
                CategoryId = g.Key,
                Quantity = g.Sum(x => x.Quantity),
                Taxable = g.Sum(x => x.Taxable),
                Tax = g.Sum(x => x.Tax),
            };

        return from s in sold
               join c in categories on s.CategoryId equals (long?)c.ItemCategoryId into gc
               from c in gc.DefaultIfEmpty()
               select new RestaurantCategorySalesRow
               {
                   CategoryKey = s.CategoryId ?? 0,
                   Category = c != null ? c.CategoryName : RestaurantReportCodes.Uncategorised,
                   Quantity = s.Quantity,
                   NetSales = s.Taxable,
                   Tax = s.Tax,
                   Total = s.Taxable + s.Tax,
               };
    }

    /// <summary>
    /// One row per waiter: the orders and covers they served whose dishes were
    /// billed on a posted invoice in the period, those dishes at menu price
    /// (before service charge and the invoice's tax split), and the dishes
    /// cancelled on their orders opened in the period. Orders taken with no
    /// waiter are one row of their own.
    /// </summary>
    public static IQueryable<RestaurantWaiterSalesRow> WaiterSales(
        IQueryable<InvoiceRead> invoices,
        IQueryable<RestaurantBillRead> bills,
        IQueryable<RestaurantOrderRead> orders,
        IQueryable<RestaurantOrderLineRead> lines,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        IQueryable<InvoiceRead> posted = Posted(invoices, fromDate, toDate);

        IQueryable<RestaurantOrderLineRead> billed =
            from l in lines
            join b in bills on l.RestaurantBillId equals (long?)b.RestaurantBillId
            join i in posted on b.InvoiceId equals i.InvoiceId
            where l.KitchenStatus != RestaurantReportCodes.Cancelled
            select l;

        IQueryable<RestaurantOrderRead> opened = orders.Where(o =>
            (fromDate == null || o.OrderDate >= fromDate) && (toDate == null || o.OrderDate <= toDate));

        IQueryable<RestaurantWaiterSalesRow> raw =
            from w in orders.Select(o => o.WaiterUserId).Distinct()
            let served = orders.Where(o => o.WaiterUserId == w && billed.Any(l => l.RestaurantOrderId == o.RestaurantOrderId))
            select new RestaurantWaiterSalesRow
            {
                WaiterUserId = w,
                WaiterKey = w != null ? w.Value.ToString() : string.Empty,
                Orders = served.Count(),
                Covers = served.Sum(o => (decimal)(o.Guests ?? 0)),
                Dishes = (from l in billed
                          join o in orders on l.RestaurantOrderId equals o.RestaurantOrderId
                          where o.WaiterUserId == w
                          select l.Quantity).Sum(),
                Sales = (from l in billed
                         join o in orders on l.RestaurantOrderId equals o.RestaurantOrderId
                         where o.WaiterUserId == w
                         select l.Quantity * l.UnitPrice).Sum(),
                CancelledDishes = (from l in lines
                                   join o in opened on l.RestaurantOrderId equals o.RestaurantOrderId
                                   where o.WaiterUserId == w && l.KitchenStatus == RestaurantReportCodes.Cancelled
                                   select l.Quantity).Sum(),
            };

        return raw
            .Where(r => r.Orders > 0 || r.CancelledDishes > 0m)
            .Select(r => new RestaurantWaiterSalesRow
            {
                WaiterUserId = r.WaiterUserId,
                WaiterKey = r.WaiterKey,
                Orders = r.Orders,
                Covers = r.Covers,
                Dishes = r.Dishes,
                Sales = r.Sales,
                CancelledDishes = r.CancelledDishes,
                AveragePerCover = r.Covers != 0m ? Math.Round(r.Sales / r.Covers, 2) : null,
            });
    }

    /// <summary>
    /// One row per table: how often it was turned — billed orders opened at it
    /// in the period — the covers it sat, how full it was, and the dishes it
    /// billed at menu price. A table with no turns is shown while it is active,
    /// because an idle table is the finding.
    /// </summary>
    public static IQueryable<RestaurantTableTurnoverRow> TableTurnover(
        IQueryable<FloorRead> floors,
        IQueryable<RestaurantTableRead> tables,
        IQueryable<RestaurantOrderRead> orders,
        IQueryable<RestaurantOrderLineRead> lines,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        IQueryable<RestaurantOrderRead> turned = orders.Where(o =>
            o.Status == RestaurantReportCodes.Billed
            && o.RestaurantTableId != null
            && (fromDate == null || o.OrderDate >= fromDate)
            && (toDate == null || o.OrderDate <= toDate));

        IQueryable<RestaurantTableTurnoverRow> raw =
            from t in tables
            join f in floors on t.FloorId equals f.FloorId
            let turns = turned.Where(o => o.RestaurantTableId == t.RestaurantTableId)
            select new RestaurantTableTurnoverRow
            {
                RestaurantTableId = t.RestaurantTableId,
                Floor = f.Name,
                Table = t.Code,
                Seats = t.Seats,
                IsActive = t.IsActive,
                Turns = turns.Count(),
                Covers = turns.Sum(o => (decimal)(o.Guests ?? 0)),
                Sales = (from l in lines
                         join o in turns on l.RestaurantOrderId equals o.RestaurantOrderId
                         where l.RestaurantBillId != null && l.KitchenStatus != RestaurantReportCodes.Cancelled
                         select l.Quantity * l.UnitPrice).Sum(),
            };

        return raw
            .Where(r => r.Turns > 0 || r.IsActive)
            .Select(r => new RestaurantTableTurnoverRow
            {
                RestaurantTableId = r.RestaurantTableId,
                Floor = r.Floor,
                Table = r.Table,
                Seats = r.Seats,
                IsActive = r.IsActive,
                Turns = r.Turns,
                Covers = r.Covers,
                Sales = r.Sales,
                AverageCovers = r.Turns != 0 ? Math.Round(r.Covers / r.Turns, 2) : null,
                SeatUsePercent = r.Turns != 0 && r.Seats != 0 ? Math.Round(r.Covers / (r.Turns * r.Seats) * 100m, 2) : null,
                SalesPerTurn = r.Turns != 0 ? Math.Round(r.Sales / r.Turns, 2) : null,
            });
    }

    /// <summary>
    /// One row per day: the day's item sales on posted invoices against the
    /// ingredients its bills used (the recipes' <c>RCN</c> issues) and the
    /// stock written off as wastage, at what the costing engine settled them
    /// at. A movement still waiting on the costing engine counts as nothing and
    /// is counted in <c>Uncosted</c>, so a low figure on a busy day says why.
    /// </summary>
    public static IQueryable<RestaurantFoodCostRow> FoodCost(
        IQueryable<InvoiceRead> invoices,
        IQueryable<InvoiceDetailRead> details,
        IQueryable<StockMovementRead> movements,
        IQueryable<WastageEntryRead> wastage,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        IQueryable<InvoiceRead> posted = Posted(invoices, fromDate, toDate);

        IQueryable<StockMovementRead> used = movements.Where(m =>
            m.SourceType == RestaurantReportCodes.RecipeConsumption
            && (fromDate == null || m.MovementDate >= fromDate)
            && (toDate == null || m.MovementDate <= toDate));

        IQueryable<StockMovementRead> wasted =
            from m in movements
            where m.SourceType == RestaurantReportCodes.StockAdjustment
                && (fromDate == null || m.MovementDate >= fromDate)
                && (toDate == null || m.MovementDate <= toDate)
                && wastage.Any(w => w.StockAdjustmentId == m.SourceId && w.ItemId == m.ItemId)
            select m;

        IQueryable<DateOnly> days = posted.Select(i => i.DocumentDate)
            .Union(used.Select(m => m.MovementDate))
            .Union(wasted.Select(m => m.MovementDate));

        IQueryable<RestaurantFoodCostRow> raw =
            from day in days
            select new RestaurantFoodCostRow
            {
                Date = day,
                FoodSales = (from d in details
                             join i in posted on d.InvoiceId equals i.InvoiceId
                             where i.DocumentDate == day && d.ItemId != null
                             select d.TaxableAmount * i.ExchangeRate).Sum(),
                IngredientCost = used.Where(m => m.MovementDate == day).Sum(m => m.TotalCost ?? 0m),
                WastageCost = wasted.Where(m => m.MovementDate == day).Sum(m => m.TotalCost ?? 0m),
                Uncosted = used.Count(m => m.MovementDate == day && m.TotalCost == null)
                    + wasted.Count(m => m.MovementDate == day && m.TotalCost == null),
            };

        return raw.Select(r => new RestaurantFoodCostRow
        {
            Date = r.Date,
            FoodSales = r.FoodSales,
            IngredientCost = r.IngredientCost,
            WastageCost = r.WastageCost,
            FoodCost = r.IngredientCost + r.WastageCost,
            GrossMargin = r.FoodSales - r.IngredientCost - r.WastageCost,
            FoodCostPercent = r.FoodSales != 0m
                ? Math.Round((r.IngredientCost + r.WastageCost) / r.FoodSales * 100m, 2)
                : null,
            Uncosted = r.Uncosted,
        });
    }

    /// <summary>
    /// One row per wastage entry in the period, valued at what its stock
    /// adjustment's movement cost. An entry whose adjustment is waiting for
    /// approval or for the costing engine shows as not costed.
    /// </summary>
    public static IQueryable<RestaurantWastageRow> Wastage(
        IQueryable<WastageEntryRead> wastage,
        IQueryable<ItemRead> items,
        IQueryable<ItemCategoryRead> categories,
        IQueryable<UnitOfMeasureRead> units,
        IQueryable<StockMovementRead> movements,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        IQueryable<StockMovementRead> adjusted = movements.Where(m => m.SourceType == RestaurantReportCodes.StockAdjustment);

        return from w in wastage
               where (fromDate == null || w.EntryDate >= fromDate) && (toDate == null || w.EntryDate <= toDate)
               join it in items on w.ItemId equals it.ItemId
               join c in categories on it.ItemCategoryId equals (long?)c.ItemCategoryId into gc
               from c in gc.DefaultIfEmpty()
               join u in units on w.UomId equals u.UomId into gu
               from u in gu.DefaultIfEmpty()
               let moved = adjusted.Where(m => m.SourceId == w.StockAdjustmentId && m.ItemId == w.ItemId)
               select new RestaurantWastageRow
               {
                   WastageEntryId = w.WastageEntryId,
                   Date = w.EntryDate,
                   ItemCode = it.ItemCode,
                   ItemName = it.ItemName,
                   Category = c != null ? c.CategoryName : RestaurantReportCodes.Uncategorised,
                   Quantity = w.Quantity,
                   Unit = u != null ? u.UomCode : null,
                   Reason = w.Reason,
                   Note = w.Note,
                   Cost = moved.Sum(m => m.TotalCost ?? 0m),
                   IsCosted = moved.Any() && !moved.Any(m => m.TotalCost == null),
               };
    }

    /// <summary>
    /// One row per kitchen ticket line taken back on an order opened in the
    /// period: what it was, its value at menu price, why, and who cancelled it.
    /// A dish cancelled before it was sent never reached the kitchen and is not
    /// here.
    /// </summary>
    public static IQueryable<RestaurantKotCancellationRow> KotCancellations(
        IQueryable<KotRead> kots,
        IQueryable<KotLineRead> kotLines,
        IQueryable<RestaurantOrderRead> orders,
        IQueryable<RestaurantOrderLineRead> lines,
        IQueryable<RestaurantTableRead> tables,
        DateOnly? fromDate,
        DateOnly? toDate) =>
        from kl in kotLines
        where kl.Status == RestaurantReportCodes.Cancelled
        join k in kots on kl.KotId equals k.KotId
        join l in lines on kl.RestaurantOrderLineId equals l.RestaurantOrderLineId
        join o in orders on k.RestaurantOrderId equals o.RestaurantOrderId
        where (fromDate == null || o.OrderDate >= fromDate) && (toDate == null || o.OrderDate <= toDate)
        join t in tables on o.RestaurantTableId equals (long?)t.RestaurantTableId into gt
        from t in gt.DefaultIfEmpty()
        select new RestaurantKotCancellationRow
        {
            KotLineId = kl.KotLineId,
            Date = o.OrderDate,
            CancelledAt = kl.StatusChangedAt,
            KotNo = k.KotNo,
            OrderNo = o.OrderNo,
            Table = t != null ? t.Code : o.OrderType,
            Station = k.Station,
            Dish = l.VariantName != null ? l.ItemName + " (" + l.VariantName + ")" : l.ItemName,
            Quantity = kl.Quantity,
            Value = kl.Quantity * l.UnitPrice,
            Reason = kl.CancelReason ?? l.CancelReason,
            WaiterUserId = o.WaiterUserId,
            CancelledByUserId = kl.ModifiedBy,
        };
}

// ---- Rows ----------------------------------------------------------------------

public sealed class RestaurantDayEndRow
{
    public DateOnly Date { get; set; }
    public int Bills { get; set; }
    public decimal Covers { get; set; }
    public decimal ItemSales { get; set; }
    public decimal Taxable { get; set; }
    public decimal ServiceCharge { get; set; }
    public decimal OtherSales { get; set; }
    public decimal Tax { get; set; }
    public decimal RoundOff { get; set; }
    public decimal Total { get; set; }
    public decimal Cash { get; set; }
    public decimal Card { get; set; }
    public decimal Upi { get; set; }
    public decimal OnAccount { get; set; }
    public decimal Tips { get; set; }
    public decimal? AverageBill { get; set; }
    public decimal? AveragePerCover { get; set; }
}

public sealed class RestaurantItemSalesRow
{
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string Category { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal NetSales { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public decimal? AveragePrice { get; set; }
}

public sealed class RestaurantCategorySalesRow
{
    public long CategoryKey { get; set; }
    public string Category { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal NetSales { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
}

public sealed class RestaurantWaiterSalesRow
{
    public Guid? WaiterUserId { get; set; }
    public string WaiterKey { get; set; } = null!;
    public string? WaiterName { get; set; }
    public int Orders { get; set; }
    public decimal Covers { get; set; }
    public decimal Dishes { get; set; }
    public decimal Sales { get; set; }
    public decimal CancelledDishes { get; set; }
    public decimal? AveragePerCover { get; set; }
}

public sealed class RestaurantTableTurnoverRow
{
    public long RestaurantTableId { get; set; }
    public string Floor { get; set; } = null!;
    public string Table { get; set; } = null!;
    public int Seats { get; set; }
    public bool IsActive { get; set; }
    public int Turns { get; set; }
    public decimal Covers { get; set; }
    public decimal Sales { get; set; }
    public decimal? AverageCovers { get; set; }
    public decimal? SeatUsePercent { get; set; }
    public decimal? SalesPerTurn { get; set; }
}

public sealed class RestaurantFoodCostRow
{
    public DateOnly Date { get; set; }
    public decimal FoodSales { get; set; }
    public decimal IngredientCost { get; set; }
    public decimal WastageCost { get; set; }
    public decimal FoodCost { get; set; }
    public decimal GrossMargin { get; set; }
    public decimal? FoodCostPercent { get; set; }
    public int Uncosted { get; set; }
}

public sealed class RestaurantWastageRow
{
    public long WastageEntryId { get; set; }
    public DateOnly Date { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string Category { get; set; } = null!;
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public string Reason { get; set; } = null!;
    public string? Note { get; set; }
    public decimal Cost { get; set; }
    public bool IsCosted { get; set; }
}

public sealed class RestaurantKotCancellationRow
{
    public long KotLineId { get; set; }
    public DateOnly Date { get; set; }
    public DateTime CancelledAt { get; set; }
    public string KotNo { get; set; } = null!;
    public string OrderNo { get; set; } = null!;
    public string Table { get; set; } = null!;
    public string Station { get; set; } = null!;
    public string Dish { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal Value { get; set; }
    public string? Reason { get; set; }
    public Guid? WaiterUserId { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public string? WaiterName { get; set; }
    public string? CancelledBy { get; set; }
}

// ---- Sources -------------------------------------------------------------------

/// <summary>What every restaurant report has in common: the trade, the permission and a date range.</summary>
public abstract class RestaurantReportSource<TRow> : ReportSource<TRow>
    where TRow : class
{
    public override ReportModule Module => ReportModule.Restaurant;

    public override BusinessType? RequiredBusinessType => BusinessType.Restaurant;

    public override string RequiredPermission => "restaurant.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date },
    ];

    protected static DateOnly? From(ReportParameters parameters) => parameters.Date("from");

    protected static DateOnly? To(ReportParameters parameters) => parameters.Date("to");
}

public sealed class RestaurantDayEndSource : RestaurantReportSource<RestaurantDayEndRow>
{
    public override string ReportKey => "restaurant-day-end";
    public override string Title => "Restaurant Day-End";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<RestaurantDayEndRow, DateOnly>("date", ColumnDataType.Date, r => r.Date),
        ReportColumn.Of<RestaurantDayEndRow, int>("bills", ColumnDataType.Quantity, r => r.Bills, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantDayEndRow, decimal>("covers", ColumnDataType.Quantity, r => r.Covers, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantDayEndRow, decimal>("itemSales", ColumnDataType.Money, r => r.ItemSales, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantDayEndRow, decimal>("serviceCharge", ColumnDataType.Money, r => r.ServiceCharge, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantDayEndRow, decimal>("otherSales", ColumnDataType.Money, r => r.OtherSales, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantDayEndRow, decimal>("taxable", ColumnDataType.Money, r => r.Taxable, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantDayEndRow, decimal>("tax", ColumnDataType.Money, r => r.Tax, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantDayEndRow, decimal>("roundOff", ColumnDataType.Money, r => r.RoundOff, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantDayEndRow, decimal>("total", ColumnDataType.Money, r => r.Total, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantDayEndRow, decimal>("cash", ColumnDataType.Money, r => r.Cash, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantDayEndRow, decimal>("card", ColumnDataType.Money, r => r.Card, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantDayEndRow, decimal>("upi", ColumnDataType.Money, r => r.Upi, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantDayEndRow, decimal>("onAccount", ColumnDataType.Money, r => r.OnAccount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantDayEndRow, decimal>("tips", ColumnDataType.Money, r => r.Tips, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantDayEndRow, decimal?>("averageBill", ColumnDataType.Money, r => r.AverageBill),
        ReportColumn.Of<RestaurantDayEndRow, decimal?>("averagePerCover", ColumnDataType.Money, r => r.AveragePerCover),
    ];

    protected override IQueryable<RestaurantDayEndRow> Build(ReportParameters parameters, ReportingDbContext db) =>
        RestaurantReports.DayEnd(db.Invoices, db.InvoiceDetails, db.RestaurantBills, db.InvoiceTenders, db.RestaurantOrders, From(parameters), To(parameters));

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<RestaurantDayEndRow, DateOnly>>)(r => r.Date);
}

public sealed class RestaurantItemSalesSource : RestaurantReportSource<RestaurantItemSalesRow>
{
    public override string ReportKey => "restaurant-item-sales";
    public override string Title => "Restaurant Item Sales";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<RestaurantItemSalesRow, string>("itemCode", ColumnDataType.Text, r => r.ItemCode),
        ReportColumn.Of<RestaurantItemSalesRow, string>("itemName", ColumnDataType.Text, r => r.ItemName),
        ReportColumn.Of<RestaurantItemSalesRow, string>("category", ColumnDataType.Text, r => r.Category, groupable: true),
        ReportColumn.Of<RestaurantItemSalesRow, decimal>("quantity", ColumnDataType.Quantity, r => r.Quantity, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantItemSalesRow, decimal>("netSales", ColumnDataType.Money, r => r.NetSales, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantItemSalesRow, decimal>("tax", ColumnDataType.Money, r => r.Tax, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantItemSalesRow, decimal>("total", ColumnDataType.Money, r => r.Total, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantItemSalesRow, decimal?>("averagePrice", ColumnDataType.Money, r => r.AveragePrice),
        ReportColumn.Of<RestaurantItemSalesRow, long>("itemId", ColumnDataType.Number, r => r.ItemId, filterable: false),
    ];

    protected override IQueryable<RestaurantItemSalesRow> Build(ReportParameters parameters, ReportingDbContext db) =>
        RestaurantReports.ItemSales(db.Invoices, db.InvoiceDetails, db.Items, db.ItemCategories, From(parameters), To(parameters));

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<RestaurantItemSalesRow, long>>)(r => r.ItemId);
}

public sealed class RestaurantCategorySalesSource : RestaurantReportSource<RestaurantCategorySalesRow>
{
    public override string ReportKey => "restaurant-category-sales";
    public override string Title => "Restaurant Category Sales";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<RestaurantCategorySalesRow, string>("category", ColumnDataType.Text, r => r.Category),
        ReportColumn.Of<RestaurantCategorySalesRow, decimal>("quantity", ColumnDataType.Quantity, r => r.Quantity, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantCategorySalesRow, decimal>("netSales", ColumnDataType.Money, r => r.NetSales, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantCategorySalesRow, decimal>("tax", ColumnDataType.Money, r => r.Tax, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantCategorySalesRow, decimal>("total", ColumnDataType.Money, r => r.Total, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantCategorySalesRow, long>("categoryKey", ColumnDataType.Number, r => r.CategoryKey, filterable: false),
    ];

    protected override IQueryable<RestaurantCategorySalesRow> Build(ReportParameters parameters, ReportingDbContext db) =>
        RestaurantReports.CategorySales(db.Invoices, db.InvoiceDetails, db.Items, db.ItemCategories, From(parameters), To(parameters));

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<RestaurantCategorySalesRow, long>>)(r => r.CategoryKey);
}

public sealed class RestaurantWaiterSalesSource : RestaurantReportSource<RestaurantWaiterSalesRow>
{
    private readonly BatchedNameResolverRepository _resolver;

    public RestaurantWaiterSalesSource(BatchedNameResolverRepository resolver) => _resolver = resolver;

    public override string ReportKey => "restaurant-waiter-sales";
    public override string Title => "Restaurant Waiter-wise Sales";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<RestaurantWaiterSalesRow, string?>("waiterName", ColumnDataType.Text, r => r.WaiterName, sortable: false, filterable: false),
        ReportColumn.Of<RestaurantWaiterSalesRow, int>("orders", ColumnDataType.Quantity, r => r.Orders, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantWaiterSalesRow, decimal>("covers", ColumnDataType.Quantity, r => r.Covers, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantWaiterSalesRow, decimal>("dishes", ColumnDataType.Quantity, r => r.Dishes, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantWaiterSalesRow, decimal>("sales", ColumnDataType.Money, r => r.Sales, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantWaiterSalesRow, decimal?>("averagePerCover", ColumnDataType.Money, r => r.AveragePerCover),
        ReportColumn.Of<RestaurantWaiterSalesRow, decimal>("cancelledDishes", ColumnDataType.Quantity, r => r.CancelledDishes, aggregate: AggregateFunction.Sum),
    ];

    protected override IQueryable<RestaurantWaiterSalesRow> Build(ReportParameters parameters, ReportingDbContext db) =>
        RestaurantReports.WaiterSales(db.Invoices, db.RestaurantBills, db.RestaurantOrders, db.RestaurantOrderLines, From(parameters), To(parameters));

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<RestaurantWaiterSalesRow, string>>)(r => r.WaiterKey);

    protected override async Task FormatRowsAsync(IReadOnlyList<RestaurantWaiterSalesRow> page, CancellationToken ct)
    {
        Dictionary<Guid, string> names = await _resolver.GetUserNamesAsync(page.Select(r => r.WaiterUserId), ct);

        foreach (RestaurantWaiterSalesRow row in page)
        {
            row.WaiterName = row.WaiterUserId is Guid id ? names.GetValueOrDefault(id) : "No waiter";
        }
    }
}

public sealed class RestaurantTableTurnoverSource : RestaurantReportSource<RestaurantTableTurnoverRow>
{
    public override string ReportKey => "restaurant-table-turnover";
    public override string Title => "Restaurant Table Turnover";
    public override RestaurantFeature? RequiredRestaurantFeature => RestaurantFeature.Tables;

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<RestaurantTableTurnoverRow, string>("floor", ColumnDataType.Text, r => r.Floor, groupable: true),
        ReportColumn.Of<RestaurantTableTurnoverRow, string>("table", ColumnDataType.Text, r => r.Table),
        ReportColumn.Of<RestaurantTableTurnoverRow, int>("seats", ColumnDataType.Quantity, r => r.Seats, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantTableTurnoverRow, int>("turns", ColumnDataType.Quantity, r => r.Turns, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantTableTurnoverRow, decimal>("covers", ColumnDataType.Quantity, r => r.Covers, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantTableTurnoverRow, decimal?>("averageCovers", ColumnDataType.Number, r => r.AverageCovers),
        ReportColumn.Of<RestaurantTableTurnoverRow, decimal?>("seatUsePercent", ColumnDataType.Percent, r => r.SeatUsePercent),
        ReportColumn.Of<RestaurantTableTurnoverRow, decimal>("sales", ColumnDataType.Money, r => r.Sales, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantTableTurnoverRow, decimal?>("salesPerTurn", ColumnDataType.Money, r => r.SalesPerTurn),
        ReportColumn.Of<RestaurantTableTurnoverRow, long>("restaurantTableId", ColumnDataType.Number, r => r.RestaurantTableId, filterable: false),
    ];

    protected override IQueryable<RestaurantTableTurnoverRow> Build(ReportParameters parameters, ReportingDbContext db) =>
        RestaurantReports.TableTurnover(db.Floors, db.RestaurantTables, db.RestaurantOrders, db.RestaurantOrderLines, From(parameters), To(parameters));

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<RestaurantTableTurnoverRow, long>>)(r => r.RestaurantTableId);
}

public sealed class RestaurantFoodCostSource : RestaurantReportSource<RestaurantFoodCostRow>
{
    public override string ReportKey => "restaurant-food-cost";
    public override string Title => "Restaurant Food Cost";
    public override RestaurantFeature? RequiredRestaurantFeature => RestaurantFeature.Recipes;

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<RestaurantFoodCostRow, DateOnly>("date", ColumnDataType.Date, r => r.Date),
        ReportColumn.Of<RestaurantFoodCostRow, decimal>("foodSales", ColumnDataType.Money, r => r.FoodSales, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantFoodCostRow, decimal>("ingredientCost", ColumnDataType.Money, r => r.IngredientCost, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantFoodCostRow, decimal>("wastageCost", ColumnDataType.Money, r => r.WastageCost, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantFoodCostRow, decimal>("foodCost", ColumnDataType.Money, r => r.FoodCost, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantFoodCostRow, decimal>("grossMargin", ColumnDataType.Money, r => r.GrossMargin, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantFoodCostRow, decimal?>("foodCostPercent", ColumnDataType.Percent, r => r.FoodCostPercent),
        ReportColumn.Of<RestaurantFoodCostRow, int>("uncosted", ColumnDataType.Number, r => r.Uncosted),
    ];

    protected override IQueryable<RestaurantFoodCostRow> Build(ReportParameters parameters, ReportingDbContext db) =>
        RestaurantReports.FoodCost(db.Invoices, db.InvoiceDetails, db.StockMovements, db.WastageEntries, From(parameters), To(parameters));

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<RestaurantFoodCostRow, DateOnly>>)(r => r.Date);
}

public sealed class RestaurantWastageSource : RestaurantReportSource<RestaurantWastageRow>
{
    public override string ReportKey => "restaurant-wastage";
    public override string Title => "Restaurant Wastage";
    public override RestaurantFeature? RequiredRestaurantFeature => RestaurantFeature.Recipes;

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<RestaurantWastageRow, DateOnly>("date", ColumnDataType.Date, r => r.Date),
        ReportColumn.Of<RestaurantWastageRow, string>("itemCode", ColumnDataType.Text, r => r.ItemCode),
        ReportColumn.Of<RestaurantWastageRow, string>("itemName", ColumnDataType.Text, r => r.ItemName, groupable: true),
        ReportColumn.Of<RestaurantWastageRow, string>("category", ColumnDataType.Text, r => r.Category, groupable: true),
        ReportColumn.Of<RestaurantWastageRow, decimal>("quantity", ColumnDataType.Quantity, r => r.Quantity),
        ReportColumn.Of<RestaurantWastageRow, string?>("unit", ColumnDataType.Text, r => r.Unit),
        ReportColumn.Of<RestaurantWastageRow, string>("reason", ColumnDataType.Text, r => r.Reason, groupable: true),
        ReportColumn.Of<RestaurantWastageRow, string?>("note", ColumnDataType.Text, r => r.Note),
        ReportColumn.Of<RestaurantWastageRow, decimal>("cost", ColumnDataType.Money, r => r.Cost, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantWastageRow, bool>("isCosted", ColumnDataType.Boolean, r => r.IsCosted),
        ReportColumn.Of<RestaurantWastageRow, long>("wastageEntryId", ColumnDataType.Number, r => r.WastageEntryId, filterable: false),
    ];

    protected override IQueryable<RestaurantWastageRow> Build(ReportParameters parameters, ReportingDbContext db) =>
        RestaurantReports.Wastage(db.WastageEntries, db.Items, db.ItemCategories, db.UnitsOfMeasure, db.StockMovements, From(parameters), To(parameters));

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<RestaurantWastageRow, long>>)(r => r.WastageEntryId);
}

public sealed class RestaurantKotCancellationsSource : RestaurantReportSource<RestaurantKotCancellationRow>
{
    private readonly BatchedNameResolverRepository _resolver;

    public RestaurantKotCancellationsSource(BatchedNameResolverRepository resolver) => _resolver = resolver;

    public override string ReportKey => "restaurant-kot-cancellations";
    public override string Title => "Restaurant KOT Cancellations";
    public override RestaurantFeature? RequiredRestaurantFeature => RestaurantFeature.Kitchen;

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<RestaurantKotCancellationRow, DateOnly>("date", ColumnDataType.Date, r => r.Date),
        ReportColumn.Of<RestaurantKotCancellationRow, DateTime>("cancelledAt", ColumnDataType.DateTime, r => r.CancelledAt),
        ReportColumn.Of<RestaurantKotCancellationRow, string>("kotNo", ColumnDataType.Text, r => r.KotNo),
        ReportColumn.Of<RestaurantKotCancellationRow, string>("orderNo", ColumnDataType.Text, r => r.OrderNo),
        ReportColumn.Of<RestaurantKotCancellationRow, string>("table", ColumnDataType.Text, r => r.Table, groupable: true),
        ReportColumn.Of<RestaurantKotCancellationRow, string>("station", ColumnDataType.Text, r => r.Station, groupable: true),
        ReportColumn.Of<RestaurantKotCancellationRow, string>("dish", ColumnDataType.Text, r => r.Dish, groupable: true),
        ReportColumn.Of<RestaurantKotCancellationRow, decimal>("quantity", ColumnDataType.Quantity, r => r.Quantity, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantKotCancellationRow, decimal>("value", ColumnDataType.Money, r => r.Value, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<RestaurantKotCancellationRow, string?>("reason", ColumnDataType.Text, r => r.Reason),
        ReportColumn.Of<RestaurantKotCancellationRow, string?>("waiterName", ColumnDataType.Text, r => r.WaiterName, sortable: false, filterable: false),
        ReportColumn.Of<RestaurantKotCancellationRow, string?>("cancelledBy", ColumnDataType.Text, r => r.CancelledBy, sortable: false, filterable: false),
        ReportColumn.Of<RestaurantKotCancellationRow, long>("kotLineId", ColumnDataType.Number, r => r.KotLineId, filterable: false),
    ];

    protected override IQueryable<RestaurantKotCancellationRow> Build(ReportParameters parameters, ReportingDbContext db) =>
        RestaurantReports.KotCancellations(db.Kots, db.KotLines, db.RestaurantOrders, db.RestaurantOrderLines, db.RestaurantTables, From(parameters), To(parameters));

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<RestaurantKotCancellationRow, long>>)(r => r.KotLineId);

    protected override async Task FormatRowsAsync(IReadOnlyList<RestaurantKotCancellationRow> page, CancellationToken ct)
    {
        Dictionary<Guid, string> names = await _resolver.GetUserNamesAsync(
            page.Select(r => r.WaiterUserId).Concat(page.Select(r => r.CancelledByUserId)), ct);

        foreach (RestaurantKotCancellationRow row in page)
        {
            row.WaiterName = row.WaiterUserId is Guid waiter ? names.GetValueOrDefault(waiter) : null;
            row.CancelledBy = row.CancelledByUserId is Guid by ? names.GetValueOrDefault(by) : null;
        }
    }
}
