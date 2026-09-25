using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Employees;
using Shared.Kernel.Numbering;
using Shared.Kernel.School;
using Shared.Kernel.Stock;
using WorkOrder.Entity.Enums;
using WorkOrder.Entity.Models;
using WorkOrder.Entity.TableEntities;
using WorkOrder.Repository;
using WorkOrder.Repository.SeedData;

namespace WorkOrder.Api.Services;

public enum WorkOrderOutcome
{
    Ok = 1,
    NotFound = 2,
    Invalid = 3,

    /// <summary>The lifecycle forbids it: an edit after Open, a move from the wrong status.</summary>
    StateRule = 4,

    /// <summary>Inventory refused the issue, usually for want of stock.</summary>
    StockRefused = 5,

    /// <summary>Facility, Hrm or Inventory could not be reached.</summary>
    Unavailable = 6,
}

public sealed record WorkOrderResult(WorkOrderOutcome Outcome, long? Id = null, string? Detail = null, object? Body = null)
{
    public static WorkOrderResult Ok(long id, object? body = null) => new(WorkOrderOutcome.Ok, id, null, body);

    public static WorkOrderResult Fail(WorkOrderOutcome outcome, string? detail = null) => new(outcome, null, detail);
}

/// <summary>
/// Work orders (S6, TK-66). Where the job is — an asset or a space — is
/// checked through Facility, the assignee through Hrm, and parts are issued
/// through Inventory's guarded decrement, keyed on the work order and part so
/// the same part is never issued twice.
/// </summary>
public sealed class WorkOrderService
{
    public const string TypeCode = "WRK";

    private readonly WorkOrderDbContext _db;
    private readonly INumberGenerator _numbers;
    private readonly IFacilityClient _facility;
    private readonly IEmployeeDirectory _employees;
    private readonly IStockClient _stock;
    private readonly ILogger<WorkOrderService> _log;

    public WorkOrderService(
        WorkOrderDbContext db,
        INumberGenerator numbers,
        IFacilityClient facility,
        IEmployeeDirectory employees,
        IStockClient stock,
        ILogger<WorkOrderService> log)
    {
        _db = db;
        _numbers = numbers;
        _facility = facility;
        _employees = employees;
        _stock = stock;
        _log = log;
    }

    public async Task<List<WorkOrderView>> ListAsync(WorkOrderStatus? status, long? facilityAssetId, CancellationToken ct)
    {
        List<WorkOrderDocument> rows = await _db.WorkOrders.AsNoTracking().Include(w => w.Tasks).Include(w => w.Parts)
            .Where(w => (status == null || w.WorkOrderStatus == status) && (facilityAssetId == null || w.FacilityAssetId == facilityAssetId))
            .OrderByDescending(w => w.ReportedDate).ThenByDescending(w => w.WorkOrderId)
            .Take(1000)
            .ToListAsync(ct);
        return [.. rows.Select(View)];
    }

    public async Task<WorkOrderView?> GetAsync(long id, CancellationToken ct) =>
        await _db.WorkOrders.AsNoTracking().Include(w => w.Tasks).Include(w => w.Parts).FirstOrDefaultAsync(w => w.WorkOrderId == id, ct) is WorkOrderDocument w
            ? View(w)
            : null;

