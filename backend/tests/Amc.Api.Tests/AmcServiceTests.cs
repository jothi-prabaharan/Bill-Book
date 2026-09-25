using Amc.Api.Services;
using Amc.Entity.Enums;
using Amc.Entity.Models;
using Amc.Entity.TableEntities;
using Amc.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Kernel.Contacts;
using Shared.Kernel.School;
using Xunit;

namespace Amc.Api.Tests;

/// <summary>Master's contacts: 50 an active vendor, 51 a customer, 52 an inactive vendor.</summary>
internal sealed class FakeContacts : IContactDirectory
{
    public Task<IReadOnlyDictionary<long, ContactSummary>> FindAsync(IEnumerable<long> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<long, ContactSummary>>(ids.Where(id => id is 50 or 51 or 52).ToDictionary(id => id, id => new ContactSummary
        {
            ContactId = id, ContactCode = $"C{id}", DisplayName = id == 50 ? "CoolAir Services" : "Other",
            IsVendor = id != 51, IsCustomer = id == 51, IsActive = id != 52,
        }));

    public Task<EnsureGuardianResponse> EnsureGuardianAsync(string displayName, string mobileNumber, string? email, CancellationToken ct) =>
        throw new NotSupportedException();
}

/// <summary>Facility with assets 7 and 8 in use and 9 disposed.</summary>
internal sealed class FakeFacility : IFacilityClient
{
    public Task<FacilityLookupResponse> LookupAsync(IEnumerable<long> assetIds, IEnumerable<long> spaceIds, CancellationToken ct) =>
        Task.FromResult(new FacilityLookupResponse
        {
            Assets = [.. assetIds.Where(id => id is 7 or 8 or 9).Select(id => new FacilityItem { Id = id, Code = $"A{id}", Name = "AC", IsUsable = id != 9 })],
        });
}

/// <summary>WorkOrder, idempotent on source key like the real one.</summary>
internal sealed class FakeWorkOrders : IWorkOrderClient
{
    public Dictionary<string, RaiseWorkOrder> Raised { get; } = [];

    public bool Down { get; set; }

    public Task<RaisedWorkOrder> RaiseAsync(RaiseWorkOrder request, CancellationToken ct)
    {
        if (Down)
        {
            throw new HttpRequestException("WorkOrder is down.");
        }

        Raised.TryAdd(request.SourceKey, request);
        int n = Raised.Keys.ToList().IndexOf(request.SourceKey) + 1;
        return Task.FromResult(new RaisedWorkOrder { WorkOrderId = n, WorkOrderNo = $"WRK-{n:00000}", Created = true });
    }
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

/// <summary>
/// Contracts, covered assets, visits and renewals against a real database, with
/// Master, Facility and WorkOrder faked (S8, TK-68). The card's Done-when:
/// covered assets and visits are recorded, and a reminder is due before expiry
/// (the sending half is <c>Notification.Worker.Tests.AmcRenewalReminderRunTests</c>).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AmcServiceTests
{
    private readonly PostgresFixture _postgres;

    public AmcServiceTests(PostgresFixture postgres) => _postgres = postgres;

    private static AmcService Service(AmcDbContext db, FakeWorkOrders workOrders, FixedClock clock) =>
        new(db, new FakeContacts(), new FakeFacility(), workOrders, clock, NullLogger<AmcService>.Instance);

    private static FixedClock On(int year, int month, int day) => new(new DateTimeOffset(year, month, day, 9, 0, 0, TimeSpan.Zero));

    private static SaveContractRequest CoolAir(params long[] assets) => new()
    {
        ContractNo = "CA/2026/114",
        VendorContactId = 50,
        StartDate = new DateOnly(2026, 4, 1),
        EndDate = new DateOnly(2027, 3, 31),
        ContractValue = 48000m,
        VisitsPerYear = 4,
        AmcCoverage = AmcCoverage.Comprehensive,
        RenewalReminderDays = 30,
        ReminderEmail = "office@school.in",
        FacilityAssetIds = [.. assets],
    };

    [SkippableFact]
    public async Task A_contracts_covered_assets_and_visits_are_recorded()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using AmcDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        var workOrders = new FakeWorkOrders();
        AmcService service = Service(db, workOrders, On(2026, 5, 1));

        long id = (await service.SaveAsync(null, CoolAir(7, 8), default)).Id!.Value;
        Assert.Equal(AmcOutcome.Ok, (await service.ActivateAsync(id, default)).Outcome);

        Assert.Equal(AmcOutcome.Ok, (await service.RecordVisitAsync(id, new RecordVisitRequest
        {
            VisitDate = new DateOnly(2026, 6, 10), VisitKind = VisitKind.Scheduled, Remarks = "Filters cleaned",
        }, default)).Outcome);
        Assert.Equal(AmcOutcome.Ok, (await service.RecordVisitAsync(id, new RecordVisitRequest
        {
            VisitDate = new DateOnly(2026, 7, 2), VisitKind = VisitKind.Breakdown, FacilityAssetId = 8, RaiseWorkOrder = true,
        }, default)).Outcome);

        ContractView view = (await service.GetAsync(id, default))!;
        Assert.Equal(ContractStatus.Active, view.ContractStatus);
        Assert.Equal([7L, 8L], view.FacilityAssetIds.Order());
        Assert.Equal(2, view.VisitsMade);

        List<VisitView> visits = await service.VisitsAsync(id, default);
        Assert.Equal("WRK-00001", visits.First().WorkOrderNo);
        RaiseWorkOrder raised = Assert.Single(workOrders.Raised.Values);
        Assert.Equal("Amc", raised.WorkOrderSource);
        Assert.Equal(id, raised.AmcContractId);
        Assert.Equal(8, raised.FacilityAssetId);
    }

