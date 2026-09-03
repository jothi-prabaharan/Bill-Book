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

    public LeadsController(CustomerDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
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

    [HttpPost("{id:long}/convert")]
    public async Task<IActionResult> Convert(long id, [FromBody] ConvertLeadRequest request, CancellationToken ct)
    {
        var lead = await _db.Leads.FindAsync(new object[] { id }, ct);
        if (lead == null) return NotFound();

        if (lead.OrgId != _tenant.OrgId) return Forbid();

        if (lead.Status == LeadStatus.Converted)
        {
            return BadRequest("Lead is already converted.");
        }

        lead.Status = LeadStatus.Converted;
        lead.ConvertedContactId = request.ContactId;
        lead.ConvertedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok();
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

public class ConvertLeadRequest
{
    [Required(ErrorMessage = "ContactId is required for conversion.")]
    public long ContactId { get; set; }
}
