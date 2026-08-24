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
    private readonly AdminDbContext _db;

    public InternalMasterController(AdminDbContext db) => _db = db;

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
    [HttpGet("account-types")]
    public async Task<IActionResult> GetAccountTypes(CancellationToken ct)
    {
        var types = await _db.AccountTypes
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .Select(t => new { t.AccountTypeId, t.SystemName, t.DisplayName, t.NormalBalance, t.ReportSection, t.SortOrder })
            .ToListAsync(ct);

        return Ok(types);
    }

    /// <summary>
    /// Every state. 37-odd rows and fixed, so a report resolving a warehouse's
    /// state id reads the whole table once and caches it, the same as account
    /// types, rather than one round trip per warehouse.
    /// </summary>
    [HttpGet("states")]
    public async Task<IActionResult> GetStates(CancellationToken ct)
    {
        var states = await _db.States
            .Where(s => s.IsActive)
            .OrderBy(s => s.StateName)
            .Select(s => new { s.StateId, s.CountryId, s.StateCode, s.StateName })
            .ToListAsync(ct);

        return Ok(states);
    }

    /// <summary>Every country. Same shape and the same reason as <see cref="GetStates"/>.</summary>
    [HttpGet("countries")]
    public async Task<IActionResult> GetCountries(CancellationToken ct)
    {
        var countries = await _db.Countries
            .Where(c => c.IsActive)
            .OrderBy(c => c.CountryName)
            .Select(c => new { c.CountryId, c.CountryCode, c.CountryName })
            .ToListAsync(ct);

        return Ok(countries);
    }
}
