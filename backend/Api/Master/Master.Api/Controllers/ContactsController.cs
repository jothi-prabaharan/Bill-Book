using Master.Api.Services;
using Master.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;

namespace Master.Api.Controllers;

/// <summary>
/// Customers, vendors, job workers and prescribers — one master with role flags.
/// The contact saves as a whole, addresses and people included, because the
/// rules that matter are rules about the set.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("contacts")]
[Route("api/contacts")]
public sealed class ContactsController : ControllerBase
{
    private readonly ContactService _contacts;

    /// <summary>Read for <c>Portal:BaseUrl</c> only — see GeneratePortalLink.</summary>
    private readonly IConfiguration _configuration;

    public ContactsController(ContactService contacts, IConfiguration configuration)
    {
        _contacts = contacts;
        _configuration = configuration;
    }

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

    /// <summary>
    /// A contact from a name and one way to reach them.
    ///
    /// <b>The same authority as any other create</b> — this controller's
    /// <c>contacts</c> module permission, and POST maps to <c>contacts.edit</c>.
    /// A CRM user converting a lead into a brand-new contact is creating master
    /// data, so they need the permission that creating master data needs; without
    /// it this answers 403 and the conversion tells them why.
    ///
    /// Contacts composes the rest of the request — see
    /// <c>ContactService.CreateQuickAsync</c> — so a caller never has to know
    /// about person roles, categories or the branch's base currency.
    /// </summary>
    [HttpPost("quick")]
    public async Task<IActionResult> CreateQuick(
        [FromBody] QuickContactRequest request, CancellationToken ct)
    {
        SaveContactResult result = await _contacts.CreateQuickAsync(request, ct);

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

    /// <summary>
    /// Creates the sub-accounts for a contact whose call failed when it was
    /// saved — the contact exists, it simply cannot be posted against yet.
    /// </summary>
    [HttpPost("{contactId:long}/link-sub-ledger")]
    public async Task<IActionResult> LinkSubLedger(long contactId, CancellationToken ct)
    {
        SaveContactOutcome outcome = await _contacts.RetrySubLedgerAsync(contactId, ct);
        return Respond(outcome, NoContent);
    }

    [HttpPost("{contactId:long}/portal-link")]
    public async Task<IActionResult> GeneratePortalLink(long contactId, CancellationToken ct)
    {
        string? token = await _contacts.GeneratePortalLinkAsync(contactId, ct);
        if (token is null)
        {
            return NotFound();
        }

        // The token and, where the deployment says where the portal lives, the
        // whole link.
        //
        // <b>Both, rather than one or the other.</b> A screen that has just
        // generated the link knows its own origin and can compose it; an email
        // sent to a contact cannot ask a browser anything, so somewhere the
        // deployment has to say. `Portal:BaseUrl` is optional for exactly that
        // reason — a development box with no portal deployed still gets a
        // usable token, and does not get a link to a host that is not there.
        string? baseUrl = _configuration["Portal:BaseUrl"]?.TrimEnd('/');

        return Ok(new
        {
            token,
            url = string.IsNullOrWhiteSpace(baseUrl)
                ? null
                : $"{baseUrl}/portal?token={Uri.EscapeDataString(token)}",
        });
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
            SaveContactOutcome.SubLedgerUnavailable => StatusCode(
                StatusCodes.Status409Conflict,
                new MessageResponse
                {
                    Message = "The contact was saved but its receivable and payable sub-accounts "
                        + "could not be created, so it cannot be invoiced or billed yet. Use "
                        + "Link sub-ledger to try again.",
                }),
            SaveContactOutcome.SubLedgerAlreadyLinked => BadRequest(new MessageResponse
            {
                Message = "This contact already has its sub-accounts.",
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
