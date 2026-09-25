using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Kernel.Employees;
using Shared.Kernel.Numbering;
using Shared.Kernel.School;
using Shared.Kernel.Stock;
using WorkOrder.Api.Services;
using WorkOrder.Entity.Enums;
using WorkOrder.Entity.Models;
using WorkOrder.Entity.TableEntities;
using WorkOrder.Repository;
using Xunit;

namespace WorkOrder.Api.Tests;

internal sealed class AprilYear : IFinancialYearProvider
{
    public Task<int> GetStartMonthAsync(CancellationToken ct = default) => Task.FromResult(4);
}

/// <summary>Facility with asset 7 in use, asset 8 disposed and space 3 active.</summary>
internal sealed class FakeFacility : IFacilityClient
{
    public Task<FacilityLookupResponse> LookupAsync(IEnumerable<long> assetIds, IEnumerable<long> spaceIds, CancellationToken ct) =>
        Task.FromResult(new FacilityLookupResponse
        {
            Assets = [.. assetIds.Where(id => id is 7 or 8).Select(id => new FacilityItem { Id = id, Code = $"A{id}", Name = "Asset", IsUsable = id == 7 })],
            Spaces = [.. spaceIds.Where(id => id == 3).Select(id => new FacilityItem { Id = id, Code = "S3", Name = "Space", IsUsable = true })],
        });
}

/// <summary>Employee with employee 21 active and 22 exited.</summary>
internal sealed class FakeEmployees : IEmployeeDirectory
{
    public Task<IReadOnlyDictionary<long, EmployeeProfile>> FindAsync(IEnumerable<long> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<long, EmployeeProfile>>(ids.Where(id => id is 21 or 22).ToDictionary(id => id, id => new EmployeeProfile
        {
            EmployeeId = id, EmployeeCode = $"E{id}", FullName = "Ravi", Gender = "Male", EmployeeStatus = id == 21 ? "Active" : "Exited",
        }));
}

/// <summary>Inventory, keyed on source and line like the real one: an item's stock moves once per line.</summary>
internal sealed class FakeStock : IStockClient
{
    private readonly HashSet<(string, long, long)> _issued = [];

    public Dictionary<long, decimal> OnHand { get; } = new() { [101] = 10m };

