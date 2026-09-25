using Employee.Entity.Models;
using Employee.Entity.TableEntities;
using Employee.Repository;
using Microsoft.EntityFrameworkCore;

namespace Employee.Api.Services;

/// <summary>Announcements and policy documents (H1, TK-48). HRMS only.</summary>
public sealed class NoticeService
{
    private readonly EmployeeDbContext _db;

    public NoticeService(EmployeeDbContext db) => _db = db;

    public async Task<IReadOnlyList<AnnouncementRow>> AnnouncementsAsync(CancellationToken ct) =>
        await _db.Announcements.AsNoTracking()
            .OrderByDescending(a => a.IsPinned).ThenByDescending(a => a.PublishDate)
            .Select(a => new AnnouncementRow
            {
                AnnouncementId = a.AnnouncementId, Title = a.Title, Body = a.Body,
                PublishDate = a.PublishDate, ExpiryDate = a.ExpiryDate,
                Audience = a.Audience, AudienceRefId = a.AudienceRefId, IsPinned = a.IsPinned,
            })
            .ToListAsync(ct);

    public async Task<NoticeResult> SaveAnnouncementAsync(long? id, SaveAnnouncementRequest r, CancellationToken ct)
    {
        if (r.ExpiryDate is DateOnly expiry && expiry < r.PublishDate)
        {
            return new NoticeResult(NoticeOutcome.ExpiresBeforePublish);
        }

        if (r.Audience != Entity.Enums.AnnouncementAudience.Everyone && r.AudienceRefId is null)
        {
            return new NoticeResult(NoticeOutcome.AudienceMissing);
        }

        Announcement? row = id is long existing
            ? await _db.Announcements.FirstOrDefaultAsync(a => a.AnnouncementId == existing, ct)
            : new Announcement();
        if (row is null)
        {
            return new NoticeResult(NoticeOutcome.NotFound);
        }

        row.Title = r.Title.Trim();
        row.Body = r.Body.Trim();
        row.PublishDate = r.PublishDate;
        row.ExpiryDate = r.ExpiryDate;
        row.Audience = r.Audience;
        row.AudienceRefId = r.Audience == Entity.Enums.AnnouncementAudience.Everyone ? null : r.AudienceRefId;
        row.IsPinned = r.IsPinned;
        if (id is null)
        {
            _db.Announcements.Add(row);
        }

        await _db.SaveChangesAsync(ct);
        return new NoticeResult(NoticeOutcome.Ok, row.AnnouncementId);
    }

    public async Task<IReadOnlyList<PolicyDocumentRow>> PoliciesAsync(CancellationToken ct) =>
        await _db.PolicyDocuments.AsNoTracking()
            .OrderByDescending(p => p.EffectiveDate)
            .Select(p => new PolicyDocumentRow
            {
                PolicyDocumentId = p.PolicyDocumentId, Title = p.Title, AttachmentKey = p.AttachmentKey,
                EffectiveDate = p.EffectiveDate, IsAcknowledgementRequired = p.IsAcknowledgementRequired, IsActive = p.IsActive,
                Acknowledgements = _db.PolicyAcknowledgements.Count(a => a.PolicyDocumentId == p.PolicyDocumentId),
            })
            .ToListAsync(ct);

    public async Task<NoticeResult> SavePolicyAsync(long? id, SavePolicyDocumentRequest r, CancellationToken ct)
    {
        PolicyDocument? row = id is long existing
            ? await _db.PolicyDocuments.FirstOrDefaultAsync(p => p.PolicyDocumentId == existing, ct)
            : new PolicyDocument();
        if (row is null)
        {
            return new NoticeResult(NoticeOutcome.NotFound);
        }

        row.Title = r.Title.Trim();
        row.AttachmentKey = r.AttachmentKey.Trim();
        row.EffectiveDate = r.EffectiveDate;
        row.IsAcknowledgementRequired = r.IsAcknowledgementRequired;
        row.IsActive = r.IsActive;
        if (id is null)
        {
            _db.PolicyDocuments.Add(row);
        }

        await _db.SaveChangesAsync(ct);
        return new NoticeResult(NoticeOutcome.Ok, row.PolicyDocumentId);
    }
}
