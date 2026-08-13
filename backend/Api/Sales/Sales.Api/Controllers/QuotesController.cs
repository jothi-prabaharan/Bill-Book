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
    private readonly QuoteService _quotes;

    public QuotesController(QuoteService quotes) => _quotes = quotes;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        List<QuoteListItem> quotes = await _quotes.ListAsync(ct);
        return Ok(quotes);
    }

    [HttpGet("{quoteId:long}")]
    public async Task<IActionResult> Get(long quoteId, CancellationToken ct)
    {
        QuoteView? quote = await _quotes.GetAsync(quoteId, ct);
        return quote is null ? NotFound() : Ok(quote);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveQuoteRequest request, CancellationToken ct)
    {
        QuoteResult result = await _quotes.CreateAsync(request, ct);

        return result.Outcome == QuoteOutcome.Ok
            ? CreatedAtAction(nameof(Get), new { quoteId = result.QuoteId }, result)
            : Respond(result.Outcome, result.Detail);
    }

    [HttpPut("{quoteId:long}")]
    public async Task<IActionResult> Update(long quoteId, [FromBody] SaveQuoteRequest request, CancellationToken ct)
    {
        QuoteResult result = await _quotes.UpdateAsync(quoteId, request, ct);

        return result.Outcome == QuoteOutcome.Ok
            ? NoContent()
            : Respond(result.Outcome, result.Detail);
    }

    [HttpPost("{quoteId:long}/void")]
    [PermissionAction("void")]
    public async Task<IActionResult> Void(long quoteId, [FromBody] VoidQuoteRequest request, CancellationToken ct)
    {
        QuoteResult result = await _quotes.VoidAsync(quoteId, request, ct);

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
            QuoteOutcome.AlreadyConverted => Conflict(new MessageResponse { Message = "This quote has already been converted to an order." }),
            QuoteOutcome.Lapsed => BadRequest(new MessageResponse { Message = "A lapsed quote cannot be converted." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
}

public sealed class MessageResponse
{
    public required string Message { get; init; }
}
