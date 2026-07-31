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

    /// <summary>
    /// One state by id. Per-customer services hold an unenforced StateId and
    /// need its GST code to check a GSTIN's first two digits — a cross-database
    /// FK is impossible, so the check happens in C# against this.
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

    [HttpGet("ledger-types")]
    public async Task<IActionResult> GetLedgerTypes(CancellationToken ct)
    {
        var types = await _db.LedgerTypes
            .Where(t => t.IsActive)
            .OrderBy(t => t.LedgerTypeId)
            .Select(t => new { t.LedgerTypeId, t.Code, t.Name })
            .ToListAsync(ct);
        return Ok(types);
    }

    [HttpGet("ledger-sources")]
    public async Task<IActionResult> GetLedgerSources(CancellationToken ct)
    {
        var sources = await _db.LedgerSources
            .Where(s => s.IsActive)
            .OrderBy(s => s.LedgerSourceId)
            .Select(s => new { s.LedgerSourceId, s.Code, s.Name, s.Direction })
            .ToListAsync(ct);
        return Ok(sources);
    }

    /// <summary>
    /// HSN and SAC codes, searchable. Chapter rows (2 digits) are grouping
    /// headings and are excluded by default — an invoice line needs a real code.
    /// </summary>
    [HttpGet("hsn-sac")]
    public async Task<IActionResult> GetHsnSacCodes(
        [FromQuery] string? search,
        [FromQuery] string? codeType,
        [FromQuery] bool includeChapters,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        IQueryable<Entity.TableEntities.HsnSacCode> query = _db.HsnSacCodes.Where(c => c.IsActive);

        if (!includeChapters)
        {
            query = query.Where(c => c.DigitLength > 2);
        }

        if (!string.IsNullOrWhiteSpace(codeType))
        {
            Entity.Enums.HsnSacCodeType type = codeType.Equals("SAC", StringComparison.OrdinalIgnoreCase)
                ? Entity.Enums.HsnSacCodeType.Sac
                : Entity.Enums.HsnSacCodeType.Hsn;
            query = query.Where(c => c.CodeType == type);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(c => c.Code.StartsWith(term) || c.Description.Contains(term));
        }

        var codes = await query
            .OrderBy(c => c.Code)
            .Take(Math.Clamp(take, 1, 200))
            .Select(c => new
            {
                c.HsnSacCodeId,
                c.Code,
                c.CodeType,
                c.Description,
                c.ChapterCode,
                c.DefaultGstRate,
                c.DigitLength,
            })
            .ToListAsync(ct);

        return Ok(codes);
    }

    /// <summary>The chapter headings, for grouping the picker.</summary>
    [HttpGet("hsn-sac/chapters")]
    public async Task<IActionResult> GetHsnChapters(CancellationToken ct)
    {
        var chapters = await _db.HsnSacCodes
            .Where(c => c.DigitLength == 2 && c.IsActive)
            .OrderBy(c => c.Code)
            .Select(c => new { c.Code, c.Description, c.CodeType })
            .ToListAsync(ct);
        return Ok(chapters);
    }

    /// <summary>The five account types — the only level above the chart of accounts.</summary>
    [HttpGet("account-types")]
    public async Task<IActionResult> GetAccountTypes(CancellationToken ct)
    {
        var types = await _db.AccountTypes
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .Select(t => new
            {
                t.AccountTypeId,
                t.DisplayName,
                t.NormalBalance,
                t.ReportSection,
                t.SortOrder,
            })
            .ToListAsync(ct);
        return Ok(types);
    }
}
