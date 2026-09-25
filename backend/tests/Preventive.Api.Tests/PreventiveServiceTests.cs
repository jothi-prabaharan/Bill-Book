using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Preventive.Api.Services;
using Preventive.Entity.Enums;
using Preventive.Entity.Models;
using Preventive.Entity.TableEntities;
using Preventive.Repository;
using Shared.Kernel.Employees;
using Shared.Kernel.School;
using Xunit;

namespace Preventive.Api.Tests;

/// <summary>Facility with asset 7 in use and space 3 active.</summary>
internal sealed class FakeFacility : IFacilityClient
{
    public Task<FacilityLookupResponse> LookupAsync(IEnumerable<long> assetIds, IEnumerable<long> spaceIds, CancellationToken ct) =>
        Task.FromResult(new FacilityLookupResponse
        {
            Assets = [.. assetIds.Where(id => id == 7).Select(id => new FacilityItem { Id = id, Code = "AC-1", Name = "AC", IsUsable = true })],
            Spaces = [.. spaceIds.Where(id => id == 3).Select(id => new FacilityItem { Id = id, Code = "S3", Name = "Lab", IsUsable = true })],
        });
}

internal sealed class FakeEmployees : IEmployeeDirectory
{
    public Task<IReadOnlyDictionary<long, EmployeeProfile>> FindAsync(IEnumerable<long> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<long, EmployeeProfile>>(ids.Where(id => id == 21).ToDictionary(id => id, id => new EmployeeProfile
        {
            EmployeeId = id, EmployeeCode = "E21", FullName = "Ravi", Gender = "Male", EmployeeStatus = "Active",
        }));
}

/// <summary>WorkOrder, idempotent on source key like the real one.</summary>
internal sealed class FakeWorkOrders : IWorkOrderClient
{
    public Dictionary<string, RaisedWorkOrder> Raised { get; } = [];

    public int FailNext { get; set; }

