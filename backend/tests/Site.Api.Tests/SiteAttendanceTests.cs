using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shared.Kernel.Storage;
using Shared.Kernel.Tenancy;
using Site.Entity.Enums;
using Site.Entity.TableEntities;
using Site.Repository.Repositories;
using Xunit;

namespace Site.Api.Tests;

public sealed class FakeFileStorage : IFileStorage
{
    public readonly Dictionary<string, byte[]> Files = new();

    public Task<string> SaveAsync(
        string key,
        Stream content,
        string contentType,
        FileWriteMode mode = FileWriteMode.CreateNew,
        CancellationToken ct = default)
    {
        if (mode == FileWriteMode.CreateNew && Files.ContainsKey(key))
        {
            throw new InvalidOperationException($"Key already exists: {key}");
        }

        using var ms = new MemoryStream();
        content.CopyTo(ms);
        Files[key] = ms.ToArray();
        return Task.FromResult(key);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
    {
        if (Files.TryGetValue(key, out var bytes))
        {
            return Task.FromResult<Stream?>(new MemoryStream(bytes));
        }

        return Task.FromResult<Stream?>(null);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        Files.Remove(key);
        return Task.CompletedTask;
    }

    public Task<Uri?> GetDownloadUrlAsync(string key, TimeSpan lifetime, CancellationToken ct = default)
    {
        return Task.FromResult<Uri?>(new Uri($"https://fake.storage/{key}"));
    }
}

[Collection(nameof(PostgresCollection))]
public sealed class SiteAttendanceTests
{
    private readonly PostgresFixture _postgres;

    public SiteAttendanceTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public void Geofence_distance_calculation_flags_breach_correctly()
    {
        // Coordinates for Site: 12.9716, 77.5946 (Bangalore City Center), Radius: 200m
        decimal siteLat = 12.9716m;
        decimal siteLng = 77.5946m;

        // Inside radius (~50m away)
        decimal insideLat = 12.9718m;
        decimal insideLng = 77.5948m;
        double insideDist = SiteAttendanceRepository.CalculateDistanceMeters(insideLat, insideLng, siteLat, siteLng);
        Assert.True(insideDist <= 200, $"Expected inside radius, got {insideDist}m");

        // Outside radius (~2km away)
        decimal outsideLat = 12.9900m;
        decimal outsideLng = 77.6100m;
        double outsideDist = SiteAttendanceRepository.CalculateDistanceMeters(outsideLat, outsideLng, siteLat, siteLng);
        Assert.True(outsideDist > 200, $"Expected outside radius, got {outsideDist}m");
    }

    [SkippableFact]
    public async Task CheckIn_within_geofence_records_attendance_without_breach()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var customerId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        await using var db = _postgres.CreateContext(customerId, orgId);
        var fileStorage = new FakeFileStorage();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId, CustomerCode = "CUST001" };
        var repo = new SiteAttendanceRepository(db, fileStorage, tenant, new SiteMarginRepository(db));

        var site = new SiteJob
        {
            SiteCode = $"SIT-{Guid.NewGuid():N}"[..10],
            SiteName = "Metro Station Site",
            Address = "MG Road",
            Latitude = 12.9716m,
            Longitude = 77.5946m,
            GeofenceRadiusMeters = 200,
            Status = SiteStatus.Active,
        };
        var trade = new Trade
        {
            TradeCode = $"TRD-{Guid.NewGuid():N}"[..10],
            TradeName = "Masonry",
        };
        db.Trades.Add(trade);
        await db.SaveChangesAsync();

        var laborer = new Laborer
        {
            FullName = "Ramesh Kumar",
            TradeId = trade.TradeId,
            Phone = "9876543210",
            IsActive = true,
        };
        db.Sites.Add(site);
        db.Laborers.Add(laborer);
        await db.SaveChangesAsync();

        var assignment = new SiteAssignment
        {
            SiteId = site.SiteId,
            LaborerId = laborer.LaborerId,
            TradeId = trade.TradeId,
            StartDate = new DateOnly(2026, 10, 1),
            Status = AssignmentStatus.Active,
        };
        db.SiteAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var today = new DateOnly(2026, 10, 9);
        var attendance = await repo.CheckInAsync(
            assignmentId: assignment.SiteAssignmentId,
            latitude: 12.9718m,
            longitude: 77.5948m,
            expectedLaborerId: laborer.LaborerId,
            today: today);

