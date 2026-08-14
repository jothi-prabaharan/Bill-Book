using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Api.Services;
using Sales.Entity.Models;
using Shared.Kernel.Internal;

namespace Sales.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("sales")]
[Route("api/sales/quotes")]
public sealed class QuotesController : ControllerBase
{
    private readonly QuoteService _Quotes;

    public QuotesController(QuoteService Quotes) => _Quotes = Quotes;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        List<QuoteListItem> Quotes = await _Quotes.ListAsync(ct);
        return Ok(Quotes);
    }

    [HttpGet("{QuoteId:long}")]
    public async Task<IActionResult> Get(long QuoteId, CancellationToken ct)
    {
        QuoteView? Quote = await _Quotes.GetAsync(QuoteId, ct);
        return Quote is null ? NotFound() : Ok(Quote);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveQuoteRequest request, CancellationToken ct)
    {
        QuoteResult result = await _Quotes.CreateAsync(request, ct);

        return result.Outcome == QuoteOutcome.Ok
            ? CreatedAtAction(nameof(Get), new { QuoteId = result.QuoteId }, result)
            : Respond(result.Outcome, result.Detail);
    }

    [HttpPut("{QuoteId:long}")]
    public async Task<IActionResult> Update(long QuoteId, [FromBody] SaveQuoteRequest request, CancellationToken ct)
    {
        QuoteResult result = await _Quotes.UpdateAsync(QuoteId, request, ct);

        return result.Outcome == QuoteOutcome.Ok
            ? NoContent()
            : Respond(result.Outcome, result.Detail);
    }

    [HttpPost("{QuoteId:long}/approve")]
    [PermissionAction("sales.approve")]
    public async Task<IActionResult> Approve(long QuoteId, CancellationToken ct)
    {
        QuoteResult result = await _Quotes.PostAsync(QuoteId, ct);

        return result.Outcome == QuoteOutcome.Ok
            ? NoContent()
            : Respond(result.Outcome, result.Detail);
    }

    [HttpPost("{QuoteId:long}/void")]
    [PermissionAction("sales.void")]
    public async Task<IActionResult> Void(long QuoteId, [FromBody] VoidQuoteRequest request, CancellationToken ct)
    {
        QuoteResult result = await _Quotes.VoidAsync(QuoteId, request, ct);

        return result.Outcome == QuoteOutcome.Ok
            ? NoContent()
            : Respond(result.Outcome, result.Detail);
    }

    private IActionResult Respond(QuoteOutcome outcome, string? detail) =>
        outcome switch
        {
            QuoteOutcome.NotFound => NotFound(),
            QuoteOutcome.LifecycleRefused => BadRequest(new MessageResponse { Message = detail ?? "Action refused by document lifecycle." }),
            QuoteOutcome.LineInvalid => BadRequest(new MessageResponse { Message = detail ?? "One or more lines are invalid." }),
            QuoteOutcome.ValidityInvalid => BadRequest(new MessageResponse { Message = detail ?? "Validity date is invalid." }),
            QuoteOutcome.PlaceOfSupplyRefused => BadRequest(new MessageResponse { Message = detail ?? "Place of supply could not be determined." }),
            QuoteOutcome.RatesUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new MessageResponse { Message = detail ?? "Tax rates or base currency are temporarily unavailable." }),
            QuoteOutcome.AlreadyConverted => Conflict(new MessageResponse { Message = "This Quote has already been converted." }),
            QuoteOutcome.Lapsed => Conflict(new MessageResponse { Message = detail ?? "This Quote has lapsed." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
}
