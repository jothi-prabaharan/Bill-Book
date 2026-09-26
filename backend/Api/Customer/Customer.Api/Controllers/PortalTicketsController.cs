using System.ComponentModel.DataAnnotations;
using Customer.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Customer.Api.Controllers;

/// <summary>
/// A contact's support tickets in the client portal (TK-97): list, read,
/// raise and reply. The contact is the portal token's; another contact's
/// ticket is 404, and internal notes never leave this service through here.
/// </summary>
[ApiController]
[Authorize]
[RequirePortalAccess]
[Route("api/portal/tickets")]
[RequireApp(App.RetailErp)]
public sealed class PortalTicketsController : ControllerBase
{
    private readonly PortalTicketService _tickets;

    public PortalTicketsController(PortalTicketService tickets) => _tickets = tickets;

    private long ContactId => long.Parse(User.FindFirst(RequirePortalAccessAttribute.ContactClaim)!.Value);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _tickets.ListAsync(ContactId, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) =>
        await _tickets.GetAsync(ContactId, id, ct) is { } detail ? Ok(detail) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Raise([FromBody] PortalRaiseTicketRequest request, CancellationToken ct)
    {
        long id = await _tickets.RaiseAsync(ContactId, request.Subject!, request.Description, ct);
        return CreatedAtAction(nameof(Get), new { id }, new { ticketId = id });
    }

    [HttpPost("{id:long}/messages")]
    public async Task<IActionResult> Reply(long id, [FromBody] PortalTicketReplyRequest request, CancellationToken ct) =>
        await _tickets.ReplyAsync(ContactId, id, request.Body!, ct) switch
        {
            PortalTicketOutcome.Ok => NoContent(),
            PortalTicketOutcome.NotFound => NotFound(),
            _ => Conflict(new { message = "This ticket is closed. Raise a new one if you still need help." }),
        };
}

public sealed class PortalRaiseTicketRequest
{
    [Required(ErrorMessage = "A subject is required.")]
    [MaxLength(200, ErrorMessage = "The subject cannot exceed 200 characters.")]
    public string? Subject { get; set; }

    [MaxLength(4000, ErrorMessage = "The description cannot exceed 4000 characters.")]
    public string? Description { get; set; }
}

public sealed class PortalTicketReplyRequest
{
    [Required(ErrorMessage = "A message is required.")]
    [MaxLength(4000, ErrorMessage = "A message cannot exceed 4000 characters.")]
    public string? Body { get; set; }
}