    [SkippableFact]
    public async Task An_asset_is_under_only_one_active_contract_at_a_time()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using AmcDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        AmcService service = Service(db, new FakeWorkOrders(), On(2026, 5, 1));

        long first = (await service.SaveAsync(null, CoolAir(7), default)).Id!.Value;
        await service.ActivateAsync(first, default);

        SaveContractRequest overlapping = CoolAir(7);
        overlapping.ContractNo = "CA/2026/200";
        overlapping.StartDate = new DateOnly(2026, 10, 1);
        overlapping.EndDate = new DateOnly(2027, 9, 30);
        long second = (await service.SaveAsync(null, overlapping, default)).Id!.Value;
        Assert.Equal(AmcOutcome.Invalid, (await service.ActivateAsync(second, default)).Outcome);

        // The renewal starting the day after the first ends does not overlap.
        SaveContractRequest renewal = CoolAir(7);
        renewal.ContractNo = "CA/2027/001";
        renewal.StartDate = new DateOnly(2027, 4, 1);
        renewal.EndDate = new DateOnly(2028, 3, 31);
        long third = (await service.SaveAsync(null, renewal, default)).Id!.Value;
        Assert.Equal(AmcOutcome.Ok, (await service.ActivateAsync(third, default)).Outcome);
    }

    [SkippableFact]
    public async Task The_vendor_and_assets_are_checked()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using AmcDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        AmcService service = Service(db, new FakeWorkOrders(), On(2026, 5, 1));

        foreach (long vendor in new long[] { 51, 52, 99 })
        {
            SaveContractRequest request = CoolAir(7);
            request.VendorContactId = vendor;
            Assert.Equal(AmcOutcome.Invalid, (await service.SaveAsync(null, request, default)).Outcome);
        }

        Assert.Equal(AmcOutcome.Invalid, (await service.SaveAsync(null, CoolAir(9), default)).Outcome);

