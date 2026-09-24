using Master.Api.Services;
using Master.Entity.Enums;
using Master.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Apps;

namespace Master.Api.Controllers;

/// <summary>
/// Exchange and metal rate history (TK-24).
///
/// <b>Reading is open to any signed-in user</b>, like the rest of the global
/// reference data in <see cref="MasterController"/>: every role that raises a
/// foreign-currency or jewellery document needs the day's rate, and no module
/// permission is held by all of them.
///
/// <b>Writing needs <c>platform.edit</c></b>, which only a platform operator's
/// token carries. A rate is one global row read by every customer, so letting a
/// customer's own settings user write it would let one business change the
/// rate another business's invoices snapshot.
/// </summary>
[ApiController]
[Authorize]
[Route("api/rates")]
[RequireApp(App.All)]
public sealed class RatesController : ControllerBase
{
    private readonly RateService _rates;

    public RatesController(RateService rates) => _rates = rates;

    /// <summary>The latest rate on or before <c>on</c> (today when omitted), or 404 when none is on file.</summary>
    [HttpGet("exchange")]
    public async Task<IActionResult> Exchange(
        [FromQuery] string from, [FromQuery] string to, [FromQuery] DateOnly? on, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return BadRequest(new MessageResponse { Message = "Name both currencies." });
        }

        ExchangeRateDto? rate = await _rates.LatestExchangeAsync(from, to, on ?? Today(), ct);
        return rate is null ? NotFound() : Ok(rate);
    }

    /// <summary>The latest price per gram on or before <c>on</c>, or 404 when none is on file.</summary>
    [HttpGet("metal")]
    public async Task<IActionResult> Metal(
        [FromQuery] string metal, [FromQuery] string purity, [FromQuery] DateOnly? on, CancellationToken ct)
    {
        if (!Enum.TryParse(metal, ignoreCase: true, out Metal parsed) || !Enum.IsDefined(parsed)
            || string.IsNullOrWhiteSpace(purity))
        {
            return BadRequest(new MessageResponse { Message = "Name a metal (Gold, Silver or Platinum) and a purity." });
        }

        MetalRateDto? rate = await _rates.LatestMetalAsync(parsed, purity, on ?? Today(), ct);
        return rate is null ? NotFound() : Ok(rate);
    }

    [RequirePermission("platform.view")]
    [HttpGet("exchange/history")]
    public async Task<IActionResult> ExchangeHistory(
        [FromQuery] string? from, [FromQuery] string? to, [FromQuery] int take = 100, CancellationToken ct = default) =>
        Ok(await _rates.ExchangeHistoryAsync(from, to, take, ct));

    [RequirePermission("platform.view")]
    [HttpGet("metal/history")]
    public async Task<IActionResult> MetalHistory(
        [FromQuery] string? metal, [FromQuery] string? purity, [FromQuery] int take = 100, CancellationToken ct = default)
    {
        Metal? parsed = Enum.TryParse(metal, ignoreCase: true, out Metal m) && Enum.IsDefined(m) ? m : null;
        return Ok(await _rates.MetalHistoryAsync(parsed, purity, take, ct));
    }

    /// <summary>Enters or corrects the manual rate for a pair and date.</summary>
    [RequirePermission("platform.edit")]
    [HttpPut("exchange")]
    public async Task<IActionResult> SetExchange([FromBody] SaveExchangeRateRequest request, CancellationToken ct) =>
        Map(await _rates.SetExchangeAsync(request, ct));

    /// <summary>Enters or corrects the manual price per gram for a metal, purity and date.</summary>
    [RequirePermission("platform.edit")]
    [HttpPut("metal")]
    public async Task<IActionResult> SetMetal([FromBody] SaveMetalRateRequest request, CancellationToken ct) =>
        Map(await _rates.SetMetalAsync(request, ct));

    [RequirePermission("platform.edit")]
    [HttpDelete("exchange/{id:long}")]
    public async Task<IActionResult> DeleteExchange(long id, CancellationToken ct) =>
        Map(await _rates.DeleteExchangeAsync(id, ct));

    [RequirePermission("platform.edit")]
    [HttpDelete("metal/{id:long}")]
    public async Task<IActionResult> DeleteMetal(long id, CancellationToken ct) =>
        Map(await _rates.DeleteMetalAsync(id, ct));

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    private IActionResult Map(RateSaveOutcome outcome) => outcome switch
    {
        RateSaveOutcome.Ok => NoContent(),
        RateSaveOutcome.NotFound => NotFound(),
        RateSaveOutcome.UnknownCurrency => UnprocessableEntity(new MessageResponse { Message = "Both currencies must be ones the system knows." }),
        RateSaveOutcome.SameCurrency => UnprocessableEntity(new MessageResponse { Message = "A rate needs two different currencies." }),
        RateSaveOutcome.UnknownMetal => UnprocessableEntity(new MessageResponse { Message = "The metal must be Gold, Silver or Platinum." }),
        RateSaveOutcome.NotManual => Conflict(new MessageResponse { Message = "Only a rate entered by hand can be removed. Enter a manual rate for the same date to override a fetched one." }),
        _ => StatusCode(StatusCodes.Status500InternalServerError),
    };
}
