using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Storage;
using Shared.Kernel.Tenancy;
using Site.Entity.Enums;
using Site.Entity.TableEntities;

namespace Site.Repository.Repositories;

public sealed record LaborerPortalAssignmentDto(
    long AssignmentId,
    long SiteId,
    string SiteCode,
    string SiteName,
    string Address,
    decimal? Latitude,
    decimal? Longitude,
    int RadiusMeters,
    long LaborerId,
    string LaborerName,
    long TradeId,
    string TradeName,
    DateOnly StartDate,
    DateOnly? EndDate,
    SiteAttendanceSummaryDto? TodayAttendance);

public sealed record SiteAttendanceSummaryDto(
    long AttendanceId,
    DateOnly WorkDate,
    DateTime CheckInTime,
    DateTime? ClockOutTime,
    AttendanceStatus Status,
    bool IsGeofenceBreached,
    decimal DayFraction,
    decimal OtHoursClaimed);

public sealed record SiteAttendanceDetailDto(
    long AttendanceId,
    long AssignmentId,
    long SiteId,
    string SiteName,
    long LaborerId,
    string LaborerName,
    DateOnly WorkDate,
    DateTime CheckInTime,
    decimal? CheckInLat,
    decimal? CheckInLng,
    bool IsGeofenceBreached,
    DateTime? ClockOutTime,
    decimal DayFraction,
    decimal OtHoursClaimed,
    decimal OtHoursApproved,
    string? WorkSummary,
    AttendanceStatus Status,
    string? RejectionReason,
    List<SiteAttendancePhotoDto> Photos);

public sealed record SiteAttendancePhotoDto(
    long PhotoId,
    long AttendanceId,
    string FileName,
    string ContentType,
    long FileSize,
    DateTime UploadedAt);

public sealed record UploadedPhotoFile(
    string FileName,
    string ContentType,
    long FileSize,
    Stream Content);

public class SiteAttendanceRepository
{
    private readonly SiteDbContext _db;
    private readonly IFileStorage _fileStorage;
    private readonly ITenantContext _tenantContext;
    private readonly SiteMarginRepository _marginRepo;

    public SiteAttendanceRepository(
        SiteDbContext db,
        IFileStorage fileStorage,
        ITenantContext tenantContext,
        SiteMarginRepository marginRepo)
    {
        _db = db;
        _fileStorage = fileStorage;
        _tenantContext = tenantContext;
        _marginRepo = marginRepo;
    }