    public async Task<WorkOrderResult> SaveAsync(long? id, SaveWorkOrderRequest request, CancellationToken ct)
    {
        if (request.DueDate is DateOnly due && due < request.ReportedDate)
        {
            return WorkOrderResult.Fail(WorkOrderOutcome.Invalid, "The due date cannot be before the day it was reported.");
        }

        WorkOrderResult? where = await CheckWhereAsync(request.FacilityAssetId, request.SpaceId, ct);
        if (where is not null)
        {
            return where;
        }

        WorkOrderDocument? order;
        if (id is long existing)
        {
            order = await _db.WorkOrders.Include(w => w.Tasks).FirstOrDefaultAsync(w => w.WorkOrderId == existing, ct);
            if (order is null)
            {
                return WorkOrderResult.Fail(WorkOrderOutcome.NotFound);
            }

            if (!WorkOrderLifecycle.IsEditable(order.WorkOrderStatus))
            {
                return WorkOrderResult.Fail(WorkOrderOutcome.StateRule, WorkOrderLifecycle.NotEditable(order.WorkOrderStatus));
            }

            _db.WorkOrderTasks.RemoveRange(order.Tasks);
            await _db.SaveChangesAsync(ct);
            order.Tasks.Clear();
        }
        else
        {
            order = new WorkOrderDocument
            {
                WorkOrderNo = (await _numbers.NextAsync(WorkOrderSeed.SeriesCode, request.ReportedDate, ct)).Code,
            };
            _db.WorkOrders.Add(order);
        }

        order.Title = request.Title.Trim();
        order.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        order.WorkOrderSource = request.WorkOrderSource;
        order.Priority = request.Priority;
        order.FacilityAssetId = request.FacilityAssetId;
        order.SpaceId = request.SpaceId;
        order.ReportedDate = request.ReportedDate;
        order.DueDate = request.DueDate;

        int sort = 0;
        foreach (string task in request.Tasks.Where(t => !string.IsNullOrWhiteSpace(t)))
        {
            order.Tasks.Add(new WorkOrderTask { Description = task.Trim(), SortOrder = ++sort });
        }

        await _db.SaveChangesAsync(ct);
        return WorkOrderResult.Ok(order.WorkOrderId);
    }

    /// <summary>Moves a work order. Closing needs <c>workorder.close</c>, which the caller says it holds.</summary>
    public async Task<WorkOrderResult> ActAsync(long id, WorkOrderActionRequest request, bool mayClose, CancellationToken ct)
    {
        WorkOrderDocument? order = await _db.WorkOrders.FirstOrDefaultAsync(w => w.WorkOrderId == id, ct);
        if (order is null)
        {
            return WorkOrderResult.Fail(WorkOrderOutcome.NotFound);
        }

        if (WorkOrderLifecycle.Next(order.WorkOrderStatus, request.Action) is not WorkOrderStatus next)
        {
            return WorkOrderResult.Fail(WorkOrderOutcome.StateRule, WorkOrderLifecycle.Refusal(order.WorkOrderStatus, request.Action));
        }

        switch (request.Action)
        {
            case WorkOrderAction.Assign:
                if (request.EmployeeId is not long employeeId)
                {
                    return WorkOrderResult.Fail(WorkOrderOutcome.Invalid, "Choose who does the work.");
                }

                WorkOrderResult? assignee = await CheckEmployeeAsync(employeeId, ct);
                if (assignee is not null)
                {
                    return assignee;
                }

                order.AssignedEmployeeId = employeeId;
                break;

            case WorkOrderAction.Start when order.AssignedEmployeeId is null && order.AmcContractId is null:
                return WorkOrderResult.Fail(WorkOrderOutcome.Invalid, "Assign the work order before starting it.");

            case WorkOrderAction.Complete:
                DateOnly completed = request.CompletedDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
                if (completed < order.ReportedDate)
                {
                    return WorkOrderResult.Fail(WorkOrderOutcome.Invalid, "The work cannot be completed before it was reported.");
                }

                order.CompletedDate = completed;
                order.LabourCost = request.LabourCost ?? order.LabourCost;
                break;

            case WorkOrderAction.Close when !mayClose:
                return WorkOrderResult.Fail(WorkOrderOutcome.StateRule, "Closing a work order needs permission to close work orders.");

            case WorkOrderAction.Cancel:
                if (string.IsNullOrWhiteSpace(request.Reason))
                {
                    return WorkOrderResult.Fail(WorkOrderOutcome.Invalid, "Give a reason to cancel.");
                }

                if (await _db.WorkOrderParts.AnyAsync(p => p.WorkOrderId == id, ct))
                {
                    return WorkOrderResult.Fail(WorkOrderOutcome.StateRule, "A work order with parts issued cannot be cancelled. Complete it instead.");
                }

                order.CancelReason = request.Reason.Trim();
                break;
        }

        order.WorkOrderStatus = next;
        await _db.SaveChangesAsync(ct);
        return WorkOrderResult.Ok(order.WorkOrderId);
    }

