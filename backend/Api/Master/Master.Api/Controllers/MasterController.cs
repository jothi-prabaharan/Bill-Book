using Master.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Master.Api.Controllers;

/// <summary>Public reference data for the signup and settings screens.</summary>
[ApiController]
[Route("api/master")]
public sealed class MasterController : ControllerBase
{
    private readonly MasterDbContext _db;

    public MasterController(MasterDbContext db) => _db = db;

    [HttpGet("countries")]
    public async Task<IActionResult> GetCountries(CancellationToken ct)
    {
        var countries = await _db.Countries
            .Where(c => c.IsActive)
            .OrderBy(c => c.CountryName)
            .Select(c => new { c.CountryId, c.CountryCode, c.CountryName, c.CurrencyCode, c.PhoneCode })
            .ToListAsync(ct);
        return Ok(countries);
    }

    [HttpGet("countries/{countryId:int}/states")]
    public async Task<IActionResult> GetStates(int countryId, CancellationToken ct)
    {
        var states = await _db.States
            .Where(s => s.CountryId == countryId && s.IsActive)
            .OrderBy(s => s.StateName)
            .Select(s => new { s.StateId, s.StateCode, s.StateName })
            .ToListAsync(ct);
        return Ok(states);
    }

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

    /// <summary>
    /// Read-only: transaction types are product-defined seed data. New types
    /// arrive by EF migration, never at runtime, so there is no write endpoint.
    /// </summary>
    [HttpGet("transaction-types")]
    public async Task<IActionResult> GetTransactionTypes([FromQuery] bool? postingOnly, CancellationToken ct)
    {
        var query = _db.TransactionTypes.Where(t => t.IsActive);
        if (postingOnly == true)
        {
            query = query.Where(t => t.IsLedgerPosting);
        }

        var types = await query
            .OrderBy(t => t.Name)
            .Select(t => new { t.Code, t.Name, t.IsLedgerPosting })
            .ToListAsync(ct);
        return Ok(types);
    }

    [HttpGet("transaction-types/{code}")]
    public async Task<IActionResult> GetTransactionType(string code, CancellationToken ct)
    {
        var type = await _db.TransactionTypes
            .Where(t => t.Code == code.ToUpperInvariant())
            .Select(t => new { t.Code, t.Name, t.IsLedgerPosting, t.IsActive })
            .FirstOrDefaultAsync(ct);
        return type is null ? NotFound() : Ok(type);
    }
}
