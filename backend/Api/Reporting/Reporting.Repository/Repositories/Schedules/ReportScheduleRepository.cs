using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Entity.TableEntities;
using Reporting.Repository;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;

namespace Reporting.Repository.Repositories;

/// <summary>
/// A person's report schedules (TK-114). A schedule is its owner's alone:
/// nobody else lists, changes or deletes it, and it runs with the owner's
/// permissions as they stand at each run.
/// </summary>
public sealed class ReportScheduleRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ReportingDbContext _db;
    private readonly ICurrentUser _user;
    private readonly ReportCatalogRepository _catalog;
    private readonly IBranchUsers _users;
    private readonly TimeProvider _clock;

    public ReportScheduleRepository(
        ReportingDbContext db, ICurrentUser user, ReportCatalogRepository catalog, IBranchUsers users, TimeProvider clock)
    {
        _db = db;
        _user = user;
        _catalog = catalog;
        _users = users;
        _clock = clock;
    }

    public async Task<List<ReportScheduleModel>> ListAsync(string? reportKey, CancellationToken ct)
    {
        Guid? me = _user.UserId;

        List<ReportSchedule> schedules = await _db.ReportSchedules
            .Where(s => s.OwnerUserId == me && (reportKey == null || s.ReportKey == reportKey))
            .OrderBy(s => s.Title)
            .ToListAsync(ct);

        return [.. schedules.Select(Model)];
    }

    /// <summary>The branch's users a schedule can go to. Null when Master could not be asked.</summary>
    public async Task<List<ReportRecipientView>?> RecipientsAsync(CancellationToken ct) =>
        await _users.ListAsync(ct) is { } users
            ? [.. users.Select(u => new ReportRecipientView { UserId = u.UserId, DisplayName = u.DisplayName })]
            : null;

    /// <summary>Null when the caller may not run the report, which the controller answers as not found.</summary>
    public async Task<ReportScheduleModel?> CreateAsync(ReportScheduleModel model, IReadOnlySet<string> permissions, CancellationToken ct)
    {
        if (_catalog.Source(model.ReportKey, permissions) is not IReportSourceRepository source || _user.UserId is not Guid me)
        {
            return null;
        }

        await CheckAsync(model, source, ct);

        ReportSchedule schedule = new()
        {
            ReportKey = source.ReportKey,
            OwnerUserId = me,
        };

        Apply(schedule, model);
        _db.ReportSchedules.Add(schedule);
        await _db.SaveChangesAsync(ct);

        return Model(schedule);
    }

    public async Task<ReportScheduleModel?> UpdateAsync(long id, ReportScheduleModel model, IReadOnlySet<string> permissions, CancellationToken ct)
    {
        Guid? me = _user.UserId;
        ReportSchedule? schedule = await _db.ReportSchedules
            .FirstOrDefaultAsync(s => s.ReportScheduleId == id && s.OwnerUserId == me, ct);

        if (schedule is null || _catalog.Source(schedule.ReportKey, permissions) is not IReportSourceRepository source)
        {
            return null;
        }

        await CheckAsync(model, source, ct);
        Apply(schedule, model);
        await _db.SaveChangesAsync(ct);

        return Model(schedule);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct)
    {
        Guid? me = _user.UserId;

        return await _db.ReportSchedules
            .Where(s => s.ReportScheduleId == id && s.OwnerUserId == me)
            .ExecuteDeleteAsync(ct) == 1;
    }

    /// <summary>The next slot is worked out again on every save, so a changed day takes effect from its next occurrence.</summary>
    private void Apply(ReportSchedule schedule, ReportScheduleModel model)
    {
        schedule.CustomReportId = model.CustomReportId;
        schedule.Title = model.Title.Trim();
        schedule.LayoutJson = JsonSerializer.Serialize(model.Layout, Json);
        schedule.Frequency = model.Frequency;
        schedule.DayOfWeek = model.Frequency == ReportScheduleFrequency.Weekly ? model.DayOfWeek ?? DayOfWeek.Monday : null;
        schedule.DayOfMonth = model.Frequency == ReportScheduleFrequency.Monthly ? model.DayOfMonth ?? 1 : null;
        schedule.SendAtHour = model.SendAtHour;
        schedule.TimeZoneId = string.IsNullOrWhiteSpace(model.TimeZoneId) ? ReportScheduleCalendarRepository.DefaultTimeZone : model.TimeZoneId;
        schedule.RecipientUserIds = [.. model.RecipientUserIds.Distinct()];
        schedule.IsActive = model.IsActive;
        schedule.NextRunAt = ReportScheduleCalendarRepository.NextRun(
            schedule.Frequency, schedule.DayOfWeek, schedule.DayOfMonth, schedule.SendAtHour, schedule.TimeZoneId, _clock.GetUtcNow());
    }

    private async Task CheckAsync(ReportScheduleModel model, IReportSourceRepository source, CancellationToken ct)
    {
        if (!Enum.IsDefined(model.Frequency))
        {
            throw new ReportQueryException("Choose daily, weekly or monthly.");
        }

        if (!string.IsNullOrWhiteSpace(model.TimeZoneId) && !ReportScheduleCalendarRepository.IsKnown(model.TimeZoneId))
        {
            throw new ReportQueryException($"'{model.TimeZoneId}' is not a time zone this server knows.");
        }

        if (model.RecipientUserIds.Count == 0)
        {
            throw new ReportQueryException("Choose at least one recipient.");
        }

        IReadOnlyList<BranchUserAccess> users = await _users.ListAsync(ct)
            ?? throw new ReportQueryException("The branch's users could not be checked just now. Try again in a moment.");

        HashSet<Guid> branch = [.. users.Select(u => u.UserId)];

        if (model.RecipientUserIds.Any(id => !branch.Contains(id)))
        {
            throw new ReportQueryException("Every recipient must be an active user of this branch.");
        }

        // The columns must be the report's now, custom field columns included.
        if (source is IDatasetSource dataset)
        {
            await dataset.PrepareColumnsAsync(ct);
        }

        ReportQueryRequest copy = JsonSerializer.Deserialize<ReportQueryRequest>(JsonSerializer.Serialize(model.Layout, Json), Json) ?? new();
        List<string> unknown = CustomReportRepository.Prune(copy, source.Columns.Select(c => c.Key).ToHashSet(StringComparer.Ordinal));

        if (unknown.Count > 0)
        {
            throw new ReportQueryException($"{source.Title} has no column {string.Join(", ", unknown)}.");
        }
    }

    private static ReportScheduleModel Model(ReportSchedule s) => new()
    {
        ReportScheduleId = s.ReportScheduleId,
        ReportKey = s.ReportKey,
        CustomReportId = s.CustomReportId,
        Title = s.Title,
        Frequency = s.Frequency,
        DayOfWeek = s.DayOfWeek,
        DayOfMonth = s.DayOfMonth,
        SendAtHour = s.SendAtHour,
        TimeZoneId = s.TimeZoneId,
        RecipientUserIds = [.. s.RecipientUserIds],
        IsActive = s.IsActive,
        Layout = JsonSerializer.Deserialize<ReportQueryRequest>(s.LayoutJson, Json) ?? new(),
        NextRunAt = s.NextRunAt,
        LastRunAt = s.LastRunAt,
        LastStatus = s.LastStatus,
        LastMessage = s.LastMessage,
    };
}