        SaveContractRequest backwards = CoolAir(7);
        backwards.EndDate = backwards.StartDate;
        Assert.Equal(AmcOutcome.Invalid, (await service.SaveAsync(null, backwards, default)).Outcome);
    }

    [SkippableFact]
    public async Task An_active_contracts_terms_are_fixed_but_its_assets_can_change()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using AmcDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        AmcService service = Service(db, new FakeWorkOrders(), On(2026, 5, 1));
        long id = (await service.SaveAsync(null, CoolAir(7), default)).Id!.Value;
        await service.ActivateAsync(id, default);

        SaveContractRequest dearer = CoolAir(7);
        dearer.ContractValue = 60000m;
        Assert.Equal(AmcOutcome.StateRule, (await service.SaveAsync(id, dearer, default)).Outcome);

        Assert.Equal(AmcOutcome.Ok, (await service.SaveAsync(id, CoolAir(7, 8), default)).Outcome);
        Assert.Equal(2, await db.AmcCoveredAssets.CountAsync(a => a.AmcContractId == id));
    }

    [SkippableFact]
    public async Task A_visit_needs_the_contract_in_force_and_a_covered_asset()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using AmcDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        var workOrders = new FakeWorkOrders();
        AmcService service = Service(db, workOrders, On(2026, 5, 1));
        long id = (await service.SaveAsync(null, CoolAir(7), default)).Id!.Value;

        var visit = new RecordVisitRequest { VisitDate = new DateOnly(2026, 6, 1) };
        Assert.Equal(AmcOutcome.StateRule, (await service.RecordVisitAsync(id, visit, default)).Outcome);

        await service.ActivateAsync(id, default);
        Assert.Equal(AmcOutcome.StateRule,
            (await service.RecordVisitAsync(id, new RecordVisitRequest { VisitDate = new DateOnly(2027, 4, 2) }, default)).Outcome);
        Assert.Equal(AmcOutcome.Invalid,
            (await service.RecordVisitAsync(id, new RecordVisitRequest { VisitDate = new DateOnly(2026, 6, 1), FacilityAssetId = 8 }, default)).Outcome);
        Assert.Equal(AmcOutcome.Invalid,
            (await service.RecordVisitAsync(id, new RecordVisitRequest { VisitDate = new DateOnly(2026, 6, 1), RaiseWorkOrder = true }, default)).Outcome);

        workOrders.Down = true;
        Assert.Equal(AmcOutcome.Unavailable, (await service.RecordVisitAsync(id,
            new RecordVisitRequest { VisitDate = new DateOnly(2026, 6, 1), FacilityAssetId = 7, RaiseWorkOrder = true }, default)).Outcome);
    }

    [SkippableFact]
    public async Task A_renewal_is_due_in_the_window_before_expiry_and_expiry_is_recorded()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using AmcDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        AmcService service = Service(db, new FakeWorkOrders(), On(2026, 5, 1));
        long id = (await service.SaveAsync(null, CoolAir(7), default)).Id!.Value;
        await service.ActivateAsync(id, default);

        Assert.Empty(await service.RenewalsDueAsync(new DateOnly(2027, 2, 28), default));

        AmcRenewalDue due = Assert.Single(await service.RenewalsDueAsync(new DateOnly(2027, 3, 1), default));
        Assert.Equal("CA/2026/114", due.ContractNo);
        Assert.Equal("CoolAir Services", due.VendorName);
        Assert.Equal("office@school.in", due.ReminderEmail);

        Assert.Empty(await service.RenewalsDueAsync(new DateOnly(2027, 4, 1), default));
        Assert.Equal(ContractStatus.Expired, (await db.AmcContracts.AsNoTracking().SingleAsync(c => c.AmcContractId == id)).ContractStatus);
    }

    [SkippableFact]
    public async Task A_terminated_contract_takes_no_visits_and_sends_no_reminder()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using AmcDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        AmcService service = Service(db, new FakeWorkOrders(), On(2026, 5, 1));
        long id = (await service.SaveAsync(null, CoolAir(7), default)).Id!.Value;
        await service.ActivateAsync(id, default);

        Assert.Equal(AmcOutcome.Ok, (await service.TerminateAsync(id, "Vendor closed down", default)).Outcome);

        Assert.Equal(AmcOutcome.StateRule,
            (await service.RecordVisitAsync(id, new RecordVisitRequest { VisitDate = new DateOnly(2026, 6, 1) }, default)).Outcome);
        Assert.Empty(await service.RenewalsDueAsync(new DateOnly(2027, 3, 15), default));
        AmcContract row = await db.AmcContracts.AsNoTracking().SingleAsync();
        Assert.Equal("Vendor closed down", row.TerminationReason);
    }
}
