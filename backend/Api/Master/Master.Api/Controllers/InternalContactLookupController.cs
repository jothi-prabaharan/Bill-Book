using Master.Api.Services;
using Master.Entity.Models;
using Master.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Contacts;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;
using Shared.Kernel.Validation;

namespace Master.Api.Controllers;

/// <summary>
/// Which contacts exist in a branch and what each is for (School, TK-61): Sis
/// checks a student's guardians, Fee the guardian it invoices, Amc a contract's
/// vendor. The branch comes from the body, as on every internal route, and the
/// query filter keeps the answer inside it: another branch's contact comes back
/// missing, never named.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/contacts")]
public sealed class InternalContactLookupController : ControllerBase
{
    private const int MaxIds = 500;

    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalContactLookupController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost("lookup")]
    public async Task<IActionResult> Lookup([FromBody] ContactLookupRequest request, CancellationToken ct)
    {
        switch (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId))
        {
            case InternalTenantOutcome.Missing:
                return BadRequest(new MessageResponse { Message = "A customer and an organization are required to look up contacts." });
            case InternalTenantOutcome.Mismatch:
                return Forbid();
        }

        List<long> ids = [.. request.Ids.Distinct().Take(MaxIds)];
        if (ids.Count == 0)
        {
            return Ok(Array.Empty<ContactSummary>());
        }

        var db = _services.GetRequiredService<ContactsDbContext>();
        List<ContactSummary> found = await db.Contacts
            .AsNoTracking()
            .Where(c => ids.Contains(c.ContactId))
            .Select(c => new ContactSummary
            {
                ContactId = c.ContactId,
                ContactCode = c.ContactCode,
                DisplayName = c.DisplayName,
                IsCustomer = c.IsCustomer,
                IsVendor = c.IsVendor,
                IsGuardian = c.IsGuardian,
                IsActive = c.IsActive,
            })
            .ToListAsync(ct);

        return Ok(found);
    }

    /// <summary>
    /// The guardian with this mobile number, or a new one (School admissions,
    /// TK-62). Matching on the mobile number is what makes an admit retried
    /// after a lost reply, or a second child of the same parent, reuse the
    /// contact rather than make another. A new guardian is not a customer; it is
    /// numbered like one and given its sub-ledger like any other contact.
    /// </summary>
    [HttpPost("guardians/ensure")]
    public async Task<IActionResult> EnsureGuardian([FromBody] EnsureGuardianRequest request, CancellationToken ct)
    {
        switch (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId))
        {
            case InternalTenantOutcome.Missing:
                return BadRequest(new MessageResponse { Message = "A customer and an organization are required to find a guardian." });
            case InternalTenantOutcome.Mismatch:
                return Forbid();
        }

        string? mobile = PhoneNumbers.NormalizeOptional(request.MobileNumber);
        if (mobile is null || string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return UnprocessableEntity(new MessageResponse { Message = "A guardian needs a name and a mobile number." });
        }

        var db = _services.GetRequiredService<ContactsDbContext>();
        long? existing = await db.Contacts
            .Where(c => c.IsGuardian && c.IsActive
                && db.ContactPersons.Any(p => p.ContactId == c.ContactId && p.IsActive && p.MobileNumber == mobile))
            .OrderBy(c => c.ContactId)
            .Select(c => (long?)c.ContactId)
            .FirstOrDefaultAsync(ct);
        if (existing is long found)
        {
            return Ok(new EnsureGuardianResponse { ContactId = found, Created = false });
        }

        SaveContactResult created = await _services.GetRequiredService<ContactService>().CreateQuickAsync(new QuickContactRequest
        {
            DisplayName = request.DisplayName.Trim(),
            MobileNumber = mobile,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            IsCustomer = false,
            IsGuardian = true,
        }, ct);

        return created.Outcome == SaveContactOutcome.Ok && created.ContactId is long contactId
            ? Ok(new EnsureGuardianResponse { ContactId = contactId, Created = true })
            : UnprocessableEntity(new MessageResponse { Message = "The guardian could not be added as a contact. Check the name, mobile number and email." });
    }
}
