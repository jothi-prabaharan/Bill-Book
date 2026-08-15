using Master.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Master.Api.Controllers;

/// <summary>
/// Reference data for the signup and settings screens.
///
/// Authenticated by default, with two exceptions marked below. Countries and
/// states are needed by the signup form, which by definition runs before any
/// token exists. Everything else here — currencies, HSN/SAC, account and ledger
/// types — is only ever read from inside the app, so there is no reason to serve
/// it to anyone who finds the port.
/// </summary>
[ApiController]
[Authorize]
[Route("api/master")]
public sealed class MasterController : ControllerBase
{
    private readonly AdminDbContext _db;

    public MasterController(AdminDbContext db) => _db = db;

    /// <summary>Anonymous: the signup form needs this before there is an account.</summary>
    [AllowAnonymous]
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

    /// <summary>Anonymous for the same reason as countries — signup asks for a state.</summary>
    [AllowAnonymous]
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
        [FromQuery] bool includeInactive,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        IQueryable<Entity.TableEntities.HsnSacCode> query = _db.HsnSacCodes;

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

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

        // Counted before paging: the master page has to say how many matched,
        // not how many fitted on the page.
        int total = await query.CountAsync(ct);

        var codes = await query
            .OrderBy(c => c.Code)
            .Skip(Math.Max(skip, 0))
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
                c.IsActive,
            })
            .ToListAsync(ct);

        return Ok(new { total, skip = Math.Max(skip, 0), rows = codes });
    }

    /// <summary>
    /// How much of the nomenclature is actually loaded, split by what it is.
    ///
    /// The page needs this to tell an empty search from an empty database. Only
    /// the 97 HS chapters and 32 SAC groups ship as seed data; the ~13,000
    /// detailed codes are imported from the CBIC file, and until that runs a
    /// search for a real 8-digit code finds nothing — which reads as a broken
    /// screen rather than as data nobody has loaded yet.
    /// </summary>
    [HttpGet("hsn-sac/summary")]
    public async Task<IActionResult> GetHsnSacSummary(CancellationToken ct)
    {
        var counts = await _db.HsnSacCodes
            .GroupBy(c => new { c.CodeType, IsChapter = c.DigitLength <= 2 })
            .Select(g => new
            {
                g.Key.CodeType,
                g.Key.IsChapter,
                Total = g.Count(),
                Active = g.Count(c => c.IsActive),
            })
            .ToListAsync(ct);

        return Ok(new
        {
            HsnChapters = counts.Where(c => c.CodeType == Entity.Enums.HsnSacCodeType.Hsn && c.IsChapter)
                .Sum(c => c.Total),
            HsnCodes = counts.Where(c => c.CodeType == Entity.Enums.HsnSacCodeType.Hsn && !c.IsChapter)
                .Sum(c => c.Total),
            SacGroups = counts.Where(c => c.CodeType == Entity.Enums.HsnSacCodeType.Sac && c.IsChapter)
                .Sum(c => c.Total),
            SacCodes = counts.Where(c => c.CodeType == Entity.Enums.HsnSacCodeType.Sac && !c.IsChapter)
                .Sum(c => c.Total),
            Inactive = counts.Sum(c => c.Total - c.Active),
        });
    }

    /// <summary>
    /// One code by id. The item master stores only <c>HsnSacCodeId</c>, so a
    /// saved item has nothing to display without this — search is by code
    /// prefix and cannot resolve an id it already holds.
    /// </summary>
    [HttpGet("hsn-sac/{hsnSacCodeId:int}")]
    public async Task<IActionResult> GetHsnSacCode(int hsnSacCodeId, CancellationToken ct)
    {
        var code = await _db.HsnSacCodes
            .Where(c => c.HsnSacCodeId == hsnSacCodeId)
            .Select(c => new
            {
                c.HsnSacCodeId,
                c.Code,
                c.CodeType,
                c.Description,
                c.DefaultGstRate,
                c.DigitLength,
                c.IsActive,
            })
            .FirstOrDefaultAsync(ct);

        return code is null ? NotFound() : Ok(code);
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
