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
}
