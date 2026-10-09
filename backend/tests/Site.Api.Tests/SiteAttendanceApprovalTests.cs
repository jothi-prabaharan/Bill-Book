using System;
using System.Threading.Tasks;
using Shared.Kernel.Tenancy;
using Site.Entity.Enums;
using Site.Entity.TableEntities;
using Site.Repository.Repositories;
using Xunit;

namespace Site.Api.Tests;

[Collection(nameof(PostgresCollection))]
public sealed class SiteAttendanceApprovalTests
{
    private readonly PostgresFixture _postgres;

    public SiteAttendanceApprovalTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [SkippableFact]
    public async Task Approval_freezes_margin_snapshots_and_increments_assignment_counters()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var customerId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        await using var db = _postgres.CreateContext(customerId, orgId);
        var fileStorage = new FakeFileStorage();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId, CustomerCode = "CUST001" };
        var marginRepo = new SiteMarginRepository(db);
        var repo = new SiteAttendanceRepository(db, fileStorage, tenant, marginRepo);

        // 1. Setup trade, laborer with cost ₹800/day, OT cost ₹100/hr
        var trade = new Trade
        {
            TradeCode = $"TRD-{Guid.NewGuid():N}"[..10],
            TradeName = "Carpentry",
            DefaultCostRatePerDay = 800m,
            DefaultSellRatePerDay = 1000m,
            DefaultOtCostRatePerHour = 100m,
            DefaultOtSellRatePerHour = 150m,
        };
        db.Trades.Add(trade);
        await db.SaveChangesAsync();

        var site = new SiteJob
        {
            SiteCode = $"SIT-{Guid.NewGuid():N}"[..10],
            SiteName = "Luxury Villa",
            Address = "Whitefield",
            Status = SiteStatus.Active,
        };
        var laborer = new Laborer
        {
            FullName = "Ravi Kumar",
            TradeId = trade.TradeId,
            Phone = "9876500001",
            CostRatePerDay = 800m,
            SellRatePerDay = 1000m,
            OtCostRatePerHour = 100m,
            OtSellRatePerHour = 150m,
            IsActive = true,
        };
        db.Sites.Add(site);
        db.Laborers.Add(laborer);
        await db.SaveChangesAsync();

        // 2. Setup site margin rule: 20% on Labor, Flat ₹50 on Overtime
        var laborMarginRule = new MarginRule
        {
            Scope = MarginScope.Site,
            ScopeId = site.SiteId,
            AppliesTo = MarginAppliesTo.Labor,
            MarginType = MarginType.Percent,
            MarginValue = 20m, // 20% of 800 = 160 -> Sell 960
            EffectiveFrom = new DateOnly(2026, 1, 1),
            IsActive = true,
        };
        var otMarginRule = new MarginRule
        {
            Scope = MarginScope.Site,
            ScopeId = site.SiteId,
            AppliesTo = MarginAppliesTo.Overtime,
            MarginType = MarginType.FlatPerUnit,
            MarginValue = 50m, // 100 + 50 = 150 -> Sell 150
            EffectiveFrom = new DateOnly(2026, 1, 1),
            IsActive = true,
        };
        db.MarginRules.AddRange(laborMarginRule, otMarginRule);

        var assignment = new SiteAssignment
        {
            SiteId = site.SiteId,
            LaborerId = laborer.LaborerId,
            TradeId = trade.TradeId,
            StartDate = new DateOnly(2026, 10, 1),
            ApprovedDays = 2.0m,
            ApprovedOtHours = 1.0m,
            Status = AssignmentStatus.Active,
        };
        db.SiteAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var attendance = new SiteAttendance
        {
            AssignmentId = assignment.SiteAssignmentId,
            SiteId = site.SiteId,
            LaborerId = laborer.LaborerId,
            WorkDate = new DateOnly(2026, 10, 9),
            CheckInTime = DateTime.UtcNow.AddHours(-9),
            ClockOutTime = DateTime.UtcNow,
            DayFraction = 1.0m,
            OtHoursClaimed = 3.0m,
            WorkSummary = "Cabinet installation complete",
            Status = AttendanceStatus.Submitted,
        };
        db.SiteAttendances.Add(attendance);
        await db.SaveChangesAsync();

        // 3. Approve clock out with 2.5 hours of approved OT (adjusted down from 3 claimed)
        var approved = await repo.ApproveClockOutAsync(
            attendance.AttendanceId,
            otHoursApproved: 2.5m,
            approvedByUserId: 12345);

        Assert.Equal(AttendanceStatus.Approved, approved.Status);
        Assert.Equal(800m, approved.DayCostSnapshot);
        Assert.Equal(160m, approved.DayMarginSnapshot); // 20% of 800
        Assert.Equal(960m, approved.DaySellSnapshot); // 800 + 160
        Assert.Equal(100m, approved.OtCostSnapshot);
        Assert.Equal(50m, approved.OtMarginSnapshot); // Flat ₹50
        Assert.Equal(150m, approved.OtSellSnapshot); // 100 + 50
        Assert.Equal(2.5m, approved.OtHoursApproved);

        // 4. Verify assignment counters rose atomically in the same transaction
        var updatedAssignment = await db.SiteAssignments.FindAsync(assignment.SiteAssignmentId);
        Assert.NotNull(updatedAssignment);
        Assert.Equal(3.0m, updatedAssignment!.ApprovedDays); // was 2.0, + 1.0
        Assert.Equal(3.5m, updatedAssignment.ApprovedOtHours); // was 1.0, + 2.5

        // 5. Repricing test: if margin rule changes later, frozen snapshot does not change!
        laborMarginRule.MarginValue = 50m; // Changed to 50%
        await db.SaveChangesAsync();

        var reloadedAttendance = await db.SiteAttendances.FindAsync(attendance.AttendanceId);
        Assert.NotNull(reloadedAttendance);
        Assert.Equal(960m, reloadedAttendance!.DaySellSnapshot); // Still 960, not affected!
    }

    [SkippableFact]
    public async Task Rejection_leaves_assignment_counters_unchanged()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var customerId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        await using var db = _postgres.CreateContext(customerId, orgId);
        var fileStorage = new FakeFileStorage();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId, CustomerCode = "CUST001" };
        var marginRepo = new SiteMarginRepository(db);
        var repo = new SiteAttendanceRepository(db, fileStorage, tenant, marginRepo);

        var trade = new Trade { TradeCode = $"TRD-{Guid.NewGuid():N}"[..10], TradeName = "Welding" };
        db.Trades.Add(trade);
        await db.SaveChangesAsync();

        var site = new SiteJob { SiteCode = $"SIT-{Guid.NewGuid():N}"[..10], SiteName = "Industrial Park", Status = SiteStatus.Active };
        var laborer = new Laborer { FullName = "Manoj", TradeId = trade.TradeId, Phone = "9876500002", IsActive = true };
        db.Sites.Add(site);
        db.Laborers.Add(laborer);
        await db.SaveChangesAsync();

        var assignment = new SiteAssignment
        {
            SiteId = site.SiteId,
            LaborerId = laborer.LaborerId,
            TradeId = trade.TradeId,
            StartDate = new DateOnly(2026, 10, 1),
            ApprovedDays = 5.0m,
            ApprovedOtHours = 2.0m,
            Status = AssignmentStatus.Active,
        };
        db.SiteAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var attendance = new SiteAttendance
        {
            AssignmentId = assignment.SiteAssignmentId,
            SiteId = site.SiteId,
            LaborerId = laborer.LaborerId,
            WorkDate = new DateOnly(2026, 10, 9),
            CheckInTime = DateTime.UtcNow.AddHours(-9),
            ClockOutTime = DateTime.UtcNow,
            DayFraction = 1.0m,
            OtHoursClaimed = 4.0m,
            WorkSummary = "Incomplete work",
            Status = AttendanceStatus.Submitted,
        };
        db.SiteAttendances.Add(attendance);
        await db.SaveChangesAsync();

        // Reject clock-out
        var rejected = await repo.RejectClockOutAsync(
            attendance.AttendanceId,
            "Photos are blurry and work was reported incomplete by site engineer",
            rejectedByUserId: 12345);

        Assert.Equal(AttendanceStatus.Rejected, rejected.Status);
        Assert.Equal("Photos are blurry and work was reported incomplete by site engineer", rejected.RejectionReason);

        // Verify assignment counters did NOT change
        var updatedAssignment = await db.SiteAssignments.FindAsync(assignment.SiteAssignmentId);
        Assert.NotNull(updatedAssignment);
        Assert.Equal(5.0m, updatedAssignment!.ApprovedDays);
        Assert.Equal(2.0m, updatedAssignment.ApprovedOtHours);
    }
}
