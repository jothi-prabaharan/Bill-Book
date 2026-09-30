using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Audit;

/// <summary>
/// The edit-log routes every tenant service serves under
/// <c>api/edit-log/{service}</c> (TK-118): a document's history and the
/// audit-log report. A service's controller derives from this, names its route
/// and its module permission, and hands over its context — nothing else.
/// </summary>
public abstract class EditLogControllerBase<TContext> : ControllerBase
    where TContext : TenantDbContext
{
    private readonly TContext _db;

    protected EditLogControllerBase(TContext db) => _db = db;

    /// <summary>The history of one document or master, its lines included.</summary>
    [HttpGet("history/{entity}/{key}")]
    public async Task<IActionResult> History(string entity, string key, CancellationToken ct) =>
        Ok(await EditLogReader.HistoryAsync(_db, entity, key, ct));

    /// <summary>The audit-log report: newest first, filtered by date, user and table.</summary>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? userId,
        [FromQuery] string? entity,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await EditLogReader.SearchAsync(_db, from, to, userId, entity, page, pageSize, ct));

    [HttpGet("entities")]
    public async Task<IActionResult> Entities(CancellationToken ct) =>
        Ok(await EditLogReader.EntityNamesAsync(_db, ct));

    [HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken ct) =>
        Ok(await EditLogReader.UsersAsync(_db, ct));
}