    public async Task<WorkOrderResult> TickAsync(long id, long taskId, bool isDone, CancellationToken ct)
    {
        WorkOrderDocument? order = await _db.WorkOrders.AsNoTracking().FirstOrDefaultAsync(w => w.WorkOrderId == id, ct);
        WorkOrderTask? task = await _db.WorkOrderTasks.FirstOrDefaultAsync(t => t.WorkOrderTaskId == taskId && t.WorkOrderId == id, ct);
        if (order is null || task is null)
        {
            return WorkOrderResult.Fail(WorkOrderOutcome.NotFound);
        }

        if (!WorkOrderLifecycle.TakesTicks(order.WorkOrderStatus))
        {
            return WorkOrderResult.Fail(WorkOrderOutcome.StateRule, $"A work order that is {WorkOrderLifecycle.Words(order.WorkOrderStatus)} takes no more changes.");
        }

        task.IsDone = isDone;
        await _db.SaveChangesAsync(ct);
        return WorkOrderResult.Ok(taskId);
    }

    /// <summary>
    /// Issues a part from Inventory. The part row is saved first so its id keys
    /// the stock movement: Inventory refuses a second movement for the same
    /// work order and line, so a retried issue moves stock once.
    /// </summary>
    public async Task<WorkOrderResult> IssuePartAsync(long id, IssuePartRequest request, CancellationToken ct)
    {
        WorkOrderDocument? order = await _db.WorkOrders.AsNoTracking().FirstOrDefaultAsync(w => w.WorkOrderId == id, ct);
        if (order is null)
        {
            return WorkOrderResult.Fail(WorkOrderOutcome.NotFound);
        }

        if (!WorkOrderLifecycle.TakesParts(order.WorkOrderStatus))
        {
            return WorkOrderResult.Fail(WorkOrderOutcome.StateRule,
                $"Parts are issued while the work is under way, not when it is {WorkOrderLifecycle.Words(order.WorkOrderStatus)}.");
        }

        var part = new WorkOrderPart { WorkOrderId = id, ItemId = request.ItemId, WarehouseId = request.WarehouseId, Quantity = request.Quantity };
        _db.WorkOrderParts.Add(part);
        await _db.SaveChangesAsync(ct);

        StockIssueResponse issued;
        IReadOnlyList<StockItem> named;
        try
        {
            issued = await _stock.IssueAsync(new StockIssueRequest
            {
                MovementDate = request.IssueDate,
                SourceType = TypeCode,
                SourceId = id,
                Lines = [new StockIssueLine { SourceLineId = part.WorkOrderPartId, ItemId = request.ItemId, Quantity = request.Quantity, WarehouseId = request.WarehouseId }],
            }, ct);
            named = issued.Success ? await _stock.SearchAsync(null, ct) : [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogError(ex, "A part for work order {WorkOrderId} could not be issued.", id);
            return WorkOrderResult.Fail(WorkOrderOutcome.Unavailable);
        }

        if (!issued.Success || issued.Lines.FirstOrDefault() is not { Success: true } line)
        {
            // The request's transaction takes the part row back.
            string outcome = issued.Lines.FirstOrDefault()?.Outcome ?? "Refused";
            return WorkOrderResult.Fail(WorkOrderOutcome.StockRefused, outcome.Contains("Insufficient", StringComparison.OrdinalIgnoreCase)
                ? "There is not enough of that item in stock."
                : "Inventory refused the issue. Check the item and warehouse.");
        }

        part.UnitCost = line.UnitCost;
        part.ItemName = named.FirstOrDefault(i => i.ItemId == request.ItemId)?.ItemName;
        await _db.SaveChangesAsync(ct);
        return WorkOrderResult.Ok(part.WorkOrderPartId);
    }

    /// <summary>
    /// Raises a work order for another service (a plan's occurrence, an AMC
    /// visit), once: a second call with the same source key returns the first.
    /// </summary>
    public async Task<RaiseWorkOrderResponse> RaiseAsync(RaiseWorkOrderRequest request, CancellationToken ct)
    {
        if (await _db.WorkOrders.AsNoTracking().Where(w => w.SourceKey == request.SourceKey)
            .Select(w => new { w.WorkOrderId, w.WorkOrderNo }).FirstOrDefaultAsync(ct) is { } already)
        {
            return new RaiseWorkOrderResponse { WorkOrderId = already.WorkOrderId, WorkOrderNo = already.WorkOrderNo, Created = false };
        }

        var order = new WorkOrderDocument
        {
            WorkOrderNo = (await _numbers.NextAsync(WorkOrderSeed.SeriesCode, request.ReportedDate, ct)).Code,
            Title = request.Title.Trim(),
            WorkOrderSource = request.WorkOrderSource,
            FacilityAssetId = request.FacilityAssetId,
            SpaceId = request.SpaceId,
            ReportedDate = request.ReportedDate,
            DueDate = request.DueDate,
            PreventivePlanId = request.PreventivePlanId,
            AmcContractId = request.AmcContractId,
            SourceKey = request.SourceKey,
            AssignedEmployeeId = request.AssignedEmployeeId,
            WorkOrderStatus = request.AssignedEmployeeId is null ? WorkOrderStatus.Open : WorkOrderStatus.Assigned,
        };
        _db.WorkOrders.Add(order);
        await _db.SaveChangesAsync(ct);
        return new RaiseWorkOrderResponse { WorkOrderId = order.WorkOrderId, WorkOrderNo = order.WorkOrderNo, Created = true };
    }

    public Task<IReadOnlyList<StockItem>> StockItemsAsync(string? search, CancellationToken ct) => _stock.SearchAsync(search, ct);

    public Task<IReadOnlyList<StockWarehouse>> WarehousesAsync(CancellationToken ct) => _stock.WarehousesAsync(ct);

    // ---- Helpers ---------------------------------------------------------------

    private async Task<WorkOrderResult?> CheckWhereAsync(long? assetId, long? spaceId, CancellationToken ct)
    {
        if (assetId is null && spaceId is null)
        {
            return WorkOrderResult.Fail(WorkOrderOutcome.Invalid, "Say where the work is: an asset, a space, or both.");
        }

        FacilityLookupResponse found;
        try
        {
            found = await _facility.LookupAsync(assetId is long a ? [a] : [], spaceId is long s ? [s] : [], ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogError(ex, "The asset or space of a work order could not be checked.");
            return WorkOrderResult.Fail(WorkOrderOutcome.Unavailable);
        }

        bool assetOk = assetId is null || found.Assets.Any(x => x.Id == assetId && x.IsUsable);
        bool spaceOk = spaceId is null || found.Spaces.Any(x => x.Id == spaceId && x.IsUsable);
        return assetOk && spaceOk ? null : WorkOrderResult.Fail(WorkOrderOutcome.Invalid, "Choose an asset in use and an active space of this branch.");
    }

    private async Task<WorkOrderResult?> CheckEmployeeAsync(long employeeId, CancellationToken ct)
    {
        IReadOnlyDictionary<long, EmployeeProfile> found;
        try
        {
            found = await _employees.FindAsync([employeeId], ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogError(ex, "The assignee of a work order could not be checked.");
            return WorkOrderResult.Fail(WorkOrderOutcome.Unavailable);
        }

        return found.TryGetValue(employeeId, out EmployeeProfile? employee) && employee.EmployeeStatus != "Exited"
            ? null
            : WorkOrderResult.Fail(WorkOrderOutcome.Invalid, "Choose a current employee of this branch.");
    }

    private static WorkOrderView View(WorkOrderDocument w) => new()
    {
        WorkOrderId = w.WorkOrderId,
        WorkOrderNo = w.WorkOrderNo,
        Title = w.Title,
        Description = w.Description,
        WorkOrderSource = w.WorkOrderSource,
        Priority = w.Priority,
        FacilityAssetId = w.FacilityAssetId,
        SpaceId = w.SpaceId,
        ReportedDate = w.ReportedDate,
        DueDate = w.DueDate,
        AssignedEmployeeId = w.AssignedEmployeeId,
        AmcContractId = w.AmcContractId,
        PreventivePlanId = w.PreventivePlanId,
        WorkOrderStatus = w.WorkOrderStatus,
        CompletedDate = w.CompletedDate,
        LabourCost = w.LabourCost,
        PartsCost = w.Parts.Sum(p => p.Quantity * p.UnitCost),
        CancelReason = w.CancelReason,
        Tasks = [.. w.Tasks.OrderBy(t => t.SortOrder).Select(t => new WorkOrderTaskView { WorkOrderTaskId = t.WorkOrderTaskId, Description = t.Description, IsDone = t.IsDone })],
        Parts = [.. w.Parts.Select(p => new WorkOrderPartView
        {
            WorkOrderPartId = p.WorkOrderPartId, ItemId = p.ItemId, ItemName = p.ItemName, WarehouseId = p.WarehouseId, Quantity = p.Quantity, UnitCost = p.UnitCost,
        })],
    };
}
