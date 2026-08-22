using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Master.Api.Services;

/// <summary>
/// A state's GST code, by id.
///
/// Two callers need it and both are now in this service: an organization checks
/// its own GSTIN's first two digits against its state when a branch is saved,
/// and a contact does the same when one is saved. Getting it wrong sends
/// CGST + SGST where IGST belongs, and nothing complains until filing.
///
/// The contact side holds an unenforced StateId — the contact is in a
/// per-customer database and mst.States is in the master one, so a foreign key
/// is not possible across them and the id is validated in C# instead. That is
/// still true after the merge: one service, still two databases.
/// </summary>
public interface IStateDirectory
{
    /// <summary>The state's GST code, or null when the id is unknown.</summary>
    Task<string?> GetStateCodeAsync(int stateId, CancellationToken ct);
}

/// <summary>
/// Reads mst.States directly. This was two identical HTTP clients onto Master's
/// own internal state endpoint — one in Platform, one in Contacts — kept in step
/// by hand. Both callers live here now, so it is a query.
/// </summary>
public sealed class StateDirectory : IStateDirectory
{
    private readonly AdminDbContext _db;
    private readonly IMemoryCache _cache;

    public StateDirectory(AdminDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<string?> GetStateCodeAsync(int stateId, CancellationToken ct)
    {
        // Still cached, though it is no longer a network call: state codes are
        // legislated, and a branch or contact save should not spend a query on
        // something that changes when Parliament says so.
        if (_cache.TryGetValue($"state:{stateId}", out string? cached))
        {
            return cached;
        }

        string? code = await _db.States
            .Where(s => s.StateId == stateId)
            .Select(s => s.StateCode)
            .FirstOrDefaultAsync(ct);

        if (code is null)
        {
            return null;
        }

        _cache.Set($"state:{stateId}", code, TimeSpan.FromHours(12));
        return code;
    }
}
