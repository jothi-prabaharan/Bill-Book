using Customer.Entity.Enums;
using Customer.Entity.Models;
using Customer.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;

namespace Customer.Repository.Repositories;

public sealed class EmailConsentRepository
{
    private readonly CustomerDbContext _db;

    public EmailConsentRepository(CustomerDbContext db) => _db = db;

    public async Task<EmailConsentDto> RecordConsentAsync(
        string email,
        ConsentStatus status,
        ConsentSource source,
        CancellationToken ct)
    {
        string normalized = email.Trim().ToLowerInvariant();

        var existing = await _db.EmailConsents
            .FirstOrDefaultAsync(c => c.Email == normalized, ct);

        if (existing is not null)
        {
            existing.Status = status;
            existing.Source = source;
            existing.ChangedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);

            return new EmailConsentDto
            {
                EmailConsentId = existing.EmailConsentId,
                Email = existing.Email,
                Status = existing.Status,
                Source = existing.Source,
                ChangedAt = existing.ChangedAt
            };
        }

        var consent = new EmailConsent
        {
            Email = normalized,
            Status = status,
            Source = source,
            ChangedAt = DateTimeOffset.UtcNow
        };

        _db.EmailConsents.Add(consent);
        await _db.SaveChangesAsync(ct);

        return new EmailConsentDto
        {
            EmailConsentId = consent.EmailConsentId,
            Email = consent.Email,
            Status = consent.Status,
            Source = consent.Source,
            ChangedAt = consent.ChangedAt
        };
    }

    public async Task<EmailConsentDto?> GetConsentAsync(string email, CancellationToken ct)
    {
        string normalized = email.Trim().ToLowerInvariant();

        var consent = await _db.EmailConsents
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Email == normalized, ct);

        if (consent is null) return null;

        return new EmailConsentDto
        {
            EmailConsentId = consent.EmailConsentId,
            Email = consent.Email,
            Status = consent.Status,
            Source = consent.Source,
            ChangedAt = consent.ChangedAt
        };
    }

    public async Task<List<EmailConsentDto>> ListConsentsAsync(
        string? search,
        ConsentStatus? status,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var query = _db.EmailConsents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToLowerInvariant();
            query = query.Where(c => c.Email.Contains(term));
        }

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        return await query
            .OrderByDescending(c => c.ChangedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new EmailConsentDto
            {
                EmailConsentId = c.EmailConsentId,
                Email = c.Email,
                Status = c.Status,
                Source = c.Source,
                ChangedAt = c.ChangedAt
            })
            .ToListAsync(ct);
    }

    public async Task<HashSet<string>> GetSuppressedEmailsAsync(
        IEnumerable<string> emails,
        CancellationToken ct)
    {
        var distinctEmails = emails
            .Select(e => e.Trim().ToLowerInvariant())
            .Where(e => e.Length > 0)
            .Distinct()
            .ToList();

        if (distinctEmails.Count == 0) return [];

        var suppressed = await _db.EmailConsents
            .AsNoTracking()
            .Where(c => distinctEmails.Contains(c.Email) &&
                        (c.Status == ConsentStatus.Unsubscribed || c.Status == ConsentStatus.Bounced))
            .Select(c => c.Email)
            .ToListAsync(ct);

        return suppressed.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> IsSuppressedAsync(string email, CancellationToken ct)
    {
        string normalized = email.Trim().ToLowerInvariant();
        return await _db.EmailConsents
            .AsNoTracking()
            .AnyAsync(c => c.Email == normalized &&
                          (c.Status == ConsentStatus.Unsubscribed || c.Status == ConsentStatus.Bounced), ct);
    }
}