    public Task<IReadOnlyList<StockItem>> SearchAsync(string? search, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StockItem>>([.. OnHand.Select(p => new StockItem { ItemId = p.Key, ItemCode = $"I{p.Key}", ItemName = "Tube light", QuantityOnHand = p.Value })]);

    public Task<IReadOnlyList<StockWarehouse>> WarehousesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StockWarehouse>>([new StockWarehouse { WarehouseId = 1, WarehouseCode = "MAIN", WarehouseName = "Main store" }]);

    public Task<StockIssueResponse> IssueAsync(StockIssueRequest request, CancellationToken ct)
    {
        var response = new StockIssueResponse { Success = true };
        foreach (StockIssueLine line in request.Lines)
        {
            if (!_issued.Add((request.SourceType, request.SourceId, line.SourceLineId)))
            {
                response.Lines.Add(new StockIssueLineResult { SourceLineId = line.SourceLineId, Success = true, Outcome = "AlreadyIssued", UnitCost = 45m });
                continue;
            }

            if (OnHand.GetValueOrDefault(line.ItemId) < line.Quantity)
            {
                response.Success = false;
                response.Lines.Add(new StockIssueLineResult { SourceLineId = line.SourceLineId, Outcome = "InsufficientStock" });
                continue;
            }

            OnHand[line.ItemId] -= line.Quantity;
            response.Lines.Add(new StockIssueLineResult { SourceLineId = line.SourceLineId, Success = true, Outcome = "Issued", UnitCost = 45m });
        }

        return Task.FromResult(response);
    }
}

/// <summary>
/// Work orders against a real database, with Facility, Employee and Inventory faked
/// (S6, TK-66). The card's Done-when: editing an Assigned work order is
/// refused, and issuing a part moves stock.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class WorkOrderServiceTests
{
    private readonly PostgresFixture _postgres;

    public WorkOrderServiceTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed record Branch(WorkOrderDbContext Db, FakeStock Stock);

    private async Task<Branch> NewBranchAsync()
    {
        Guid orgId = Guid.NewGuid();
        WorkOrderDbContext db = _postgres.CreateContext(Guid.NewGuid(), orgId);
        await new WorkOrderSeeder(db).SeedForOrganizationAsync(orgId, default);
        return new Branch(db, new FakeStock());
    }

    private static WorkOrderService Service(Branch b) => new(
        b.Db,
        new NumberGenerator(b.Db, Options.Create(new NumberingOptions()), new AprilYear()),
        new FakeFacility(),
        new FakeEmployees(),
        b.Stock,
        NullLogger<WorkOrderService>.Instance);

    private static SaveWorkOrderRequest Fan(long? assetId = 7, long? spaceId = 3) => new()
    {
        Title = "Ceiling fan not working",
        FacilityAssetId = assetId,
        SpaceId = spaceId,
        ReportedDate = new DateOnly(2026, 7, 1),
        Tasks = ["Check the regulator", "Replace the capacitor"],
    };

    private static async Task<long> AssignedAsync(WorkOrderService service)
    {
        long id = (await service.SaveAsync(null, Fan(), default)).Id!.Value;
        Assert.Equal(WorkOrderOutcome.Ok, (await service.ActAsync(id, new WorkOrderActionRequest { Action = WorkOrderAction.Assign, EmployeeId = 21 }, false, default)).Outcome);
        return id;
    }

    [SkippableFact]
    public async Task Editing_an_assigned_work_order_is_refused()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using WorkOrderDbContext _ = b.Db;
        WorkOrderService service = Service(b);

        long id = (await service.SaveAsync(null, Fan(), default)).Id!.Value;
        SaveWorkOrderRequest renamed = Fan();
        renamed.Title = "Ceiling fan noisy";
        Assert.Equal(WorkOrderOutcome.Ok, (await service.SaveAsync(id, renamed, default)).Outcome);

        await service.ActAsync(id, new WorkOrderActionRequest { Action = WorkOrderAction.Assign, EmployeeId = 21 }, false, default);
        WorkOrderResult refused = await service.SaveAsync(id, Fan(), default);

        Assert.Equal(WorkOrderOutcome.StateRule, refused.Outcome);
        WorkOrderView view = (await service.GetAsync(id, default))!;
        Assert.Equal("Ceiling fan noisy", view.Title);
        Assert.Equal("WRK-00001", view.WorkOrderNo);
        Assert.Equal(2, view.Tasks.Count);
    }

    [SkippableFact]
    public async Task Issuing_a_part_moves_stock_once_and_records_its_cost()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using WorkOrderDbContext _ = b.Db;
        WorkOrderService service = Service(b);
        long id = await AssignedAsync(service);

        WorkOrderResult issued = await service.IssuePartAsync(id, new IssuePartRequest { ItemId = 101, Quantity = 2, IssueDate = new DateOnly(2026, 7, 2) }, default);

        Assert.Equal(WorkOrderOutcome.Ok, issued.Outcome);
        Assert.Equal(8m, b.Stock.OnHand[101]);
        WorkOrderPart part = await b.Db.WorkOrderParts.AsNoTracking().SingleAsync();
        Assert.Equal(45m, part.UnitCost);
        Assert.Equal("Tube light", part.ItemName);
        Assert.Equal(90m, (await service.GetAsync(id, default))!.PartsCost);
    }

    [SkippableFact]
    public async Task A_part_short_of_stock_is_refused()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using WorkOrderDbContext _ = b.Db;
        WorkOrderService service = Service(b);
        long id = await AssignedAsync(service);

        WorkOrderResult refused = await service.IssuePartAsync(id, new IssuePartRequest { ItemId = 101, Quantity = 11, IssueDate = new DateOnly(2026, 7, 2) }, default);

