using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Interfaces;

namespace Accounting.Api.Services;

/// <summary>
/// How far back the books are closed, and whether a given document date is
/// allowed through.
///
/// <b>One date per branch per role.</b> A close is rarely all-or-nothing: the
/// month shuts to everyone who keys documents while staying open to whoever has
/// to make the adjusting entries the close itself produced. A single date for the
/// whole branch would close the books on the person doing the closing.
///
/// <b>What it refuses is anything that reaches or leaves the ledger</b> — a post,
/// a reversal, a void — not the keying. A draft touches nothing, so a draft may
/// be written and edited with any date at all; it simply cannot be posted into a
/// closed period. Someone catching up on last month's paperwork can still type
/// it, and finds out at post rather than at the first field.
/// </summary>
public sealed class PeriodLockService
{
    private readonly AccountingDbContext _db;
    private readonly ICurrentUser _user;

    public PeriodLockService(AccountingDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    /// <summary>
    /// The date the calling user is locked up to, or null when nothing stops
    /// them.
    ///
    /// <b>A caller with no role gets the branch's strictest lock, not none.</b>
    /// That happens on a token minted before the role claim existed and on any
    /// future caller we cannot identify. Falling open would let a stale token
    /// post into a closed period for as long as it lives; falling to the
    /// strictest date is safe and still lets ordinary work through, because most
    /// branches lock every role to the same day anyway.
    /// </summary>
    public async Task<DateOnly?> LockedUptoAsync(CancellationToken ct)
    {
        if (_user.RoleId is int roleId)
        {
            return await _db.PeriodLocks
                .Where(p => p.RoleId == roleId)
                .Select(p => (DateOnly?)p.LockedUpto)
                .FirstOrDefaultAsync(ct);
        }

        return await _db.PeriodLocks
            .OrderByDescending(p => p.LockedUpto)
            .Select(p => (DateOnly?)p.LockedUpto)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Whether a document dated <paramref name="on"/> may reach the ledger.
    /// Returns the lock that refused it, so the caller can say which date it was
    /// rather than only that it failed.
    /// </summary>
    public async Task<DateOnly?> RefusedByAsync(DateOnly on, CancellationToken ct)
    {
        DateOnly? lockedUpto = await LockedUptoAsync(ct);

        // Inclusive: locked up to 31 March means 31 March itself is closed.
        return lockedUpto is DateOnly limit && on <= limit ? limit : null;
    }

    public async Task<IReadOnlyList<PeriodLockListItem>> ListAsync(CancellationToken ct) =>
        await _db.PeriodLocks
            .OrderBy(p => p.RoleId)
            .Select(p => new PeriodLockListItem
            {
                PeriodLockId = p.PeriodLockId,
                RoleId = p.RoleId,
                LockedUpto = p.LockedUpto,
                Note = p.Note,
            })
            .ToListAsync(ct);

    /// <summary>
    /// Sets or moves a role's lock. One row per role, so this is an upsert
    /// rather than an insert — the unique index would refuse a second row and
    /// the caller means "this role is now locked to this date" either way.
    ///
    /// <b>Moving the date backwards reopens a period</b>, and is deliberately
    /// allowed: a close made in error has to be undoable. It is an audited change
    /// like any other, which is what makes it accountable rather than free.
    /// </summary>
    public async Task<long> SetAsync(SavePeriodLockRequest request, CancellationToken ct)
    {
        PeriodLock? existing = await _db.PeriodLocks
            .FirstOrDefaultAsync(p => p.RoleId == request.RoleId, ct);

        if (existing is null)
        {
            var added = new PeriodLock
            {
                RoleId = request.RoleId,
                LockedUpto = request.LockedUpto,
                Note = request.Note,
            };

            _db.PeriodLocks.Add(added);
            await _db.SaveChangesAsync(ct);
            return added.PeriodLockId;
        }

        existing.LockedUpto = request.LockedUpto;
        existing.Note = request.Note;
        await _db.SaveChangesAsync(ct);
        return existing.PeriodLockId;
    }

    /// <summary>Removes a role's lock entirely, which reopens every period for it.</summary>
    public async Task<bool> ClearAsync(int roleId, CancellationToken ct) =>
        await _db.PeriodLocks.Where(p => p.RoleId == roleId).ExecuteDeleteAsync(ct) > 0;
}
