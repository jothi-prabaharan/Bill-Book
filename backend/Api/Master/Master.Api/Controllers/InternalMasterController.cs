using Master.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Internal;

namespace Master.Api.Controllers;

/// <summary>
/// The reference lookups other services need, guarded by the internal key.
///
/// These duplicate two actions on <see cref="MasterController"/>, and that is
/// deliberate: the same data is wanted by two callers holding two different
/// credentials. The browser has a user token and no internal key; Contacts,
/// Platform and the workers have the key and no user token. An endpoint that
/// accepted either would be a door with two locks and no way to reason about
/// who came through it.
///
/// Read-only, and only what a caller actually asks for. Countries, HSN codes and
/// the type masters are not here because nothing service-to-service reads them.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/master")]
public sealed class InternalMasterController : ControllerBase
{
    private readonly MasterDbContext _db;

    public InternalMasterController(MasterDbContext db) => _db = db;

    /// <summary>
    /// One state. Contacts validates a GSTIN's first two digits against this,
    /// and Platform validates a branch address — both across a database boundary
    /// where a foreign key is impossible.
    /// </summary>
    [HttpGet("states/{stateId:int}")]
    public async Task<IActionResult> GetState(int stateId, CancellationToken ct)
    {
        var state = await _db.States
            .Where(s => s.StateId == stateId)
            .Select(s => new { s.StateId, s.CountryId, s.StateCode, s.StateName, s.IsActive })
            .FirstOrDefaultAsync(ct);

        return state is null ? NotFound() : Ok(state);
    }

    /// <summary>
    /// Active currencies. Platform reads this while provisioning, to enable a new
    /// branch's base currency before anyone can sign in to it.
    /// </summary>
    [HttpGet("currencies")]
    public async Task<IActionResult> GetCurrencies(CancellationToken ct)
    {
        var currencies = await _db.Currencies
            .Where(c => c.IsActive)
            .OrderBy(c => c.Code)
            .Select(c => new { c.CurrencyId, c.Code, c.Name, c.Symbol, c.Format, c.DecimalPlaces, c.SymbolPosition })
            .ToListAsync(ct);

        return Ok(currencies);
    }
}
