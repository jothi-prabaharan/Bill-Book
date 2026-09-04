using System.Net;
using Customer.Api.Services;
using Customer.Entity.TableEntities;
using Customer.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Customer;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;
using System.ComponentModel.DataAnnotations;

namespace Customer.Api.Controllers;

[ApiController]
[Authorize]
// "crm", not "customer". The permission catalogue is seeded with crm.* and
// support.* — the two halves this service was merged from — and never with
// customer.*, so every request here was refused for every role, whatever the
// role held. The schema merged; the permissions did not.
[RequireModulePermission("crm")]
[Route("api/leads")]
public sealed class LeadsController : ControllerBase
{
    private readonly CustomerDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IContactsClient _contacts;

    public LeadsController(
        CustomerDbContext db, ITenantContext tenant, IContactsClient contacts)
    {
        _db = db;
        _tenant = tenant;
        _contacts = contacts;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var leads = await _db.Leads
            .OrderByDescending(l => l.LeadId)
            .Select(l => new
            {
                l.LeadId,
                l.OrgId,
                l.Name,
                l.CompanyName,
                l.Phone,
                l.Email,
                l.Source,
                l.Status,
                l.ConvertedContactId,
                l.ConvertedAt
            })
            .ToListAsync(ct);

        return Ok(leads);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var lead = await _db.Leads.FindAsync(new object[] { id }, ct);
        if (lead == null) return NotFound();

        if (lead.OrgId != _tenant.OrgId) return Forbid();

        return Ok(lead);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveLeadRequest request, CancellationToken ct)
    {
        var lead = new Lead
        {
            Name = request.Name,
            CompanyName = request.CompanyName,
            Phone = request.Phone,
            Email = request.Email,
            Source = request.Source,
            Status = LeadStatus.New
        };

        _db.Leads.Add(lead);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = lead.LeadId }, lead);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveLeadRequest request, CancellationToken ct)
    {
        var lead = await _db.Leads.FindAsync(new object[] { id }, ct);
        if (lead == null) return NotFound();

        if (lead.OrgId != _tenant.OrgId) return Forbid();

        lead.Name = request.Name;
        lead.CompanyName = request.CompanyName;
        lead.Phone = request.Phone;
        lead.Email = request.Email;
        lead.Source = request.Source;

        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var lead = await _db.Leads.FindAsync(new object[] { id }, ct);
        if (lead == null) return NotFound();

        if (lead.OrgId != _tenant.OrgId) return Forbid();

        _db.Leads.Remove(lead);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Turns a lead into a contact, either an existing one or a new one.
    ///
    /// <b>Both paths are supported and exactly one is taken.</b> A request naming
    /// a <c>ContactId</c> links to that contact; a request without one has Contacts
    /// create it from the lead's own name, phone and email. Naming an id
    /// <i>and</i> asking for a new contact is a contradiction and is refused
    /// rather than resolved by precedence, because whichever way a precedence
    /// rule fell, half the callers would be surprised by it.
    ///
    /// <b>A supplied <c>ContactId</c> is checked against the caller's branch.</b>
    /// The column is a plain id — <c>con.Contacts</c> is another service's
    /// database — so nothing about the number itself says whose books it belongs
    /// to. Contacts is asked, with the caller's own token forwarded, so the answer
    /// comes back through that service's query filter and RLS policy: a contact in
    /// another branch is simply not there. That is a <c>Forbid()</c>, per
    /// CLAUDE.md, and not a <c>NotFound()</c>.
    ///
    /// <b>The lead is marked converted only after the contact exists.</b> The two
    /// live in different databases and cannot share a transaction, so the order is
    /// the guarantee: create or verify first, write the lead second. A failure
    /// leaves an unconverted lead and, at worst, a contact with no lead pointing
    /// at it — which is visible and fixable, where a lead marked converted against
    /// a contact that was never created is neither.
    /// </summary>
    [HttpPost("{id:long}/convert")]
    public async Task<IActionResult> Convert(long id, [FromBody] ConvertLeadRequest request, CancellationToken ct)
    {
        var lead = await _db.Leads.FindAsync(new object[] { id }, ct);
        if (lead == null) return NotFound();

        if (lead.OrgId != _tenant.OrgId) return Forbid();

        if (lead.Status == LeadStatus.Converted)
        {
            return BadRequest(new { message = "Lead is already converted." });
        }

        if (request.ContactId is not null && request.CreateContact)
        {
            return BadRequest(new
            {
                message = "Give a ContactId to link an existing contact, or ask for a new "
                    + "one — not both.",
            });
        }

        if (request.ContactId is null && !request.CreateContact)
        {
            // Defaulting to "create one" would turn a client that forgot the id
            // into a client that silently makes duplicate contacts.
            return BadRequest(new
            {
                message = "Give a ContactId to link an existing contact, or set CreateContact "
                    + "to make a new one from this lead.",
            });
        }

        long contactId;
        string? contactCode = null;

        if (request.ContactId is long existing)
        {
            if (!await _contacts.ExistsInCallerOrgAsync(existing, ct))
            {
                // No such contact, another branch's contact, and another
                // customer's contact are one answer on purpose: telling them
                // apart is the information an id-probing caller is after.
                return Forbid();
            }

            contactId = existing;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(lead.Email) && string.IsNullOrWhiteSpace(lead.Phone))
            {
                return BadRequest(new
                {
                    message = "This lead has neither an email nor a phone number, so a contact "
                        + "cannot be created from it. Add one, or convert to an existing contact.",
                });
            }

            CreatedContact created;

            try
            {
                created = await _contacts.CreateAsync(
                    new NewContactRequest
                    {
                        DisplayName = lead.CompanyName is { Length: > 0 } company
                            ? company
                            : lead.Name,
                        LegalName = lead.CompanyName,
                        Phone = lead.Phone,
                        Email = lead.Email,
                    },
                    ct)
                    ?? throw new ContactCreationFailedException(HttpStatusCode.BadGateway);
            }
            catch (ContactCreationFailedException ex)
            {
                // The lead is untouched. Nothing above this line has written.
                return ex.Status == HttpStatusCode.Forbidden
                    ? StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        message = "Converting a lead into a new contact needs permission to "
                            + "create contacts. Convert to an existing contact instead, or ask "
                            + "for the Contacts permission.",
                    })
                    : StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }

            contactId = created.ContactId;
            contactCode = created.ContactCode;
        }

        lead.Status = LeadStatus.Converted;
        lead.ConvertedContactId = contactId;
        lead.ConvertedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            leadId = lead.LeadId,
            contactId,
            contactCode,
            convertedAt = lead.ConvertedAt,
        });
    }
}

public class SaveLeadRequest
{
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public string Name { get; set; } = null!;

    [MaxLength(200, ErrorMessage = "Company name cannot exceed 200 characters.")]
    public string? CompanyName { get; set; }

    [MaxLength(50, ErrorMessage = "Phone cannot exceed 50 characters.")]
    public string? Phone { get; set; }

    [MaxLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
    public string? Email { get; set; }

    public LeadSource Source { get; set; } = LeadSource.Other;
}

/// <summary>
/// How a lead is converted.
///
/// <b>Exactly one of the two paths.</b> <see cref="ContactId"/> links an existing
/// contact; <see cref="CreateContact"/> asks Contacts to make one from the lead.
/// Neither, or both, is refused — see <c>LeadsController.Convert</c>.
/// </summary>
public class ConvertLeadRequest
{
    /// <summary>
    /// The existing contact to link. Validated against the caller's branch before
    /// anything is written — the foreign key proves the row exists, not whose it is.
    /// </summary>
    public long? ContactId { get; set; }

    /// <summary>Create a new contact from the lead's own name, phone and email.</summary>
    public bool CreateContact { get; set; }
}
