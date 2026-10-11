using Shared.Kernel.Apps;
using Master.Api.Services;
using Master.Repository.Repositories;
using Master.Repository.Clients;
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
/// Which contacts exist in a branch and what each is for (School, TK-61): Student
/// checks a student's guardians, Fee the guardian it invoices, MaintenanceContract a contract's
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

    /// <summary>
    /// The branch's walk-in customer (TK-148): who a restaurant bill is made
    /// out to when the guests gave no name. 404 when the branch has none.
    /// </summary>
    [HttpPost("walk-in")]
    public async Task<IActionResult> WalkIn([FromBody] ContactLookupRequest request, CancellationToken ct)
    {
        switch (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId))
        {
            case InternalTenantOutcome.Missing:
                return BadRequest(new MessageResponse { Message = "A customer and an organization are required to find the walk-in customer." });
            case InternalTenantOutcome.Mismatch:
                return Forbid();
        }

        var db = _services.GetRequiredService<ContactsDbContext>();
        long? contactId = await db.Contacts
            .AsNoTracking()
            .Where(c => c.ContactCode == ContactRepository.WalkInCode && c.IsActive)
            .Select(c => (long?)c.ContactId)
            .FirstOrDefaultAsync(ct);

        return contactId is long id ? Ok(new WalkInContactResponse { ContactId = id }) : NotFound();
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
                // A draft (TK-188) is not yet a contact a document may name,
                // so callers that refuse an inactive contact refuse it too.
                IsActive = c.IsActive && !c.IsDraft,
                IsMsme = c.IsMsme,
                UdyamNumber = c.UdyamNumber,
            })
            .ToListAsync(ct);

        return Ok(found);
    }

    /// <summary>
    /// The most a sales line may be discounted for a contact (D-29, TK-102): the
    /// contact's <c>MaxDiscountPercent</c> when set, else the branch's
    /// <c>sales.maxLineDiscountPercent</c>, else no limit. A contact another
    /// branch holds is not found rather than named.
    /// </summary>
    [HttpPost("discount-limit")]
    public async Task<IActionResult> DiscountLimit([FromBody] DiscountLimitRequest request, CancellationToken ct)
    {
        switch (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId))
        {
            case InternalTenantOutcome.Missing:
                return BadRequest(new MessageResponse { Message = "A customer and an organization are required to read a discount limit." });
            case InternalTenantOutcome.Mismatch:
                return Forbid();
        }

        var db = _services.GetRequiredService<ContactsDbContext>();
        var contact = await db.Contacts.AsNoTracking()
            .Where(c => c.ContactId == request.ContactId)
            .Select(c => new { c.MaxDiscountPercent })
            .FirstOrDefaultAsync(ct);
        if (contact is null)
        {
            return NotFound();
        }

        if (contact.MaxDiscountPercent is decimal own)
        {
            return Ok(new DiscountLimitResponse { LimitPercent = own, Source = "Contact" });
        }

        decimal branch = await _services.GetRequiredService<IDiscountLimitSetting>()
            .PercentAsync(request.OrgId, ct);
        return Ok(new DiscountLimitResponse { LimitPercent = branch, Source = "Branch" });
    }

    /// <summary>
    /// What TDS needs from a contact (TK-117): whether it applies, the section,
    /// PAN and TAN, and a lower-deduction certificate. A contact the branch
    /// does not hold is not found.
    /// </summary>
    [HttpPost("tds-profile")]
    public async Task<IActionResult> TdsProfile([FromBody] TdsProfileRequest request, CancellationToken ct)
    {
        switch (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId))
        {
            case InternalTenantOutcome.Missing:
                return BadRequest(new MessageResponse { Message = "A customer and an organization are required to read a TDS profile." });
            case InternalTenantOutcome.Mismatch:
                return Forbid();
        }

        var db = _services.GetRequiredService<ContactsDbContext>();
        TdsProfile? profile = await db.Contacts.AsNoTracking()
            .Where(c => c.ContactId == request.ContactId)
            .Select(c => new TdsProfile
            {
                ContactId = c.ContactId,
                IsTdsApplicable = c.IsTdsApplicable,
                TdsSection = c.TdsSection,
                Pan = c.Pan,
                Tan = c.Tan,
                CertificateNo = c.LowerDeductionCertificateNo,
                CertificateRate = c.LowerDeductionRate,
                CertificateValidFrom = c.LowerDeductionValidFrom,
                CertificateValidTo = c.LowerDeductionValidTo,
                CertificateLimit = c.LowerDeductionLimit,
            })
            .FirstOrDefaultAsync(ct);

        return profile is null ? NotFound() : Ok(profile);
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
            .Where(c => c.IsGuardian && c.IsActive && !c.IsDraft
                && db.ContactPersons.Any(p => p.ContactId == c.ContactId && p.IsActive && p.MobileNumber == mobile))
            .OrderBy(c => c.ContactId)
            .Select(c => (long?)c.ContactId)
            .FirstOrDefaultAsync(ct);
        if (existing is long found)
        {
            return Ok(new EnsureGuardianResponse { ContactId = found, Created = false });
        }

        SaveContactResult created = await _services.GetRequiredService<ContactRepository>().CreateQuickAsync(new QuickContactRequest
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

    /// <summary>
    /// Each contact's legal name and billing address as fields (TK-91): the IRP's
    /// buyer block needs a PIN and a state code, which a document's printed
    /// address does not hold separately. The default billing address is taken,
    /// else the first active address (TK-188: an address has a kind now, not a
    /// billing/shipping type). The state code is the address's own, else the
    /// contact's place of supply.
    /// </summary>
    [HttpPost("addresses")]
    public async Task<IActionResult> Addresses([FromBody] ContactLookupRequest request, CancellationToken ct)
    {
        switch (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId))
        {
            case InternalTenantOutcome.Missing:
                return BadRequest(new MessageResponse { Message = "A customer and an organization are required to look up addresses." });
            case InternalTenantOutcome.Mismatch:
                return Forbid();
        }

        List<long> ids = [.. request.Ids.Distinct().Take(MaxIds)];
        if (ids.Count == 0)
        {
            return Ok(Array.Empty<ContactPostalAddress>());
        }

        var db = _services.GetRequiredService<ContactsDbContext>();
        var contacts = await db.Contacts.AsNoTracking()
            .Where(c => ids.Contains(c.ContactId))
            .Select(c => new
            {
                c.ContactId,
                c.DisplayName,
                c.LegalName,
                c.Gstin,
                c.PlaceOfSupplyStateId,
                c.GstRegistrationType,
                Address = db.ContactAddresses
                    .Where(a => a.ContactId == c.ContactId && a.IsActive)
                    .OrderBy(a => a.IsDefaultBilling ? 0 : 1)
                    .ThenBy(a => a.ContactAddressId)
                    .Select(a => new
                    {
                        a.AddressLine1,
                        a.AddressLine2,
                        a.City,
                        a.PostalCode,
                        a.StateId,
                        a.Gstin,
                        // The person to ask for at this address (TK-188), else
                        // the contact's default person.
                        Phone = db.ContactPersons
                            .Where(p => p.ContactId == c.ContactId && p.IsActive
                                && (p.ContactAddressId == a.ContactAddressId || p.IsDefault)
                                && (p.MobileNumber != null || p.PhoneNumber != null))
                            .OrderBy(p => p.ContactAddressId == a.ContactAddressId ? 0 : 1)
                            .Select(p => p.MobileNumber ?? p.PhoneNumber)
                            .FirstOrDefault(),
                    })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        IStateDirectory states = _services.GetRequiredService<IStateDirectory>();
        var found = new List<ContactPostalAddress>(contacts.Count);
        foreach (var c in contacts)
        {
            int? stateId = c.Address?.StateId ?? c.PlaceOfSupplyStateId;
            found.Add(new ContactPostalAddress
            {
                ContactId = c.ContactId,
                LegalName = string.IsNullOrWhiteSpace(c.LegalName) ? c.DisplayName : c.LegalName,
                Gstin = c.Address?.Gstin ?? c.Gstin,
                AddressLine1 = c.Address?.AddressLine1,
                AddressLine2 = c.Address?.AddressLine2,
                City = c.Address?.City,
                PostalCode = c.Address?.PostalCode,
                StateCode = stateId is int id ? await states.GetStateCodeAsync(id, ct) : null,
                PhoneNumber = c.Address?.Phone,
                RegistrationType = c.GstRegistrationType.ToString(),
            });
        }

        return Ok(found);
    }
}
