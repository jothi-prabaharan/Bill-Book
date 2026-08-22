using Microsoft.EntityFrameworkCore;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tax;
using Shared.Kernel.Tenancy;

namespace Sales.Api.Services;

/// <summary>
/// Sales orders — <c>SOR</c>. A commitment, not a posting.
///
/// <b>Nothing here reaches the general ledger.</b> An order promises goods; it
/// does not sell them. What it does do, and what nothing before it in the sales
/// flow does, is <b>reserve stock</b>: confirming holds the quantity so the same
/// units cannot be promised to somebody else, and cancelling or closing short
/// gives back whatever is still held.
///
/// <b>The reservation lives in Inventory, not here.</b> There is no reservation
/// table in <c>sal</c>: <c>inv.ItemStock.QuantityReserved</c> is the one counter,
/// already guarded and already read by the issue path, and a second counter in
/// another service would be a second answer to "what is available". What this
/// service keeps is <see cref="SalesOrderDetail.ReservedQuantity"/> per line —
/// not a second source of truth but the record of what <i>this</i> order is
/// holding, which is what makes a release able to give back exactly its own.
/// </summary>
public sealed class SalesOrderService
{
    private const string TypeCode = "SOR";

    private readonly SalesDbContext _db;
    private readonly IInventoryStock _stock;
    private readonly ITaxRateProvider _rates;
    private readonly INumberGenerator _numbers;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;
    private readonly ILogger<SalesOrderService> _log;

    public SalesOrderService(
        SalesDbContext db,
        IInventoryStock stock,
        ITaxRateProvider rates,
        INumberGenerator numbers,
        ITenantContext tenant,
        ICurrentUser user,
        TimeProvider clock,
        ILogger<SalesOrderService> log)
    {
        _db = db;
        _stock = stock;
        _rates = rates;
        _numbers = numbers;
        _tenant = tenant;
        _user = user;
        _clock = clock;
        _log = log;
    }

    public async Task<SalesOrderPage> ListAsync(
        int page, int pageSize, DateOnly? from, DateOnly? to, string? status, CancellationToken ct)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

        IQueryable<SalesOrder> query = _db.SalesOrders;

        if (from is DateOnly start)
        {
            query = query.Where(o => o.DocumentDate >= start);
        }

        if (to is DateOnly end)
        {
            query = query.Where(o => o.DocumentDate <= end);
        }

        if (Enum.TryParse(status, ignoreCase: true, out DocumentStatus wanted))
        {
            query = query.Where(o => o.Status == wanted);
        }

        int total = await query.CountAsync(ct);

        List<SalesOrderListItem> items = await query
            .OrderByDescending(o => o.DocumentDate)
            .ThenByDescending(o => o.SalesOrderId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new SalesOrderListItem
            {
                SalesOrderId = o.SalesOrderId,
                DocumentNo = o.DocumentNo,
                DocumentDate = o.DocumentDate,
                DeliveryDate = o.DeliveryDate,
                ContactId = o.ContactId,
                Status = o.Status.ToString(),
                FulfilmentStatus = o.FulfilmentStatus.ToString(),
                TotalAmount = o.TotalAmount,
                CurrencyCode = o.CurrencyCode,
                QuoteId = o.QuoteId,
                LineCount = _db.SalesOrderDetails.Count(l => l.SalesOrderId == o.SalesOrderId),
                ReservedQuantity = _db.SalesOrderDetails
                    .Where(l => l.SalesOrderId == o.SalesOrderId)
                    .Sum(l => (decimal?)l.ReservedQuantity) ?? 0m,
            })
            .ToListAsync(ct);

