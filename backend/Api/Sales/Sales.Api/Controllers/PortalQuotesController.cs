using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Api.Services;
using Sales.Entity.Enums;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Sales.Api.Controllers;

/// <summary>
/// A contact's quotes in the client portal, with accept and reject (TK-96).
/// The contact is the portal token's; a quote that is not theirs is 404.
/// </summary>
[ApiController]
[Authorize]
[RequirePortalAccess]
[Route("api/portal/quotes")]
[RequireApp(App.RetailErp)]
public sealed class PortalQuotesController : ControllerBase
{
    private readonly PortalQuoteService _quotes;

    public PortalQuotesController(PortalQuoteService quotes) => _quotes = quotes;

    private long ContactId => long.Parse(User.FindFirst(RequirePortalAccessAttribute.ContactClaim)!.Value);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _quotes.ListAsync(ContactId, ct));

    [HttpPost("{id:long}/accept")]
    public async Task<IActionResult> Accept(long id, [FromBody] QuoteAnswerRequest request, CancellationToken ct) =>
        Respond(await _quotes.RespondAsync(ContactId, id, QuoteResponse.Accepted, request.Name!, request.Note, ct));

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, [FromBody] QuoteAnswerRequest request, CancellationToken ct) =>
        Respond(await _quotes.RespondAsync(ContactId, id, QuoteResponse.Rejected, request.Name!, request.Note, ct));

    private IActionResult Respond(PortalQuoteOutcome outcome) => outcome switch
    {
        PortalQuoteOutcome.Ok => NoContent(),
        PortalQuoteOutcome.NotFound => NotFound(),
        PortalQuoteOutcome.Lapsed => Conflict(new { message = "This quote is no longer valid. Ask for a new one." }),
        PortalQuoteOutcome.AlreadyAnswered => Conflict(new { message = "This quote has already been answered." }),
        _ => Conflict(new { message = "This quote has already been turned into an order." }),
    };
}

public sealed class QuoteAnswerRequest
{
    [Required(ErrorMessage = "Your name is required.")]
    [MaxLength(100, ErrorMessage = "Your name cannot exceed 100 characters.")]
    public string? Name { get; set; }

    [MaxLength(500, ErrorMessage = "The note cannot exceed 500 characters.")]
    public string? Note { get; set; }
}