        Assert.Equal(WorkOrderOutcome.StockRefused, refused.Outcome);
        Assert.Equal(10m, b.Stock.OnHand[101]);
    }

    [SkippableFact]
    public async Task Parts_are_refused_on_an_open_work_order()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using WorkOrderDbContext _ = b.Db;
        WorkOrderService service = Service(b);
        long id = (await service.SaveAsync(null, Fan(), default)).Id!.Value;

        Assert.Equal(WorkOrderOutcome.StateRule,
            (await service.IssuePartAsync(id, new IssuePartRequest { ItemId = 101, Quantity = 1, IssueDate = new DateOnly(2026, 7, 2) }, default)).Outcome);
        Assert.Equal(10m, b.Stock.OnHand[101]);
        Assert.False(await b.Db.WorkOrderParts.AnyAsync());
    }

    [SkippableFact]
    public async Task Where_and_who_are_checked()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using WorkOrderDbContext _ = b.Db;
        WorkOrderService service = Service(b);

        Assert.Equal(WorkOrderOutcome.Invalid, (await service.SaveAsync(null, Fan(null, null), default)).Outcome);
        Assert.Equal(WorkOrderOutcome.Invalid, (await service.SaveAsync(null, Fan(8, null), default)).Outcome);
        Assert.Equal(WorkOrderOutcome.Invalid, (await service.SaveAsync(null, Fan(99, 3), default)).Outcome);

        long id = (await service.SaveAsync(null, Fan(null, 3), default)).Id!.Value;
        Assert.Equal(WorkOrderOutcome.Invalid,
            (await service.ActAsync(id, new WorkOrderActionRequest { Action = WorkOrderAction.Assign, EmployeeId = 22 }, false, default)).Outcome);
        Assert.Equal(WorkOrderOutcome.Invalid,
            (await service.ActAsync(id, new WorkOrderActionRequest { Action = WorkOrderAction.Assign }, false, default)).Outcome);
    }

    [SkippableFact]
    public async Task Closing_needs_the_close_permission_and_a_completed_order()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using WorkOrderDbContext _ = b.Db;
        WorkOrderService service = Service(b);
        long id = await AssignedAsync(service);
        var close = new WorkOrderActionRequest { Action = WorkOrderAction.Close };

        Assert.Equal(WorkOrderOutcome.StateRule, (await service.ActAsync(id, close, true, default)).Outcome);
        await service.ActAsync(id, new WorkOrderActionRequest { Action = WorkOrderAction.Start }, false, default);
        Assert.Equal(WorkOrderOutcome.Ok, (await service.ActAsync(id,
            new WorkOrderActionRequest { Action = WorkOrderAction.Complete, CompletedDate = new DateOnly(2026, 7, 3), LabourCost = 300 }, false, default)).Outcome);

        Assert.Equal(WorkOrderOutcome.StateRule, (await service.ActAsync(id, close, false, default)).Outcome);
        Assert.Equal(WorkOrderOutcome.Ok, (await service.ActAsync(id, close, true, default)).Outcome);
        Assert.Equal(WorkOrderStatus.Closed, (await service.GetAsync(id, default))!.WorkOrderStatus);
    }

    [SkippableFact]
    public async Task Cancelling_needs_a_reason_and_no_parts_issued()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using WorkOrderDbContext _ = b.Db;
        WorkOrderService service = Service(b);
        long id = await AssignedAsync(service);

        Assert.Equal(WorkOrderOutcome.Invalid, (await service.ActAsync(id, new WorkOrderActionRequest { Action = WorkOrderAction.Cancel }, false, default)).Outcome);
        await service.IssuePartAsync(id, new IssuePartRequest { ItemId = 101, Quantity = 1, IssueDate = new DateOnly(2026, 7, 2) }, default);
        Assert.Equal(WorkOrderOutcome.StateRule,
            (await service.ActAsync(id, new WorkOrderActionRequest { Action = WorkOrderAction.Cancel, Reason = "Duplicate" }, false, default)).Outcome);
    }

    [SkippableFact]
    public async Task Raising_twice_with_one_source_key_makes_one_work_order()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using WorkOrderDbContext _ = b.Db;
        WorkOrderService service = Service(b);
        var raise = new RaiseWorkOrderRequest
        {
            SourceKey = "PPM:4:2026-07-01", Title = "Quarterly AC service", FacilityAssetId = 7,
            ReportedDate = new DateOnly(2026, 7, 1), PreventivePlanId = 4,
        };

        RaiseWorkOrderResponse first = await service.RaiseAsync(raise, default);
        RaiseWorkOrderResponse second = await service.RaiseAsync(raise, default);

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.WorkOrderId, second.WorkOrderId);
        Assert.Equal(1, await b.Db.WorkOrders.CountAsync());
    }
}