    public Task<RaisedWorkOrder> RaiseAsync(RaiseWorkOrder request, CancellationToken ct)
    {
        if (FailNext > 0)
        {
            FailNext--;
            throw new HttpRequestException("WorkOrder is down.");
        }

        if (Raised.TryGetValue(request.SourceKey, out RaisedWorkOrder? already))
        {
            return Task.FromResult(new RaisedWorkOrder { WorkOrderId = already.WorkOrderId, WorkOrderNo = already.WorkOrderNo, Created = false });
        }

        var made = new RaisedWorkOrder { WorkOrderId = Raised.Count + 1, WorkOrderNo = $"WRK-{Raised.Count + 1:00000}", Created = true };
        Raised[request.SourceKey] = made;
        return Task.FromResult(made);
    }
}

/// <summary>
/// Plans and generation against a real database, with Facility, Hrm and
/// WorkOrder faked (S7, TK-67). The card's Done-when: running generation twice
/// raises one work order per occurrence.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PreventiveServiceTests
{
    private readonly PostgresFixture _postgres;

    public PreventiveServiceTests(PostgresFixture postgres) => _postgres = postgres;

    private static PreventiveService Service(PreventiveDbContext db, FakeWorkOrders workOrders) =>
        new(db, new FakeFacility(), new FakeEmployees(), workOrders, NullLogger<PreventiveService>.Instance);

    private static SavePlanRequest AcService(DateOnly start, Frequency frequency = Frequency.Monthly, int leadDays = 0) => new()
    {
        Name = "AC service",
        FacilityAssetId = 7,
        Frequency = frequency,
        StartDate = start,
        LeadDays = leadDays,
        DefaultAssigneeEmployeeId = 21,
    };

    [SkippableFact]
    public async Task Running_generation_twice_raises_one_work_order_per_occurrence()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using PreventiveDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        var workOrders = new FakeWorkOrders();
        long plan = (await Service(db, workOrders).SavePlanAsync(null, AcService(new DateOnly(2026, 4, 1)), default)).Id!.Value;
        var today = new DateOnly(2026, 6, 15);

        GenerationResult first = await Service(db, workOrders).GenerateAsync(today, default);
        db.ChangeTracker.Clear();
        GenerationResult second = await Service(db, workOrders).GenerateAsync(today, default);

        // April, May and June are due; July is not yet.
        Assert.Equal(3, first.Generated);
        Assert.Equal(3, first.Raised);
        Assert.Equal(0, second.Generated);
        Assert.Equal(0, second.Raised);
        Assert.Equal(3, workOrders.Raised.Count);
        Assert.Equal(
            [new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 1), new DateOnly(2026, 6, 1)],
            await db.PreventiveOccurrences.OrderBy(o => o.DueDate).Select(o => o.DueDate).ToListAsync());
        Assert.All(await db.PreventiveOccurrences.ToListAsync(), o => Assert.Equal(OccurrenceStatus.Raised, o.OccurrenceStatus));
        Assert.Equal(new DateOnly(2026, 7, 1), (await db.PreventivePlans.SingleAsync(p => p.PreventivePlanId == plan)).NextDueDate);
        Assert.Contains(PreventiveService.SourceKey(plan, new DateOnly(2026, 5, 1)), workOrders.Raised.Keys);
    }

    [SkippableFact]
    public async Task A_work_order_that_could_not_be_raised_is_raised_once_by_the_next_run()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using PreventiveDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        var workOrders = new FakeWorkOrders { FailNext = 1 };
        await Service(db, workOrders).SavePlanAsync(null, AcService(new DateOnly(2026, 6, 1)), default);
        var today = new DateOnly(2026, 6, 2);

        GenerationResult first = await Service(db, workOrders).GenerateAsync(today, default);
        Assert.Equal(1, first.Generated);
        Assert.Equal(1, first.Failed);
        Assert.Equal(OccurrenceStatus.Scheduled, (await db.PreventiveOccurrences.SingleAsync()).OccurrenceStatus);

        db.ChangeTracker.Clear();
        GenerationResult second = await Service(db, workOrders).GenerateAsync(today, default);
        Assert.Equal(0, second.Generated);
        Assert.Equal(1, second.Raised);
        Assert.Single(workOrders.Raised);
    }

    [SkippableFact]
    public async Task A_work_order_raised_but_not_recorded_is_not_raised_again()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using PreventiveDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        var workOrders = new FakeWorkOrders();
        long plan = (await Service(db, workOrders).SavePlanAsync(null, AcService(new DateOnly(2026, 6, 1)), default)).Id!.Value;

        // WorkOrder already holds this occurrence's work order, as if the last run died before recording it.
        await workOrders.RaiseAsync(new RaiseWorkOrder { SourceKey = PreventiveService.SourceKey(plan, new DateOnly(2026, 6, 1)), Title = "AC service" }, default);

        GenerationResult result = await Service(db, workOrders).GenerateAsync(new DateOnly(2026, 6, 2), default);

        Assert.Equal(0, result.Raised);
        Assert.Single(workOrders.Raised);
        PreventiveOccurrence occurrence = await db.PreventiveOccurrences.SingleAsync();
        Assert.Equal(OccurrenceStatus.Raised, occurrence.OccurrenceStatus);
        Assert.Equal("WRK-00001", occurrence.WorkOrderNo);
    }

    [SkippableFact]
    public async Task Lead_days_raise_early_and_the_end_date_stops_the_plan()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using PreventiveDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        var workOrders = new FakeWorkOrders();
        SavePlanRequest request = AcService(new DateOnly(2026, 7, 1), Frequency.Monthly, leadDays: 5);
        request.EndDate = new DateOnly(2026, 8, 15);
        await Service(db, workOrders).SavePlanAsync(null, request, default);

        Assert.Equal(0, (await Service(db, workOrders).GenerateAsync(new DateOnly(2026, 6, 25), default)).Generated);
        Assert.Equal(1, (await Service(db, workOrders).GenerateAsync(new DateOnly(2026, 6, 26), default)).Generated);
        db.ChangeTracker.Clear();
        Assert.Equal(1, (await Service(db, workOrders).GenerateAsync(new DateOnly(2027, 1, 1), default)).Generated);
        Assert.Equal(2, await db.PreventiveOccurrences.CountAsync());
    }

    [SkippableFact]
    public async Task A_skipped_occurrence_raises_no_work_order()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using PreventiveDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        var workOrders = new FakeWorkOrders { FailNext = 1 };
        await Service(db, workOrders).SavePlanAsync(null, AcService(new DateOnly(2026, 6, 1)), default);
        await Service(db, workOrders).GenerateAsync(new DateOnly(2026, 6, 1), default);
        long occurrence = (await db.PreventiveOccurrences.SingleAsync()).PreventiveOccurrenceId;

        Assert.Equal(PreventiveOutcome.Ok, (await Service(db, workOrders).SetOccurrenceAsync(occurrence, OccurrenceStatus.Skipped, default)).Outcome);
        db.ChangeTracker.Clear();
        await Service(db, workOrders).GenerateAsync(new DateOnly(2026, 6, 2), default);

        Assert.Empty(workOrders.Raised);
        Assert.Equal(PreventiveOutcome.StateRule, (await Service(db, workOrders).SetOccurrenceAsync(occurrence, OccurrenceStatus.Done, default)).Outcome);
    }

    [SkippableFact]
    public async Task A_plan_needs_something_to_maintain_and_a_current_assignee()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using PreventiveDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        PreventiveService service = Service(db, new FakeWorkOrders());

        SavePlanRequest nowhere = AcService(new DateOnly(2026, 6, 1));
        nowhere.FacilityAssetId = null;
        Assert.Equal(PreventiveOutcome.Invalid, (await service.SavePlanAsync(null, nowhere, default)).Outcome);

        SavePlanRequest stranger = AcService(new DateOnly(2026, 6, 1));
        stranger.DefaultAssigneeEmployeeId = 99;
        Assert.Equal(PreventiveOutcome.Invalid, (await service.SavePlanAsync(null, stranger, default)).Outcome);

        SavePlanRequest backwards = AcService(new DateOnly(2026, 6, 1));
        backwards.EndDate = new DateOnly(2026, 5, 1);
        Assert.Equal(PreventiveOutcome.Invalid, (await service.SavePlanAsync(null, backwards, default)).Outcome);
    }

    [SkippableFact]
    public async Task Changing_the_schedule_carries_on_after_what_is_already_generated()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using PreventiveDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        var workOrders = new FakeWorkOrders();
        long plan = (await Service(db, workOrders).SavePlanAsync(null, AcService(new DateOnly(2026, 4, 1)), default)).Id!.Value;
        await Service(db, workOrders).GenerateAsync(new DateOnly(2026, 5, 10), default);

        db.ChangeTracker.Clear();
        await Service(db, workOrders).SavePlanAsync(plan, AcService(new DateOnly(2026, 4, 1), Frequency.Quarterly), default);

        Assert.Equal(new DateOnly(2026, 7, 1), (await db.PreventivePlans.AsNoTracking().SingleAsync()).NextDueDate);
    }
}
