using Contacts.Api.Services;
using Contacts.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Contacts.Api.Controllers;

/// <summary>
/// Customers, vendors, job workers and prescribers — one master with role flags.
/// The contact saves as a whole, addresses and people included, because the
/// rules that matter are rules about the set.
/// </summary>
[ApiController]
[Authorize]
[Route("api/contacts")]
public sealed class ContactsController : ControllerBase
{
    private readonly ContactService _contacts;

    public ContactsController(ContactService contacts) => _contacts = contacts;

    /// <summary><paramref name="role"/> is customer, vendor, jobworker or prescriber.</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] bool includeInactive,
        CancellationToken ct) =>
        Ok(await _contacts.ListAsync(search, role, includeInactive, ct));

    [HttpGet("{contactId:long}")]
    public async Task<IActionResult> Get(long contactId, CancellationToken ct)
    {
        ContactDetail? detail = await _contacts.GetAsync(contactId, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveContactRequest request, CancellationToken ct)
    {
        SaveContactResult result = await _contacts.CreateAsync(request, ct);
        return Respond(result.Outcome, () => CreatedAtAction(
            nameof(Get),
            new { contactId = result.ContactId },
            new { contactId = result.ContactId, contactCode = result.ContactCode }));
    }

    [HttpPut("{contactId:long}")]
    public async Task<IActionResult> Update(
        long contactId, [FromBody] SaveContactRequest request, CancellationToken ct)
    {
        SaveContactResult result = await _contacts.UpdateAsync(contactId, request, ct);
        return Respond(result.Outcome, NoContent);
    }

    [HttpDelete("{contactId:long}")]
    public async Task<IActionResult> Deactivate(long contactId, CancellationToken ct) =>
        Respond(await _contacts.DeactivateAsync(contactId, ct), NoContent);

    private IActionResult Respond(SaveContactOutcome outcome, Func<IActionResult> onOk) =>
        outcome switch
        {
            SaveContactOutcome.Ok => onOk(),
            SaveContactOutcome.NotFound => NotFound(),
            SaveContactOutcome.NoRole => BadRequest(new MessageResponse
            {
                Message = "Choose at least one of customer, vendor, job worker or prescriber — "
                    + "a contact with no role cannot be found on any screen.",
            }),
            SaveContactOutcome.DuplicateCode => BadRequest(new MessageResponse
            {
                Message = "Another contact already uses that code.",
            }),
            SaveContactOutcome.DuplicateGstin => BadRequest(new MessageResponse
            {
                Message = "Another contact already uses that GSTIN. If they are both a customer "
                    + "and a vendor, tick both roles on the one contact instead.",
            }),
            SaveContactOutcome.GstinRequired => BadRequest(new MessageResponse
            {
                Message = "A GSTIN is required for regular, composition and SEZ registrations.",
            }),
            SaveContactOutcome.GstinNotAllowed => BadRequest(new MessageResponse
            {
                Message = "Unregistered, overseas and consumer contacts cannot carry a GSTIN.",
            }),
            SaveContactOutcome.GstinStateMismatch => BadRequest(new MessageResponse
            {
                Message = "The GSTIN's first two digits do not match the place of supply. Left as "
                    + "is, every document for this contact would split its tax the wrong way.",
            }),
            SaveContactOutcome.UnknownState => BadRequest(new MessageResponse
            {
                Message = "That place of supply could not be verified against the state master.",
            }),
            SaveContactOutcome.NoContactPerson => BadRequest(new MessageResponse
            {
                Message = "Add at least one contact person.",
            }),
            SaveContactOutcome.NoDefaultPerson => BadRequest(new MessageResponse
            {
                Message = "Mark exactly one contact person as the default.",
            }),
            SaveContactOutcome.DefaultPersonNeedsContactDetail => BadRequest(new MessageResponse
            {
                Message = "The default contact person needs an email address or a mobile number — "
                    + "invoices and reminders are sent to them.",
            }),
            SaveContactOutcome.MultipleDefaultAddresses => BadRequest(new MessageResponse
            {
                Message = "Only one billing address and one shipping address can be the default.",
            }),
            SaveContactOutcome.MultipleDefaultBankDetails => BadRequest(new MessageResponse
            {
                Message = "Only one payout account can be the default.",
            }),
            SaveContactOutcome.TdsSectionRequired => BadRequest(new MessageResponse
            {
                Message = "Enter the TDS section, or turn TDS off.",
            }),
            SaveContactOutcome.UdyamNumberRequired => BadRequest(new MessageResponse
            {
                Message = "Enter the Udyam registration number, or turn MSME off.",
            }),
            SaveContactOutcome.UnknownRole => BadRequest(new MessageResponse
            {
                Message = "One of the contact people has a role that no longer exists.",
            }),
            SaveContactOutcome.InvalidValue => BadRequest(new MessageResponse
            {
                Message = "One of the selected options is not a recognised value.",
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
}

public class MessageResponse
{
    public string Message { get; set; } = null!;
}
