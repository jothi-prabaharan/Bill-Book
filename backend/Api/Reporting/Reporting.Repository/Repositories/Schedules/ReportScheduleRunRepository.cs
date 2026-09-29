using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Entity.TableEntities;
using Reporting.Repository;
using Shared.Kernel.Errors;
using Shared.Kernel.Internal;

namespace Reporting.Repository.Repositories;

/// <summary>What one branch's pass did.</summary>
public sealed record ScheduleRunResult(int Due, int Claimed, int Sent, int Skipped, int Failed);

/// <summary>
/// One branch's due schedules, run once each (TK-114). Scoped: the worker
/// makes one per branch with that branch's tenant set.
///
/// <b>Claim, then send.</b> A schedule is claimed by moving its
/// <see cref="ReportSchedule.NextRunAt"/> to the next slot with an update
/// guarded on the value just read; only the pass whose update changed the row
/// sends, so two replicas ticking together send one email. The cost of that
/// order is that a crash between claim and send loses that one run rather
/// than sending it twice, which for a report that comes again next week is
/// the right way round.
/// </summary>
public sealed class ReportScheduleRunRepository
{
    public const string WorkerName = "ReportScheduleWorker";

    /// <summary>A pass takes at most this many; the rest wait for the next tick.</summary>
    public const int BatchSize = 50;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ReportingDbContext _db;
    private readonly ReportCatalogRepository _catalog;
    private readonly IBranchUsers _users;
    private readonly IScheduledReportExporter _exporter;
    private readonly IReportMailer _mailer;
    private readonly IWorkerErrorAuditor? _auditor;

    public ReportScheduleRunRepository(
        ReportingDbContext db,
        ReportCatalogRepository catalog,
        IBranchUsers users,
        IScheduledReportExporter exporter,
        IReportMailer mailer,
        IWorkerErrorAuditor? auditor = null)
    {
        _db = db;
        _catalog = catalog;
        _users = users;
        _exporter = exporter;
        _mailer = mailer;
        _auditor = auditor;
    }

    public async Task<ScheduleRunResult> RunDueAsync(DateTimeOffset now, CancellationToken ct)
    {
        List<ReportSchedule> due = await _db.ReportSchedules.AsNoTracking()
            .Where(s => s.IsActive && s.NextRunAt <= now)
            .OrderBy(s => s.NextRunAt)
            .ThenBy(s => s.ReportScheduleId)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (due.Count == 0)
        {
            return new ScheduleRunResult(0, 0, 0, 0, 0);
        }

        // Read before claiming anything: without Master there is no permission
        // to check, and a claimed run that cannot be checked is a lost run.
        IReadOnlyList<BranchUserAccess>? users = await _users.ListAsync(ct);

        if (users is null)
        {
            return new ScheduleRunResult(due.Count, 0, 0, 0, 0);
        }

        Dictionary<Guid, BranchUserAccess> byId = users.ToDictionary(u => u.UserId);
        int claimed = 0, sent = 0, skipped = 0, failed = 0;

        foreach (ReportSchedule schedule in due)
        {
            DateTimeOffset next = ReportScheduleCalendarRepository.NextRun(
                schedule.Frequency, schedule.DayOfWeek, schedule.DayOfMonth, schedule.SendAtHour, schedule.TimeZoneId, now);

            if (!await TryClaimAsync(_db, schedule.ReportScheduleId, schedule.NextRunAt, next, now, ct))
            {
                continue;
            }

            claimed++;
            (ReportScheduleRunStatus status, string message) = await DeliverAsync(schedule, byId, ct);

            switch (status)
            {
                case ReportScheduleRunStatus.Sent: sent++; break;
                case ReportScheduleRunStatus.Skipped: skipped++; break;
                default: failed++; break;
            }

            string recorded = message.Length > 500 ? message[..500] : message;
            await _db.ReportSchedules
                .Where(s => s.ReportScheduleId == schedule.ReportScheduleId)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(s => s.LastStatus, status)
                    .SetProperty(s => s.LastMessage, recorded), ct);
        }