        return new SalesOrderPage
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
        };
    }

    public async Task<SalesOrderDetailModel?> GetAsync(long id, CancellationToken ct)
    {
        SalesOrderDetailModel? order = await _db.SalesOrders
            .Where(o => o.SalesOrderId == id)
            .Select(o => new SalesOrderDetailModel
            {
                SalesOrderId = o.SalesOrderId,
                DocumentNo = o.DocumentNo,
                DocumentDate = o.DocumentDate,
                DeliveryDate = o.DeliveryDate,
                ContactId = o.ContactId,
                ContactGstin = o.ContactGstin,
                BillingAddress = o.BillingAddress,
                ShippingAddress = o.ShippingAddress,
                PlaceOfSupplyStateId = o.PlaceOfSupplyStateId,
                IsInterState = o.IsInterState,
                Status = o.Status.ToString(),
                FulfilmentStatus = o.FulfilmentStatus.ToString(),
                CurrencyCode = o.CurrencyCode,
                ExchangeRate = o.ExchangeRate,
                SubTotal = o.SubTotal,
                DiscountAmount = o.DiscountAmount,
                TaxableAmount = o.TaxableAmount,
                CgstAmount = o.CgstAmount,
                SgstAmount = o.SgstAmount,
                IgstAmount = o.IgstAmount,
                CessAmount = o.CessAmount,
                RoundOffAmount = o.RoundOffAmount,
                TotalAmount = o.TotalAmount,
                QuoteId = o.QuoteId,
                Notes = o.Notes,
                TermsAndConditions = o.TermsAndConditions,
                VoidReason = o.VoidReason,
            })
            .FirstOrDefaultAsync(ct);

        if (order is null)
        {
            return null;
        }

        order.Lines = await _db.SalesOrderDetails
            .Where(l => l.SalesOrderId == id)
            .OrderBy(l => l.LineNumber)
            .Select(l => new SalesOrderLineModel
            {
                SalesOrderDetailId = l.SalesOrderDetailId,
                LineNumber = l.LineNumber,
                ItemId = l.ItemId,
                Description = l.Description,
                HsnSacCode = l.HsnSacCode,
                WarehouseId = l.WarehouseId,
                Quantity = l.Quantity,
                UomId = l.UomId,
                UnitPrice = l.UnitPrice,
                DiscountAmount = l.DiscountAmount,
                TaxableAmount = l.TaxableAmount,
                TaxAmount = l.TaxAmount,
                LineTotal = l.LineTotal,
                ReservedQuantity = l.ReservedQuantity,
                DeliveredQuantity = l.DeliveredQuantity,
                LineNotes = l.LineNotes,
            })
            .ToListAsync(ct);

        order.LineCount = order.Lines.Count;
        order.ReservedQuantity = order.Lines.Sum(l => l.ReservedQuantity);
        return order;
    }

    /// <summary>
    /// Creates or replaces a draft, recomputing every line's tax from the rates
    /// in force on the document date.
    ///
    /// <b>The tax is never taken from the caller.</b> A browser that computed its
    /// own totals would be a second implementation of the GST rules, free to
    /// disagree with the one that files the return.
    /// </summary>
    public async Task<SalesOrderResult> SaveAsync(
        long? id, SaveSalesOrderRequest request, CancellationToken ct)
    {
        if (request.Lines.Count == 0)
        {
            return new SalesOrderResult(
                SalesOrderOutcome.NoLines, id, "An order needs at least one line.");
        }

        DateOnly documentDate = request.DocumentDate
            ?? DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        SalesOrder? order;

        if (id is long existingId)
        {
            order = await _db.SalesOrders.FirstOrDefaultAsync(o => o.SalesOrderId == existingId, ct);

            if (order is null)
            {
                return new SalesOrderResult(SalesOrderOutcome.NotFound);
            }

            if (order.Status != DocumentStatus.Draft)
            {
                return new SalesOrderResult(
                    SalesOrderOutcome.NotDraft, existingId,
                    "The order is confirmed and holding stock. Cancel it, or raise an amendment.");
            }

            await _db.SalesOrderDetails
                .Where(l => l.SalesOrderId == existingId)
                .ExecuteDeleteAsync(ct);
        }
        else
        {
            // The number is taken now, at creation, not at confirm. That is the
            // rule T0.3 settled and the unique index enforces it — there is no
            // filter on it, so two unnumbered drafts would collide.
            //
            // Both ways keep the series gapless; the difference is whether a gap
            // is prevented or explained, and explained was the choice. The
            // consequence is not optional: a number issued has been spent, so an
            // abandoned draft is cancelled and keeps its number rather than being
            // deleted.
            NumberAllocation created;

            try
            {
                created = await _numbers.NextAsync(TypeCode, documentDate, ct);
            }
            catch (InvalidOperationException ex)
            {
                return new SalesOrderResult(
                    SalesOrderOutcome.SeriesMissing, null,
                    "No SOR numbering series exists for this branch, so the order could not be "
                        + $"numbered and nothing was saved. Re-run the branch seed. ({ex.Message})");
            }

            order = new SalesOrder
            {
                TransactionTypeCode = TypeCode,
                DocumentNo = created.Code,
            };

            _db.SalesOrders.Add(order);
        }

        IReadOnlyDictionary<long, TaxRate>? rates = await _rates.GetRatesAsync(documentDate, ct);

        if (rates is null)
        {
            // Null is "could not ask", which is not the same as "no rates" and
            // must not be treated as zero tax.
            return new SalesOrderResult(
                SalesOrderOutcome.InventoryUnreachable, id,
                "The tax rates could not be read, so nothing was saved. Try again shortly.");
        }

        order.ContactId = request.ContactId;
        order.DocumentDate = documentDate;
        order.DeliveryDate = request.DeliveryDate;
        order.QuoteId = request.QuoteId;
        order.CurrencyCode = request.CurrencyCode ?? "INR";
        order.ExchangeRate = request.ExchangeRate ?? 1m;
        order.PlaceOfSupplyStateId = request.PlaceOfSupplyStateId;
        order.Notes = request.Notes;
        order.TermsAndConditions = request.TermsAndConditions;

        await _db.SaveChangesAsync(ct);

        var context = new TaxContext(order.IsInterState, DiscountBeforeTax: true);
        var results = new List<TaxLineResult>();
        int lineNumber = 0;

        foreach (SaveSalesOrderLineRequest line in request.Lines)
        {
            TaxRate? rate = line.TaxMasterId is long groupId && rates.TryGetValue(groupId, out TaxRate? found)
                ? found
                : null;

            TaxLineResult computed = GstCalculator.Compute(
                new TaxLineInput
                {
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountPercent = line.DiscountPercent,
                    Rate = rate,
                    TaxTreatment = rate is null ? TaxTreatment.Exempt : TaxTreatment.Taxable,
                },
                context);

            results.Add(computed);

            _db.SalesOrderDetails.Add(new SalesOrderDetail
            {
                SalesOrderId = order.SalesOrderId,
                LineNumber = ++lineNumber,
                ItemId = line.ItemId,
                Description = line.Description,
                WarehouseId = line.WarehouseId,
                Quantity = line.Quantity,
                UomId = line.UomId,
                BaseQuantity = computed.BaseQuantity,
                UnitPrice = line.UnitPrice,
                DiscountPercent = line.DiscountPercent,
                DiscountAmount = computed.DiscountAmount,
                GrossAmount = computed.GrossAmount,
                TaxableAmount = computed.TaxableAmount,
                TaxAmount = computed.TaxAmount,
                LineTotal = computed.LineTotal,
                TaxGroupId = line.TaxMasterId,
                TaxMasterId = rate?.TaxMasterId,
                LineNotes = line.LineNotes,
            });
        }

        TaxDocumentTotals totals = GstCalculator.Totals(results);

        order.SubTotal = totals.SubTotal;
        order.DiscountAmount = totals.DiscountAmount;
        order.TaxableAmount = totals.TaxableAmount;
        order.CgstAmount = totals.CgstAmount;
        order.SgstAmount = totals.SgstAmount;
        order.IgstAmount = totals.IgstAmount;
        order.CessAmount = totals.CessAmount;
        order.TotalAmount = totals.TaxableAmount + totals.CgstAmount + totals.SgstAmount
            + totals.IgstAmount + totals.CessAmount;
        order.TotalAmountBase = order.ExchangeRate == 1m
            ? order.TotalAmount
            : Math.Round(order.TotalAmount / order.ExchangeRate, 2, MidpointRounding.AwayFromZero);

        await _db.SaveChangesAsync(ct);
        return new SalesOrderResult(SalesOrderOutcome.Ok, order.SalesOrderId);
    }

    /// <summary>
    /// Confirms the order: takes its number and holds the stock.
    ///
    /// <b>The reservation is the last thing done and the first thing undone.</b>
    /// The number and the status change go in inside a transaction that is not
    /// committed until Inventory has said yes, so a shortage leaves a draft with
    /// no number spent. If the commit itself then fails, the stock is released
    /// again — the one ordering that never leaves stock held by an order that
    /// does not exist.
    /// </summary>
    public async Task<SalesOrderResult> ConfirmAsync(long id, CancellationToken ct)
    {
        SalesOrder? order = await _db.SalesOrders.FirstOrDefaultAsync(o => o.SalesOrderId == id, ct);

        if (order is null)
        {
            return new SalesOrderResult(SalesOrderOutcome.NotFound);
        }

        if (order.Status != DocumentStatus.Draft && order.Status != DocumentStatus.ReadyToPost)
        {
            return new SalesOrderResult(
                SalesOrderOutcome.NotDraft, id, "The order has already been confirmed.");
        }

        List<SalesOrderDetail> lines = await _db.SalesOrderDetails
            .Where(l => l.SalesOrderId == id)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        if (lines.Count == 0)
        {
            return new SalesOrderResult(SalesOrderOutcome.NoLines, id, "The order has no lines.");
        }

        if (_tenant.CustomerId is not Guid customerId || _tenant.OrgId is not Guid orgId)
        {
            return new SalesOrderResult(SalesOrderOutcome.InvalidValue, id, "No branch in context.");
        }

        // Only stock lines reserve. A service or a description-only line has no
        // quantity to hold, and asking Inventory about it would be asking about
        // an item that does not exist.
        IReadOnlyList<StockLine> claims =
        [
            .. lines
                .Where(l => l.ItemId is not null && l.LineType == DocumentLineType.Stock)
                .Select(l => new StockLine(l.LineNumber, l.ItemId!.Value, l.BaseQuantity)),
        ];

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // The number was taken when the order was created, so confirming only
        // changes what the document means and what it holds.
        order.Status = DocumentStatus.Posted;
        order.FulfilmentStatus = FulfilmentStatus.Open;
        order.PostedAt = _clock.GetUtcNow();
        order.PostedBy = _user.UserId;

        foreach (SalesOrderDetail line in lines)
        {
            line.ReservedQuantity = claims.Any(c => c.LineNumber == line.LineNumber)
                ? line.BaseQuantity
                : 0m;
        }

        await _db.SaveChangesAsync(ct);

        if (claims.Count > 0)
        {
            StockReservationResult reserved =
                await _stock.ReserveAsync(customerId, orgId, id, claims, ct);

            if (!reserved.Ok)
            {
                await tx.RollbackAsync(ct);

                // The database is back where it started; the tracked entities
                // are not. Without this they keep the number and the Posted
                // status the rolled-back attempt gave them, and anything that
                // read the order later in the same request would see a confirm
                // that never happened.
                await ReloadAsync(order, lines, ct);

                return reserved.Unreachable
                    ? new SalesOrderResult(
                        SalesOrderOutcome.InventoryUnreachable, id,
                        "Inventory could not be reached, so no stock was held and the order is "
                            + "still a draft. Nothing was lost — try again.")
                    : new SalesOrderResult(
                        SalesOrderOutcome.InsufficientStock, id,
                        "There is not enough unreserved stock to confirm this order.",
                        [.. reserved.Shortages.Select(s => new SalesOrderShortage(
                            s.LineNumber, s.ItemId, s.ItemCode, s.ItemName, s.Requested, s.Available))]);
            }
        }

        try
        {
            await tx.CommitAsync(ct);
        }
        catch
        {
            // The stock is held and the order is not. Give it back rather than
            // leave a reservation nothing can ever release.
            if (claims.Count > 0)
            {
                await _stock.ReleaseAsync(customerId, orgId, id, claims, CancellationToken.None);
            }

            throw;
        }

        return new SalesOrderResult(SalesOrderOutcome.Ok, id);
    }

    /// <summary>
    /// The customer withdrew. Everything still held goes back, and the order
    /// keeps its number — a document series with a hole in it is what an auditor
    /// asks about.
    /// </summary>
    public Task<SalesOrderResult> CancelAsync(
        long id, CloseSalesOrderRequest request, CancellationToken ct) =>
        ReleaseAndCloseAsync(id, request.Reason, FulfilmentStatus.Cancelled, ct);

    /// <summary>
    /// Nothing further is coming, by agreement. What has shipped has shipped;
    /// what has not stops being a promise and is released.
    /// </summary>
    public Task<SalesOrderResult> ShortCloseAsync(
        long id, CloseSalesOrderRequest request, CancellationToken ct) =>
        ReleaseAndCloseAsync(id, request.Reason, FulfilmentStatus.Closed, ct);

    private async Task<SalesOrderResult> ReleaseAndCloseAsync(
        long id, string reason, FulfilmentStatus closeAs, CancellationToken ct)
    {
        SalesOrder? order = await _db.SalesOrders.FirstOrDefaultAsync(o => o.SalesOrderId == id, ct);

        if (order is null)
        {
            return new SalesOrderResult(SalesOrderOutcome.NotFound);
        }

        // Cancelling covers an abandoned draft as well as a confirmed order
        // taken back out — PostedAt being null is what tells the two apart, and
        // a draft simply has nothing to release. A short close does not: there
        // is nothing to close short of on an order that never confirmed.
        bool draft = order.Status == DocumentStatus.Draft
            || order.Status == DocumentStatus.ReadyToPost;

        if (order.Status != DocumentStatus.Posted
            && !(draft && closeAs == FulfilmentStatus.Cancelled))
        {
            return new SalesOrderResult(
                SalesOrderOutcome.NotConfirmed, id,
                order.Status == DocumentStatus.Void
                    ? "The order has already been cancelled."
                    : "Only a confirmed order can be closed short.");
        }

        if (order.FulfilmentStatus is FulfilmentStatus.Cancelled or FulfilmentStatus.Closed)
        {
            return new SalesOrderResult(
                SalesOrderOutcome.NotConfirmed, id, "The order is already closed.");
        }

        if (_tenant.CustomerId is not Guid customerId || _tenant.OrgId is not Guid orgId)
        {
            return new SalesOrderResult(SalesOrderOutcome.InvalidValue, id, "No branch in context.");
        }

        List<SalesOrderDetail> lines = await _db.SalesOrderDetails
            .Where(l => l.SalesOrderId == id && l.ReservedQuantity > 0)
            .ToListAsync(ct);

        // Only what is still held. A line already delivered released its share
        // when it shipped, and releasing it twice would free stock nobody holds.
        IReadOnlyList<StockLine> claims =
        [
            .. lines
                .Where(l => l.ItemId is not null)
                .Select(l => new StockLine(l.LineNumber, l.ItemId!.Value, l.ReservedQuantity)),
        ];

        if (claims.Count > 0)
        {
            StockReservationResult released =
                await _stock.ReleaseAsync(customerId, orgId, id, claims, ct);

            if (released.Unreachable)
            {
                return new SalesOrderResult(
                    SalesOrderOutcome.InventoryUnreachable, id,
                    "Inventory could not be reached, so the order is unchanged and its stock is "
                        + "still held. Try again shortly.");
            }

            if (!released.Ok)
            {
                // Reported, not refused: the release found less held than this
                // order believed, which is worth a log and a look, but refusing
                // the cancellation would leave the customer's withdrawn order open.
                _log.LogWarning(
                    "Releasing order {SalesOrderId}: {Failed} line(s) held less than recorded.",
                    id,
                    released.Shortages.Count);
            }
        }

        foreach (SalesOrderDetail line in lines)
        {
            line.ReservedQuantity = 0m;
        }

        order.FulfilmentStatus = closeAs;

        if (closeAs == FulfilmentStatus.Cancelled)
        {
            // Cancelling withdraws the document: the customer pulled out and
            // nothing about it stands. That is what Void means, and the schema
            // enforces the pairing — a Void status with no reason, or a reason
            // with no status, is refused.
            order.Status = DocumentStatus.Void;
            order.VoidReason = reason;
            order.VoidedAt = _clock.GetUtcNow();
            order.VoidedBy = _user.UserId;
        }
        else
        {
            // A short close is not a void, and stamping one would be a lie: half
            // of this order shipped, so the document is a true record of what was
            // ordered and what went out. It stays Posted and only stops expecting
            // more.
            //
            // The reason goes into Notes because the table has nowhere else for
            // it. A column of its own would be better and is a schema change to
            // a table five other documents share — worth doing deliberately
            // rather than as a side effect of this.
            string stamp = $"Closed short on "
                + $"{DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime):yyyy-MM-dd}: {reason}";

            order.Notes = string.IsNullOrWhiteSpace(order.Notes)
                ? stamp
                : $"{order.Notes}\n{stamp}";
        }

        await _db.SaveChangesAsync(ct);
        return new SalesOrderResult(SalesOrderOutcome.Ok, id);
    }

    /// <summary>
    /// Puts the tracked entities back in step with the database after a rollback.
    /// EF does not undo what it wrote to the objects, only what it wrote to the
    /// rows.
    /// </summary>
    private async Task ReloadAsync(
        SalesOrder order, List<SalesOrderDetail> lines, CancellationToken ct)
    {
        await _db.Entry(order).ReloadAsync(ct);

        foreach (SalesOrderDetail line in lines)
        {
            await _db.Entry(line).ReloadAsync(ct);
        }
    }
}