    /// <summary>
    /// Looks up a laborer linked to the user's account (TK-171).
    /// </summary>
    public async Task<Laborer?> GetLaborerByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.Laborers
            .FirstOrDefaultAsync(l => l.UserId == userId && l.IsActive, ct);
    }

    /// <summary>
    /// Returns the active assignments for a laborer, including site and trade information
    /// and today's attendance record if already recorded.
    /// </summary>
    public async Task<List<LaborerPortalAssignmentDto>> GetLaborerActiveAssignmentsAsync(
        long laborerId,
        DateOnly today,
        CancellationToken ct = default)
    {
        var assignments = await (
            from a in _db.SiteAssignments
            join s in _db.Sites on a.SiteId equals s.SiteId
            join l in _db.Laborers on a.LaborerId equals l.LaborerId
            join t in _db.Trades on a.TradeId equals t.TradeId
            where a.LaborerId == laborerId
               && a.Status == AssignmentStatus.Active
               && a.StartDate <= today
               && (a.EndDate == null || a.EndDate >= today)
               && s.Status == SiteStatus.Active
            select new
            {
                Assignment = a,
                Site = s,
                Laborer = l,
                Trade = t
            })
            .ToListAsync(ct);

        var assignmentIds = assignments.Select(x => x.Assignment.SiteAssignmentId).ToList();

        var todayAttendances = await _db.SiteAttendances
            .Where(att => assignmentIds.Contains(att.AssignmentId) && att.WorkDate == today)
            .ToListAsync(ct);

        var result = new List<LaborerPortalAssignmentDto>();

        foreach (var item in assignments)
        {
            var att = todayAttendances.FirstOrDefault(a => a.AssignmentId == item.Assignment.SiteAssignmentId);
            SiteAttendanceSummaryDto? summary = null;
            if (att != null)
            {
                summary = new SiteAttendanceSummaryDto(
                    att.AttendanceId,
                    att.WorkDate,
                    att.CheckInTime,
                    att.ClockOutTime,
                    att.Status,
                    att.IsGeofenceBreached,
                    att.DayFraction,
                    att.OtHoursClaimed);
            }

            result.Add(new LaborerPortalAssignmentDto(
                item.Assignment.SiteAssignmentId,
                item.Site.SiteId,
                item.Site.SiteCode,
                item.Site.SiteName,
                item.Site.Address,
                item.Site.Latitude,
                item.Site.Longitude,
                item.Site.GeofenceRadiusMeters,
                item.Laborer.LaborerId,
                item.Laborer.FullName,
                item.Trade.TradeId,
                item.Trade.TradeName,
                item.Assignment.StartDate,
                item.Assignment.EndDate,
                summary));
        }

        return result;
    }

    /// <summary>
    /// Performs check-in for an assignment. Refuses if attendance already exists for today.
    /// Evaluates geofence per D-63: accepts outside radius but flags IsGeofenceBreached = true.
    /// </summary>
    public async Task<SiteAttendance> CheckInAsync(
        long assignmentId,
        decimal? latitude,
        decimal? longitude,
        long? expectedLaborerId,
        DateOnly today,
        CancellationToken ct = default)
    {
        var assignment = await _db.SiteAssignments
            .FirstOrDefaultAsync(a => a.SiteAssignmentId == assignmentId, ct);

        if (assignment == null)
        {
            throw new InvalidOperationException("Assignment not found.");
        }

        if (expectedLaborerId.HasValue && assignment.LaborerId != expectedLaborerId.Value)
        {
            throw new UnauthorizedAccessException("You are not assigned to this assignment.");
        }

        if (assignment.Status != AssignmentStatus.Active)
        {
            throw new InvalidOperationException("This assignment is not active.");
        }

        // Check if attendance already exists for today on this assignment
        bool alreadyCheckedIn = await _db.SiteAttendances
            .AnyAsync(a => a.AssignmentId == assignmentId && a.WorkDate == today, ct);

        if (alreadyCheckedIn)
        {
            throw new InvalidOperationException("Attendance already recorded for this assignment today.");
        }

        var site = await _db.Sites.FirstOrDefaultAsync(s => s.SiteId == assignment.SiteId, ct);
        if (site == null)
        {
            throw new InvalidOperationException("Site not found.");
        }

        bool isGeofenceBreached = false;
        if (site.Latitude.HasValue && site.Longitude.HasValue && site.GeofenceRadiusMeters > 0)
        {
            if (!latitude.HasValue || !longitude.HasValue)
            {
                isGeofenceBreached = true;
            }
            else
            {
                double dist = CalculateDistanceMeters(
                    latitude.Value, longitude.Value,
                    site.Latitude.Value, site.Longitude.Value);

                if (dist > site.GeofenceRadiusMeters)
                {
                    isGeofenceBreached = true;
                }
            }
        }

        var attendance = new SiteAttendance
        {
            AssignmentId = assignmentId,
            SiteId = assignment.SiteId,
            LaborerId = assignment.LaborerId,
            WorkDate = today,
            CheckInTime = DateTime.UtcNow,
            CheckInLat = latitude,
            CheckInLng = longitude,
            IsGeofenceBreached = isGeofenceBreached,
            Status = AttendanceStatus.CheckedIn,
            DayFraction = 1.0m,
            OtHoursClaimed = 0m,
            OtHoursApproved = 0m,
        };

        _db.SiteAttendances.Add(attendance);
        await _db.SaveChangesAsync(ct);

        return attendance;
    }

    /// <summary>
    /// Clock-out: requires work summary, valid day fraction (0.5 or 1.0 per D-61),
    /// optional OT hours claimed, and at least one work-proof photo.
    /// Stores photos create-only via IFileStorage under StorageKey.
    /// Transitions status to Submitted.
    /// </summary>
    public async Task<SiteAttendance> ClockOutAsync(
        long attendanceId,
        decimal dayFraction,
        decimal otHoursClaimed,
        string workSummary,
        List<UploadedPhotoFile> photos,
        long? expectedLaborerId,
        CancellationToken ct = default)
    {
        if (dayFraction != 0.5m && dayFraction != 1.0m)
        {
            throw new ArgumentException("Day fraction must be 0.5 or 1.0.");
        }

        if (otHoursClaimed < 0 || otHoursClaimed > 24)
        {
            throw new ArgumentException("Overtime hours claimed must be between 0 and 24.");
        }

        if (string.IsNullOrWhiteSpace(workSummary))
        {
            throw new ArgumentException("Work summary is required.");
        }

        if (photos == null || photos.Count == 0)
        {
            throw new ArgumentException("At least one work-proof photo is required.");
        }

        var attendance = await _db.SiteAttendances
            .FirstOrDefaultAsync(a => a.AttendanceId == attendanceId, ct);

        if (attendance == null)
        {
            throw new InvalidOperationException("Attendance record not found.");
        }

        if (expectedLaborerId.HasValue && attendance.LaborerId != expectedLaborerId.Value)
        {
            throw new UnauthorizedAccessException("You are not authorized for this attendance record.");
        }

        if (attendance.Status != AttendanceStatus.CheckedIn)
        {
            throw new InvalidOperationException($"Cannot clock out attendance in {attendance.Status} status.");
        }

        StorageScope scope = StorageScope.For(_tenantContext, StorageApp.RetailErp, StorageModule.Site);

        foreach (var photoFile in photos)
        {
            string key = StorageKey.BuildKey(scope, "attendance", attendance.AttendanceId, photoFile.FileName);
            await _fileStorage.SaveAsync(key, photoFile.Content, photoFile.ContentType, FileWriteMode.CreateNew, ct);

            var photoEntity = new SiteAttendancePhoto
            {
                AttendanceId = attendance.AttendanceId,
                StorageKey = key,
                FileName = photoFile.FileName,
                ContentType = photoFile.ContentType,
                FileSize = photoFile.FileSize,
                UploadedAt = DateTime.UtcNow,
            };

            _db.SiteAttendancePhotos.Add(photoEntity);
        }

        attendance.ClockOutTime = DateTime.UtcNow;
        attendance.DayFraction = dayFraction;
        attendance.OtHoursClaimed = otHoursClaimed;
        attendance.WorkSummary = workSummary;
        attendance.Status = AttendanceStatus.Submitted;

        await _db.SaveChangesAsync(ct);
        return attendance;
    }

    /// <summary>
    /// Gets attendance details including photos.
    /// </summary>
    public async Task<SiteAttendanceDetailDto?> GetAttendanceDetailsAsync(
        long attendanceId,
        long? expectedLaborerId,
        CancellationToken ct = default)
    {
        var att = await (
            from a in _db.SiteAttendances
            join s in _db.Sites on a.SiteId equals s.SiteId
            join l in _db.Laborers on a.LaborerId equals l.LaborerId
            where a.AttendanceId == attendanceId
            select new
            {
                Attendance = a,
                SiteName = s.SiteName,
                LaborerName = l.FullName
            })
            .FirstOrDefaultAsync(ct);

        if (att == null)
        {
            return null;
        }

        if (expectedLaborerId.HasValue && att.Attendance.LaborerId != expectedLaborerId.Value)
        {
            throw new UnauthorizedAccessException("You are not authorized for this attendance record.");
        }

        var photos = await _db.SiteAttendancePhotos
            .Where(p => p.AttendanceId == attendanceId)
            .Select(p => new SiteAttendancePhotoDto(
                p.PhotoId,
                p.AttendanceId,
                p.FileName,
                p.ContentType,
                p.FileSize,
                p.UploadedAt))
            .ToListAsync(ct);

        return new SiteAttendanceDetailDto(
            att.Attendance.AttendanceId,
            att.Attendance.AssignmentId,
            att.Attendance.SiteId,
            att.SiteName,
            att.Attendance.LaborerId,
            att.LaborerName,
            att.Attendance.WorkDate,
            att.Attendance.CheckInTime,
            att.Attendance.CheckInLat,
            att.Attendance.CheckInLng,
            att.Attendance.IsGeofenceBreached,
            att.Attendance.ClockOutTime,
            att.Attendance.DayFraction,
            att.Attendance.OtHoursClaimed,
            att.Attendance.OtHoursApproved,
            att.Attendance.WorkSummary,
            att.Attendance.Status,
            att.Attendance.RejectionReason,
            photos);
    }

    /// <summary>
    /// Gets a photo entity for reading.
    /// </summary>
    public async Task<SiteAttendancePhoto?> GetPhotoAsync(long photoId, CancellationToken ct = default)
    {
        return await _db.SiteAttendancePhotos.FirstOrDefaultAsync(p => p.PhotoId == photoId, ct);
    }

    /// <summary>
    /// Opens stream to a photo from storage.
    /// </summary>
    public async Task<Stream?> OpenPhotoReadAsync(string storageKey, CancellationToken ct = default)
    {
        return await _fileStorage.OpenReadAsync(storageKey, ct);
    }

    public static double CalculateDistanceMeters(decimal lat1, decimal lng1, decimal lat2, decimal lng2)
    {
        const double r = 6371000.0;
        double phi1 = (double)lat1 * Math.PI / 180.0;
        double phi2 = (double)lat2 * Math.PI / 180.0;
        double deltaPhi = ((double)lat2 - (double)lat1) * Math.PI / 180.0;
        double deltaLambda = ((double)lng2 - (double)lng1) * Math.PI / 180.0;

        double a = Math.Sin(deltaPhi / 2.0) * Math.Sin(deltaPhi / 2.0) +
                   Math.Cos(phi1) * Math.Cos(phi2) *
                   Math.Sin(deltaLambda / 2.0) * Math.Sin(deltaLambda / 2.0);
        double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return r * c;
    }

    /// <summary>
    /// Lists all submitted attendance records awaiting admin approval (TK-172).
    /// </summary>
    public async Task<List<SiteAttendanceDetailDto>> ListPendingAttendanceAsync(
        long? siteId,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken ct = default)
    {
        var query = from a in _db.SiteAttendances
                    join s in _db.Sites on a.SiteId equals s.SiteId
                    join l in _db.Laborers on a.LaborerId equals l.LaborerId
                    where a.Status == AttendanceStatus.Submitted
                    select new { Attendance = a, SiteName = s.SiteName, LaborerName = l.FullName };

        if (siteId.HasValue)
        {
            query = query.Where(x => x.Attendance.SiteId == siteId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(x => x.Attendance.WorkDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(x => x.Attendance.WorkDate <= toDate.Value);
        }

        var list = await query.ToListAsync(ct);
        var attIds = list.Select(x => x.Attendance.AttendanceId).ToList();

        var photos = await _db.SiteAttendancePhotos
            .Where(p => attIds.Contains(p.AttendanceId))
            .Select(p => new SiteAttendancePhotoDto(
                p.PhotoId,
                p.AttendanceId,
                p.FileName,
                p.ContentType,
                p.FileSize,
                p.UploadedAt))
            .ToListAsync(ct);

        var photosByAtt = photos.GroupBy(p => p.AttendanceId).ToDictionary(g => g.Key, g => g.ToList());

        return list.Select(x => new SiteAttendanceDetailDto(
            x.Attendance.AttendanceId,
            x.Attendance.AssignmentId,
            x.Attendance.SiteId,
            x.SiteName,
            x.Attendance.LaborerId,
            x.LaborerName,
            x.Attendance.WorkDate,
            x.Attendance.CheckInTime,
            x.Attendance.CheckInLat,
            x.Attendance.CheckInLng,
            x.Attendance.IsGeofenceBreached,
            x.Attendance.ClockOutTime,
            x.Attendance.DayFraction,
            x.Attendance.OtHoursClaimed,
            x.Attendance.OtHoursApproved,
            x.Attendance.WorkSummary,
            x.Attendance.Status,
            x.Attendance.RejectionReason,
            photosByAtt.TryGetValue(x.Attendance.AttendanceId, out var ph) ? ph : new List<SiteAttendancePhotoDto>()
        )).ToList();
    }

    /// <summary>
    /// Admin approval of clock-out: freezes day and OT cost, margin and sell snapshots,
    /// increments ApprovedDays and ApprovedOtHours on SiteAssignment in one transaction (TK-172).
    /// </summary>
    public async Task<SiteAttendance> ApproveClockOutAsync(
        long attendanceId,
        decimal? otHoursApproved,
        long? approvedByUserId,
        CancellationToken ct = default)
    {
        var attendance = await _db.SiteAttendances
            .FirstOrDefaultAsync(a => a.AttendanceId == attendanceId, ct);

        if (attendance == null)
        {
            throw new InvalidOperationException("Attendance record not found.");
        }

        if (attendance.Status != AttendanceStatus.Submitted)
        {
            throw new InvalidOperationException($"Cannot approve attendance in '{attendance.Status}' status. Only Submitted attendances can be approved.");
        }

        var assignment = await _db.SiteAssignments
            .FirstOrDefaultAsync(a => a.SiteAssignmentId == attendance.AssignmentId, ct);
        if (assignment == null)
        {
            throw new InvalidOperationException("Site assignment not found.");
        }

        var laborer = await _db.Laborers
            .FirstOrDefaultAsync(l => l.LaborerId == attendance.LaborerId, ct);
        var trade = await _db.Trades
            .FirstOrDefaultAsync(t => t.TradeId == assignment.TradeId, ct);

        decimal approvedOt = otHoursApproved ?? attendance.OtHoursClaimed;
        if (approvedOt < 0 || approvedOt > attendance.OtHoursClaimed)
        {
            throw new ArgumentException("Approved OT hours must be between 0 and claimed OT hours.");
        }

        // Resolve rates
        var rates = SiteMarginRepository.ResolveRates(assignment, laborer, trade);

        // Resolve day margin
        var laborRule = await _marginRepo.ResolveRuleAsync(
            assignment.SiteAssignmentId,
            attendance.SiteId,
            trade?.TradeId,
            null,
            MarginAppliesTo.Labor,
            attendance.WorkDate,
            ct);

        var laborCalc = SiteMarginRepository.Calculate(laborRule, rates.CostRatePerDay);
        decimal dayMargin;
        decimal daySell;
        if (rates.SellRatePerDay > 0 && laborRule == null)
        {
            daySell = rates.SellRatePerDay;
            dayMargin = daySell - rates.CostRatePerDay;
        }
        else
        {
            dayMargin = laborCalc.MarginAmount;
            daySell = laborCalc.SellingPrice;
        }

        // Resolve OT margin
        var otRule = await _marginRepo.ResolveRuleAsync(
            assignment.SiteAssignmentId,
            attendance.SiteId,
            trade?.TradeId,
            null,
            MarginAppliesTo.Overtime,
            attendance.WorkDate,
            ct);

        var otCalc = SiteMarginRepository.Calculate(otRule, rates.OtCostRatePerHour);
        decimal otMargin;
        decimal otSell;
        if (rates.OtSellRatePerHour > 0 && otRule == null)
        {
            otSell = rates.OtSellRatePerHour;
            otMargin = otSell - rates.OtCostRatePerHour;
        }
        else
        {
            otMargin = otCalc.MarginAmount;
            otSell = otCalc.SellingPrice;
        }

        // Freeze snapshots on attendance
        attendance.DayCostSnapshot = rates.CostRatePerDay;
        attendance.DayMarginSnapshot = dayMargin;
        attendance.DaySellSnapshot = daySell;
        attendance.OtCostSnapshot = rates.OtCostRatePerHour;
        attendance.OtMarginSnapshot = otMargin;
        attendance.OtSellSnapshot = otSell;
        attendance.OtHoursApproved = approvedOt;
        attendance.ApprovedAt = DateTime.UtcNow;
        attendance.ApprovedBy = approvedByUserId;
        attendance.Status = AttendanceStatus.Approved;

        // In one transaction: increment counters on assignment
        assignment.ApprovedDays += attendance.DayFraction;
        assignment.ApprovedOtHours += attendance.OtHoursApproved;

        await _db.SaveChangesAsync(ct);
        return attendance;
    }

    /// <summary>
    /// Rejects clock-out with a reason. Counters on assignment are not incremented (TK-172).
    /// </summary>
    public async Task<SiteAttendance> RejectClockOutAsync(
        long attendanceId,
        string rejectionReason,
        long? rejectedByUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            throw new ArgumentException("Rejection reason is required.");
        }

        var attendance = await _db.SiteAttendances
            .FirstOrDefaultAsync(a => a.AttendanceId == attendanceId, ct);

        if (attendance == null)
        {
            throw new InvalidOperationException("Attendance record not found.");
        }

        if (attendance.Status != AttendanceStatus.Submitted)
        {
            throw new InvalidOperationException($"Cannot reject attendance in '{attendance.Status}' status. Only Submitted attendances can be rejected.");
        }

        attendance.Status = AttendanceStatus.Rejected;
        attendance.RejectionReason = rejectionReason;
        attendance.ApprovedBy = rejectedByUserId;
        attendance.ApprovedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return attendance;
    }
}
