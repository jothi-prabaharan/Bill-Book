using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Audit;

/// <summary>One row of the edit log as a History tab or the audit-log report shows it.</summary>
public sealed class EditLogEntryView
{
    public long EditLogId { get; set; }

    public string EntityName { get; set; } = null!;

    public string EntityKey { get; set; } = null!;

    public string RootEntityName { get; set; } = null!;

    public string RootEntityKey { get; set; } = null!;

    public string Operation { get; set; } = null!;

    /// <summary><c>{ "Column": { "old": …, "new": … } }</c>.</summary>
    public JsonElement Changes { get; set; }

    public Guid? UserId { get; set; }

    public string? UserName { get; set; }

    public DateTimeOffset ChangedAt { get; set; }

    public string? RequestId { get; set; }

    public string? Source { get; set; }
}

/// <summary>A user who appears in the edit log.</summary>
public sealed class EditLogUserView
{
    public Guid UserId { get; set; }

    public string? UserName { get; set; }
}

/// <summary>A page of the audit-log report.</summary>
public sealed class EditLogPage
{
    public List<EditLogEntryView> Rows { get; set; } = [];

    public int Total { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}

/// <summary>
/// Reads a service's edit log (TK-118): the history of one document or master,
/// and the audit-log report filtered by date, user and table. Read-only, and
/// through the query filter like everything else, so a branch sees its own log.
/// </summary>
public static class EditLogReader
{
    public const int MaxHistory = 1000;

    public const int MaxPageSize = 200;

    /// <summary>Everything logged against a document or master and its lines, newest first.</summary>
    public static async Task<List<EditLogEntryView>> HistoryAsync(
        TenantDbContext db, string rootEntity, string rootKey, CancellationToken ct)
    {
        List<EditLog> rows = await db.EditLogs.AsNoTracking()
            .Where(e => e.RootEntityName == rootEntity && e.RootEntityKey == rootKey)
            .OrderByDescending(e => e.ChangedAt)
            .ThenByDescending(e => e.EditLogId)
            .Take(MaxHistory)
            .ToListAsync(ct);

        return [.. rows.Select(View)];
    }

    public static async Task<EditLogPage> SearchAsync(
        TenantDbContext db,
        DateTimeOffset? from,
        DateTimeOffset? to,
        Guid? userId,
        string? entityName,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        IQueryable<EditLog> query = db.EditLogs.AsNoTracking();
        if (from is DateTimeOffset start)
        {
            query = query.Where(e => e.ChangedAt >= start);
        }

        if (to is DateTimeOffset end)
        {
            query = query.Where(e => e.ChangedAt < end);
        }

        if (userId is Guid user)
        {
            query = query.Where(e => e.UserId == user);
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            string table = entityName.Trim();
            query = query.Where(e => e.EntityName == table || e.RootEntityName == table);
        }

        int total = await query.CountAsync(ct);
        List<EditLog> rows = await query
            .OrderByDescending(e => e.ChangedAt)
            .ThenByDescending(e => e.EditLogId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new EditLogPage { Rows = [.. rows.Select(View)], Total = total, Page = page, PageSize = pageSize };
    }

    /// <summary>The tables this service has logged, for the report's table filter.</summary>
    public static Task<List<string>> EntityNamesAsync(TenantDbContext db, CancellationToken ct) =>
        db.EditLogs.AsNoTracking().Select(e => e.EntityName).Distinct().OrderBy(n => n).ToListAsync(ct);

    /// <summary>
    /// Everyone this service has logged a change for, with the name the most
    /// recent row carries — for the report's user filter, without asking Master.
    /// </summary>
    public static async Task<List<EditLogUserView>> UsersAsync(TenantDbContext db, CancellationToken ct)
    {
        var rows = await db.EditLogs.AsNoTracking()
            .Where(e => e.UserId != null)
            .GroupBy(e => e.UserId)
            .Select(g => new
            {
                UserId = g.Key!.Value,
                UserName = g.OrderByDescending(e => e.ChangedAt).Select(e => e.UserName).FirstOrDefault(),
            })
            .ToListAsync(ct);

        return [.. rows
            .Select(r => new EditLogUserView { UserId = r.UserId, UserName = r.UserName })
            .OrderBy(r => r.UserName ?? string.Empty, StringComparer.OrdinalIgnoreCase)];
    }

    private static EditLogEntryView View(EditLog e) => new()
    {
        EditLogId = e.EditLogId,
        EntityName = e.EntityName,
        EntityKey = e.EntityKey,
        RootEntityName = e.RootEntityName,
        RootEntityKey = e.RootEntityKey,
        Operation = e.Operation.ToString(),
        Changes = e.Changes.RootElement.Clone(),
        UserId = e.UserId,
        UserName = e.UserName,
        ChangedAt = e.ChangedAt,
        RequestId = e.RequestId,
        Source = e.Source,
    };
}
