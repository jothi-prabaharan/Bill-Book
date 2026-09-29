using System.Text.Json;
using Customer.Entity.Models;
using Customer.Entity.TableEntities;
using Customer.Repository.Clients;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;

namespace Customer.Repository.Repositories;

public sealed class AudienceRepository
{
    private readonly CustomerDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IContactsClient _contactsClient;
    private readonly IReportingClient _reportingClient;
    private readonly EmailConsentRepository _consentRepo;

    public AudienceRepository(
        CustomerDbContext db,
        ITenantContext tenant,
        IContactsClient contactsClient,
        IReportingClient reportingClient,
        EmailConsentRepository consentRepo)
    {
        _db = db;
        _tenant = tenant;
        _contactsClient = contactsClient;
        _reportingClient = reportingClient;
        _consentRepo = consentRepo;
    }

    public async Task<List<AudienceDto>> ListAsync(CancellationToken ct)
    {
        var audiences = await _db.Audiences
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

        return audiences.Select(MapToDto).ToList();
    }

    public async Task<AudienceDto?> GetByIdAsync(long id, CancellationToken ct)
    {
        var a = await _db.Audiences
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AudienceId == id, ct);

        return a is null ? null : MapToDto(a);
    }

    public async Task<AudienceDto> CreateAsync(SaveAudienceRequest request, CancellationToken ct)
    {
        var audience = new Audience
        {
            Name = request.Name.Trim(),
            Rules = JsonSerializer.Serialize(request.Rules),
            IsActive = request.IsActive
        };

        _db.Audiences.Add(audience);
        await _db.SaveChangesAsync(ct);

        return MapToDto(audience);
    }

    public async Task<AudienceDto?> UpdateAsync(long id, SaveAudienceRequest request, CancellationToken ct)
    {
        var audience = await _db.Audiences.FirstOrDefaultAsync(x => x.AudienceId == id, ct);
        if (audience is null) return null;

        audience.Name = request.Name.Trim();
        audience.Rules = JsonSerializer.Serialize(request.Rules);
        audience.IsActive = request.IsActive;

        await _db.SaveChangesAsync(ct);

        return MapToDto(audience);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct)
    {
        var audience = await _db.Audiences.FirstOrDefaultAsync(x => x.AudienceId == id, ct);
        if (audience is null) return false;

        _db.Audiences.Remove(audience);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Resolves recipients for an audience rule set, deduplicating by email,
    /// excluding manual exclusions, and filtering out unsubscribed/bounced addresses.
    /// </summary>
    public async Task<AudiencePreviewResponse> ResolvePreviewAsync(AudienceRules rules, CancellationToken ct)
    {
        var candidateRecipients = new Dictionary<string, AudienceRecipientPreview>(StringComparer.OrdinalIgnoreCase);

        // 1. Leads
        if (rules.Leads is not null)
        {
            var leadQuery = _db.Leads
                .AsNoTracking()
                .Where(l => !string.IsNullOrEmpty(l.Email));

            if (rules.Leads.Statuses is { Count: > 0 } statuses)
            {
                leadQuery = leadQuery.Where(l => statuses.Contains(l.Status));
            }

            if (rules.Leads.Sources is { Count: > 0 } sources)
            {
                leadQuery = leadQuery.Where(l => sources.Contains(l.Source));
            }

            if (rules.Leads.CreatedFrom.HasValue)
            {
                leadQuery = leadQuery.Where(l => l.CreatedAt >= rules.Leads.CreatedFrom.Value);
            }

            if (rules.Leads.CreatedTo.HasValue)
            {
                leadQuery = leadQuery.Where(l => l.CreatedAt <= rules.Leads.CreatedTo.Value);
            }

            var leads = await leadQuery
                .Select(l => new { l.LeadId, l.Name, l.Email })
                .ToListAsync(ct);

            foreach (var lead in leads)
            {
                string email = lead.Email!.Trim().ToLowerInvariant();
                candidateRecipients[email] = new AudienceRecipientPreview
                {
                    Email = email,
                    Name = lead.Name,
                    SourceType = "Lead",
                    LeadId = lead.LeadId
                };
            }
        }

        // 2. Contacts
        if (rules.Contacts is not null || rules.PurchaseHistory is not null)
        {
            var allContacts = await _contactsClient.ListContactsAsync(ct);
            var filteredContacts = allContacts.Where(c => !string.IsNullOrWhiteSpace(c.Email));

            if (rules.Contacts is not null)
            {
                if (rules.Contacts.IsCustomer.HasValue)
                {
                    filteredContacts = filteredContacts.Where(c => c.IsCustomer == rules.Contacts.IsCustomer.Value);
                }

                if (rules.Contacts.IsVendor.HasValue)
                {
                    filteredContacts = filteredContacts.Where(c => c.IsVendor == rules.Contacts.IsVendor.Value);
                }

                if (!string.IsNullOrWhiteSpace(rules.Contacts.City))
                {
                    filteredContacts = filteredContacts.Where(c =>
                        string.Equals(c.City, rules.Contacts.City, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(rules.Contacts.Category))
                {
                    filteredContacts = filteredContacts.Where(c =>
                        string.Equals(c.ContactCategory, rules.Contacts.Category, StringComparison.OrdinalIgnoreCase));
                }
            }

            // Purchase history segment from Reporting
            if (rules.PurchaseHistory is not null)
            {
                Guid customerId = _tenant.CustomerId ?? Guid.Empty;
                Guid orgId = _tenant.OrgId ?? Guid.Empty;
                var segmentIds = await _reportingClient.SegmentContactsAsync(customerId, orgId, rules.PurchaseHistory, ct);
                var segmentSet = segmentIds.ToHashSet();
                filteredContacts = filteredContacts.Where(c => segmentSet.Contains(c.ContactId));
            }

            foreach (var contact in filteredContacts)
            {
                string email = contact.Email!.Trim().ToLowerInvariant();
                // Overwrite or set contact info
                candidateRecipients[email] = new AudienceRecipientPreview
                {
                    Email = email,
                    Name = contact.DisplayName,
                    SourceType = "Contact",
                    ContactId = contact.ContactId
                };
            }
        }

        // 3. Manual inclusions
        if (rules.ManualInclusions is not null)
        {
            if (rules.ManualInclusions.Emails is { Count: > 0 } inclEmails)
            {
                foreach (string raw in inclEmails)
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    string email = raw.Trim().ToLowerInvariant();
                    if (!candidateRecipients.ContainsKey(email))
                    {
                        candidateRecipients[email] = new AudienceRecipientPreview
                        {
                            Email = email,
                            SourceType = "Manual"
                        };
                    }
                }
            }

            if (rules.ManualInclusions.LeadIds is { Count: > 0 } inclLeadIds)
            {
                var extraLeads = await _db.Leads
                    .AsNoTracking()
                    .Where(l => inclLeadIds.Contains(l.LeadId) && !string.IsNullOrEmpty(l.Email))
                    .Select(l => new { l.LeadId, l.Name, l.Email })
                    .ToListAsync(ct);

                foreach (var lead in extraLeads)
                {
                    string email = lead.Email!.Trim().ToLowerInvariant();
                    candidateRecipients[email] = new AudienceRecipientPreview
                    {
                        Email = email,
                        Name = lead.Name,
                        SourceType = "Lead",
                        LeadId = lead.LeadId
                    };
                }
            }
        }

        // 4. Manual exclusions
        if (rules.ManualExclusions is not null)
        {
            if (rules.ManualExclusions.Emails is { Count: > 0 } exclEmails)
            {
                foreach (string raw in exclEmails)
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    candidateRecipients.Remove(raw.Trim().ToLowerInvariant());
                }
            }

            if (rules.ManualExclusions.LeadIds is { Count: > 0 } exclLeadIds)
            {
                var removeLeadEmails = candidateRecipients.Values
                    .Where(r => r.LeadId.HasValue && exclLeadIds.Contains(r.LeadId.Value))
                    .Select(r => r.Email)
                    .ToList();

                foreach (string email in removeLeadEmails)
                {
                    candidateRecipients.Remove(email);
                }
            }

            if (rules.ManualExclusions.ContactIds is { Count: > 0 } exclContactIds)
            {
                var removeContactEmails = candidateRecipients.Values
                    .Where(r => r.ContactId.HasValue && exclContactIds.Contains(r.ContactId.Value))
                    .Select(r => r.Email)
                    .ToList();

                foreach (string email in removeContactEmails)
                {
                    candidateRecipients.Remove(email);
                }
            }
        }

        // 5. Consent and suppression check: Unsubscribed and Bounced are suppressed
        var allCandidateEmails = candidateRecipients.Keys.ToList();
        var suppressedSet = await _consentRepo.GetSuppressedEmailsAsync(allCandidateEmails, ct);

        var subscribedRecipients = candidateRecipients.Values
            .Where(r => !suppressedSet.Contains(r.Email))
            .ToList();

        return new AudiencePreviewResponse
        {
            TotalSubscribedRecipients = subscribedRecipients.Count,
            TotalSuppressedRecipients = candidateRecipients.Count - subscribedRecipients.Count,
            SampleRecipients = subscribedRecipients.Take(50).ToList()
        };
    }

    public async Task<AudiencePreviewResponse?> ResolveForAudienceAsync(long id, CancellationToken ct)
    {
        var audience = await _db.Audiences.AsNoTracking().FirstOrDefaultAsync(x => x.AudienceId == id, ct);
        if (audience is null) return null;

        var dto = MapToDto(audience);
        return await ResolvePreviewAsync(dto.Rules, ct);
    }

    private static AudienceDto MapToDto(Audience a)
    {
        AudienceRules parsedRules;
        try
        {
            parsedRules = JsonSerializer.Deserialize<AudienceRules>(a.Rules) ?? new();
        }
        catch
        {
            parsedRules = new();
        }

        return new AudienceDto
        {
            AudienceId = a.AudienceId,
            Name = a.Name,
            Rules = parsedRules,
            IsActive = a.IsActive,
            CreatedAt = a.CreatedAt,
            ModifiedAt = a.ModifiedAt
        };
    }
}