        return new ScheduleRunResult(due.Count, claimed, sent, skipped, failed);
    }

    /// <summary>
    /// Takes one run of a schedule: true for exactly one caller however many
    /// try at once, because the update only matches while
    /// <see cref="ReportSchedule.NextRunAt"/> still holds the value they read.
    /// </summary>
    public static async Task<bool> TryClaimAsync(
        ReportingDbContext db, long scheduleId, DateTimeOffset expected, DateTimeOffset next, DateTimeOffset now, CancellationToken ct) =>
        await db.ReportSchedules
            .Where(s => s.ReportScheduleId == scheduleId && s.IsActive && s.NextRunAt == expected)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.NextRunAt, next)
                .SetProperty(s => s.LastRunAt, now), ct) == 1;

    private async Task<(ReportScheduleRunStatus, string)> DeliverAsync(
        ReportSchedule schedule, Dictionary<Guid, BranchUserAccess> users, CancellationToken ct)
    {
        if (!users.TryGetValue(schedule.OwnerUserId, out BranchUserAccess? owner))
        {
            return (ReportScheduleRunStatus.Skipped, "Nothing was sent: the person who scheduled it is no longer a user of this branch.");
        }

        HashSet<string> permissions = [.. owner.Permissions];

        if (!permissions.Contains("reports.view") || !permissions.Contains("reports.export"))
        {
            return (ReportScheduleRunStatus.Skipped, $"Nothing was sent: {owner.DisplayName} no longer has permission to export reports.");
        }

        if (_catalog.Source(schedule.ReportKey, permissions) is null)
        {
            return (ReportScheduleRunStatus.Skipped, $"Nothing was sent: {owner.DisplayName} no longer has permission to run this report.");
        }

        List<BranchUserAccess> recipients = [.. schedule.RecipientUserIds.Where(users.ContainsKey).Select(id => users[id])];

        if (recipients.Count == 0)
        {
            return (ReportScheduleRunStatus.Skipped, "Nothing was sent: none of the recipients is still a user of this branch.");
        }

        ReportQueryRequest layout = JsonSerializer.Deserialize<ReportQueryRequest>(schedule.LayoutJson, Json) ?? new();
        ScheduledExport export;

        try
        {
            export = await _exporter.ExportAsync(schedule.ReportKey, layout, permissions, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await AuditAsync(schedule, ex, ct);
            return (ReportScheduleRunStatus.Failed, "The report could not be run. The error is logged for follow-up.");
        }

        if (export.Workbook is not byte[] workbook)
        {
            return (ReportScheduleRunStatus.Failed, export.Problem ?? "The report could not be run.");
        }

        string file = FileName(schedule);
        List<string> failures = [];

        foreach (BranchUserAccess recipient in recipients)
        {
            try
            {
                await _mailer.SendAsync(new ReportMail(
                    recipient.Email, recipient.DisplayName, schedule.Title, Body(schedule, export.Rows), file, workbook), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add(recipient.DisplayName);
                await AuditAsync(schedule, ex, ct);
            }
        }

        int delivered = recipients.Count - failures.Count;
        string skippedNote = recipients.Count < schedule.RecipientUserIds.Count
            ? $" {schedule.RecipientUserIds.Count - recipients.Count} recipient(s) no longer in the branch were left out."
            : string.Empty;

        return failures.Count == 0
            ? (ReportScheduleRunStatus.Sent, $"Sent to {delivered} recipient(s), {export.Rows:N0} rows.{skippedNote}")
            : (ReportScheduleRunStatus.Failed, $"Sent to {delivered} of {recipients.Count}; could not send to {string.Join(", ", failures)}.{skippedNote}");
    }

    private async Task AuditAsync(ReportSchedule schedule, Exception ex, CancellationToken ct)
    {
        if (_auditor is not null)
        {
            await _auditor.AuditAsync(WorkerName, $"ReportSchedule:{schedule.ReportScheduleId}", ex, ct);
        }
    }

    /// <summary>The attachment's name: the title, safe for a file system, and the date.</summary>
    public static string FileName(ReportSchedule schedule)
    {
        string safe = new([.. schedule.Title.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_')]);
        safe = safe.Trim().Replace(' ', '-');
        return $"{(safe.Length == 0 ? schedule.ReportKey : safe)}-{DateTime.UtcNow:yyyy-MM-dd}.xlsx";
    }

    private static string Body(ReportSchedule schedule, long rows) =>
        $"<p>{WebUtility.HtmlEncode(schedule.Title)} is attached, {rows:N0} rows as at {DateTime.UtcNow:dd-MMM-yyyy HH:mm} UTC.</p>"
        + "<p>You receive this because you were chosen as a recipient of this scheduled report. "
        + "Its owner can change or stop it from the report's Schedule button.</p>";
}