        Assert.NotNull(attendance);
        Assert.Equal(AttendanceStatus.CheckedIn, attendance.Status);
        Assert.False(attendance.IsGeofenceBreached);
        Assert.Equal(today, attendance.WorkDate);
    }

    [SkippableFact]
    public async Task CheckIn_outside_geofence_is_accepted_and_flags_breached_per_D63()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var customerId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        await using var db = _postgres.CreateContext(customerId, orgId);
        var fileStorage = new FakeFileStorage();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId, CustomerCode = "CUST001" };
        var repo = new SiteAttendanceRepository(db, fileStorage, tenant, new SiteMarginRepository(db));

        var trade = new Trade { TradeCode = $"TRD-{Guid.NewGuid():N}"[..10], TradeName = "Carpentry" };
        db.Trades.Add(trade);
        await db.SaveChangesAsync();

        var site = new SiteJob
        {
            SiteCode = $"SIT-{Guid.NewGuid():N}"[..10],
            SiteName = "Bridge Project",
            Address = "Outer Ring Road",
            Latitude = 12.9716m,
            Longitude = 77.5946m,
            GeofenceRadiusMeters = 200,
            Status = SiteStatus.Active,
        };
        var laborer = new Laborer
        {
            FullName = "Suresh Patel",
            TradeId = trade.TradeId,
            Phone = "9876543211",
            IsActive = true,
        };
        db.Sites.Add(site);
        db.Laborers.Add(laborer);
        await db.SaveChangesAsync();

        var assignment = new SiteAssignment
        {
            SiteId = site.SiteId,
            LaborerId = laborer.LaborerId,
            TradeId = trade.TradeId,
            StartDate = new DateOnly(2026, 10, 1),
            Status = AssignmentStatus.Active,
        };
        db.SiteAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var today = new DateOnly(2026, 10, 9);
        var attendance = await repo.CheckInAsync(
            assignmentId: assignment.SiteAssignmentId,
            latitude: 13.0500m,
            longitude: 77.6500m,
            expectedLaborerId: laborer.LaborerId,
            today: today);

        Assert.NotNull(attendance);
        Assert.Equal(AttendanceStatus.CheckedIn, attendance.Status);
        Assert.True(attendance.IsGeofenceBreached, "D-63 mandates accepting check-in outside geofence but flagging breach");
    }

    [SkippableFact]
    public async Task Duplicate_check_in_on_same_day_and_assignment_is_refused()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var customerId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        await using var db = _postgres.CreateContext(customerId, orgId);
        var fileStorage = new FakeFileStorage();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId, CustomerCode = "CUST001" };
        var repo = new SiteAttendanceRepository(db, fileStorage, tenant, new SiteMarginRepository(db));

        var trade = new Trade { TradeCode = $"TRD-{Guid.NewGuid():N}"[..10], TradeName = "Plumbing" };
        db.Trades.Add(trade);
        await db.SaveChangesAsync();

        var site = new SiteJob { SiteCode = $"SIT-{Guid.NewGuid():N}"[..10], SiteName = "Hospital Wing", Status = SiteStatus.Active };
        var laborer = new Laborer { FullName = "Anil", TradeId = trade.TradeId, Phone = "9876543212", IsActive = true };
        db.Sites.Add(site);
        db.Laborers.Add(laborer);
        await db.SaveChangesAsync();

        var assignment = new SiteAssignment
        {
            SiteId = site.SiteId,
            LaborerId = laborer.LaborerId,
            TradeId = trade.TradeId,
            StartDate = new DateOnly(2026, 10, 1),
            Status = AssignmentStatus.Active,
        };
        db.SiteAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var today = new DateOnly(2026, 10, 9);
        await repo.CheckInAsync(assignment.SiteAssignmentId, 12.0m, 77.0m, laborer.LaborerId, today);

        // Second check-in on the same day must throw InvalidOperationException
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repo.CheckInAsync(assignment.SiteAssignmentId, 12.0m, 77.0m, laborer.LaborerId, today));

        Assert.Contains("already recorded", ex.Message);
    }

    [SkippableFact]
    public async Task Laborer_cannot_check_in_to_another_laborers_assignment()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var customerId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        await using var db = _postgres.CreateContext(customerId, orgId);
        var fileStorage = new FakeFileStorage();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId, CustomerCode = "CUST001" };
        var repo = new SiteAttendanceRepository(db, fileStorage, tenant, new SiteMarginRepository(db));

        var trade = new Trade { TradeCode = $"TRD-{Guid.NewGuid():N}"[..10], TradeName = "Electrical" };
        db.Trades.Add(trade);
        await db.SaveChangesAsync();

        var site = new SiteJob { SiteCode = $"SIT-{Guid.NewGuid():N}"[..10], SiteName = "Office Tower", Status = SiteStatus.Active };
        var laborer = new Laborer { FullName = "Kiran", TradeId = trade.TradeId, Phone = "9876543213", IsActive = true };
        db.Sites.Add(site);
        db.Laborers.Add(laborer);
        await db.SaveChangesAsync();

        var assignment = new SiteAssignment
        {
            SiteId = site.SiteId,
            LaborerId = laborer.LaborerId,
            TradeId = trade.TradeId,
            StartDate = new DateOnly(2026, 10, 1),
            Status = AssignmentStatus.Active,
        };
        db.SiteAssignments.Add(assignment);
        await db.SaveChangesAsync();

        // Laborer 99999 attempts to check in to Laborer's assignment
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            repo.CheckInAsync(assignment.SiteAssignmentId, 12.0m, 77.0m, expectedLaborerId: 99999, new DateOnly(2026, 10, 9)));
    }

    [SkippableFact]
    public async Task ClockOut_requires_photo_and_validates_day_fraction()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var customerId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        await using var db = _postgres.CreateContext(customerId, orgId);
        var fileStorage = new FakeFileStorage();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId, CustomerCode = "CUST001" };
        var repo = new SiteAttendanceRepository(db, fileStorage, tenant, new SiteMarginRepository(db));

        var trade = new Trade { TradeCode = $"TRD-{Guid.NewGuid():N}"[..10], TradeName = "Painting" };
        db.Trades.Add(trade);
        await db.SaveChangesAsync();

        var site = new SiteJob { SiteCode = $"SIT-{Guid.NewGuid():N}"[..10], SiteName = "Villa Project", Status = SiteStatus.Active };
        var laborer = new Laborer { FullName = "Prakash", TradeId = trade.TradeId, Phone = "9876543214", IsActive = true };
        db.Sites.Add(site);
        db.Laborers.Add(laborer);
        await db.SaveChangesAsync();

        var assignment = new SiteAssignment
        {
            SiteId = site.SiteId,
            LaborerId = laborer.LaborerId,
            TradeId = trade.TradeId,
            StartDate = new DateOnly(2026, 10, 1),
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
            CheckInTime = DateTime.UtcNow,
            Status = AttendanceStatus.CheckedIn,
        };
        db.SiteAttendances.Add(attendance);
        await db.SaveChangesAsync();

        // 1. Clock out without photos must fail
        await Assert.ThrowsAsync<ArgumentException>(() =>
            repo.ClockOutAsync(
                attendanceId: attendance.AttendanceId,
                dayFraction: 1.0m,
                otHoursClaimed: 0m,
                workSummary: "Finished painting ground floor",
                photos: new List<UploadedPhotoFile>(),
                expectedLaborerId: laborer.LaborerId));

        // 2. Clock out with invalid day fraction (e.g., 0.75) must fail per D-61
        var photo = new UploadedPhotoFile("proof.jpg", "image/jpeg", 1024, new MemoryStream(Encoding.UTF8.GetBytes("image data")));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            repo.ClockOutAsync(
                attendanceId: attendance.AttendanceId,
                dayFraction: 0.75m,
                otHoursClaimed: 0m,
                workSummary: "Finished painting ground floor",
                photos: new List<UploadedPhotoFile> { photo },
                expectedLaborerId: laborer.LaborerId));

        // 3. Valid clock out with 0.5 day fraction and photo succeeds and transitions to Submitted
        var photoStream = new MemoryStream(Encoding.UTF8.GetBytes("image data"));
        var validPhoto = new UploadedPhotoFile("proof.jpg", "image/jpeg", 1024, photoStream);
        var updated = await repo.ClockOutAsync(
            attendanceId: attendance.AttendanceId,
            dayFraction: 0.5m,
            otHoursClaimed: 2.5m,
            workSummary: "Completed half day painting and OT",
            photos: new List<UploadedPhotoFile> { validPhoto },
            expectedLaborerId: laborer.LaborerId);

        Assert.Equal(AttendanceStatus.Submitted, updated.Status);
        Assert.Equal(0.5m, updated.DayFraction);
        Assert.Equal(2.5m, updated.OtHoursClaimed);
        Assert.NotNull(updated.ClockOutTime);
        Assert.Single(fileStorage.Files);
    }
}
