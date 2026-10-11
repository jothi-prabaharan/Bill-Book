using System.ComponentModel.DataAnnotations;
using Customer.Api.Services;
using Customer.Entity.TableEntities;
using Customer.Repository;
using Customer.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Customer;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;
using Shared.Kernel.Validation;

namespace Customer.Api.Controllers;

/// <summary>
/// Public enquiry endpoint for the marketing/corporate website.
/// Receives demo requests, sales inquiries, and contact form submissions,
/// and saves them directly into the CRM Lead table (cus.Leads).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/enquiry")]
public sealed class PublicEnquiryController : ControllerBase
{
    private readonly CustomerDbContext _db;
    private readonly TenantContext _tenant;
    private readonly ITenantEnumerator _tenantEnumerator;
    private readonly AutomationRepository? _automation;

    public PublicEnquiryController(
        CustomerDbContext db,
        TenantContext tenant,
        ITenantEnumerator tenantEnumerator,
        AutomationRepository? automation = null)
    {
        _db = db;
        _tenant = tenant;
        _tenantEnumerator = tenantEnumerator;
        _automation = automation;
    }

    [HttpPost]
    public async Task<IActionResult> SubmitEnquiry([FromBody] PublicEnquiryRequest request, CancellationToken ct)
    {
        // Resolve tenant context: use explicitly requested IDs if valid,
        // or resolve the default active organization via ITenantEnumerator.
        if (_tenant.CustomerId is null || _tenant.OrgId is null)
        {
            if (request.CustomerId.HasValue && request.OrgId.HasValue)
            {
                _tenant.CustomerId = request.CustomerId.Value;
                _tenant.OrgId = request.OrgId.Value;
            }
            else
            {
                var orgs = await _tenantEnumerator.ListAsync(ct);
                var target = orgs.FirstOrDefault();
                if (target is not null)
                {
                    _tenant.CustomerId = target.CustomerId;
                    _tenant.OrgId = target.OrgId;
                }
            }
        }

        var companyWithVertical = string.IsNullOrWhiteSpace(request.Vertical)
            ? request.CompanyName
            : string.IsNullOrWhiteSpace(request.CompanyName)
                ? $"[{request.Vertical}]"
                : $"{request.CompanyName} ({request.Vertical})";

        var lead = new Lead
        {
            Name = request.Name,
            CompanyName = companyWithVertical,
            Phone = PhoneNumbers.NormalizeOptional(request.Phone),
            Email = request.Email,
            Source = LeadSource.Website,
            Status = LeadStatus.New
        };

        _db.Leads.Add(lead);
        await _db.SaveChangesAsync(ct);

        if (_automation is not null)
        {
            await _automation.TriggerLeadCreatedAsync(lead.LeadId, lead.Email, lead.Name, lead.Source, ct);
        }

        return Ok(new
        {
            success = true,
            leadId = lead.LeadId,
            message = "Thank you! Your enquiry has been received and our team will get in touch shortly."
        });
    }
}

public class PublicEnquiryRequest
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

    [MaxLength(100, ErrorMessage = "Vertical cannot exceed 100 characters.")]
    public string? Vertical { get; set; }

    [MaxLength(500, ErrorMessage = "Message cannot exceed 500 characters.")]
    public string? Message { get; set; }

    public Guid? CustomerId { get; set; }

    public Guid? OrgId { get; set; }
}
