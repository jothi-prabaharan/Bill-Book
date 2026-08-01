using Contacts.Entity.Enums;
using Contacts.Entity.Models;
using Contacts.Entity.TableEntities;
using Contacts.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Storage;
using Shared.Kernel.Tenancy;

namespace Contacts.Api.Services;

/// <summary>
/// Files held against a contact. Separate from the contact aggregate because a
/// file cannot travel in a JSON body, and because uploading one should not mean
/// resubmitting the whole contact.
/// </summary>
public sealed class ContactAttachmentService
{
    private readonly ContactsDbContext _db;
    private readonly IFileStorage _storage;
    private readonly ITenantContext _tenant;
    private readonly IConfiguration _config;
    private readonly TimeProvider _clock;

    public ContactAttachmentService(
        ContactsDbContext db,
        IFileStorage storage,
        ITenantContext tenant,
        IConfiguration config,
        TimeProvider clock)
    {
        _db = db;
        _storage = storage;
        _tenant = tenant;
        _config = config;
        _clock = clock;
    }

    /// <summary>Default 10 MB. A scanned certificate is well under it; a video is not a document.</summary>
    private long MaxBytes =>
        long.TryParse(_config["FileStorage:MaxBytes"], out long configured) ? configured : 10_485_760;

    /// <summary>
    /// What may be uploaded. An allowlist rather than a blocklist: the question
    /// is which types this feature exists to hold, not which are dangerous today.
    /// </summary>
    private string[] AllowedContentTypes =>
        _config.GetSection("FileStorage:AllowedContentTypes").Get<string[]>()
        ?? ["application/pdf", "image/jpeg", "image/png", "image/webp", "image/tiff"];

    public async Task<IReadOnlyList<ContactAttachmentModel>> ListAsync(
        long contactId, CancellationToken ct)
    {
        List<ContactAttachment> rows = await _db.ContactAttachments
            .Where(a => a.ContactId == contactId && a.IsActive)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        var models = new List<ContactAttachmentModel>(rows.Count);

        foreach (ContactAttachment row in rows)
        {
            ContactAttachmentModel model = Map(row);

            // Null when the store cannot mint one — local disk cannot — and the
            // client falls back to the download endpoint.
            Uri? url = await _storage.GetDownloadUrlAsync(
                row.StoragePath, TimeSpan.FromMinutes(15), ct);

            model.DownloadUrl = url?.ToString();
            models.Add(model);
        }

        return models;
    }

    public async Task<(AttachmentOutcome Outcome, ContactAttachmentModel? Attachment)> UploadAsync(
        long contactId,
        UploadAttachmentRequest request,
        string fileName,
        string contentType,
        long length,
        Stream content,
        CancellationToken ct)
    {
        if (!await _db.Contacts.AnyAsync(c => c.ContactId == contactId, ct))
        {
            return (AttachmentOutcome.ContactNotFound, null);
        }

        if (length <= 0)
        {
            return (AttachmentOutcome.NoFile, null);
        }

        if (length > MaxBytes)
        {
            return (AttachmentOutcome.TooLarge, null);
        }

        // The browser's declared type, checked against the allowlist. It is not
        // proof of anything — it is a filter, and the reason downloads are
        // served with a fixed disposition rather than executed.
        if (!AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return (AttachmentOutcome.UnsupportedType, null);
        }

        if (!Enum.TryParse(request.DocumentType, ignoreCase: true, out AttachmentDocumentType type))
        {
            return (AttachmentOutcome.InvalidValue, null);
        }

        (Guid _, Guid orgId) = _tenant.Require();

        // Generated, never derived from the uploaded name: the organization is
        // the first segment, so the tenant boundary exists in the path as well
        // as in the row.
        string key = StorageKey.BuildKey(orgId, "contacts", contactId, fileName);
        await _storage.SaveAsync(key, content, contentType, ct);

        var attachment = new ContactAttachment
        {
            ContactId = contactId,
            DocumentType = type,
            FileName = Path.GetFileName(fileName),
            StoragePath = key,
            ContentType = contentType,
            FileSizeBytes = length,
            Description = request.Description,
            ExpiryDate = request.ExpiryDate,
            IsActive = true,
        };

        _db.ContactAttachments.Add(attachment);
        await _db.SaveChangesAsync(ct);

        return (AttachmentOutcome.Ok, Map(attachment));
    }

    /// <summary>
    /// Opens a file for download. The query is org-scoped by the global filter,
    /// so an attachment id from another organization simply is not found — the
    /// storage key is never taken from the caller.
    /// </summary>
    public async Task<(AttachmentOutcome Outcome, Stream? Content, ContactAttachment? Row)>
        OpenAsync(long attachmentId, CancellationToken ct)
    {
        ContactAttachment? row = await _db.ContactAttachments
            .FirstOrDefaultAsync(a => a.ContactAttachmentId == attachmentId && a.IsActive, ct);

        if (row is null)
        {
            return (AttachmentOutcome.NotFound, null, null);
        }

        Stream? content = await _storage.OpenReadAsync(row.StoragePath, ct);

        return content is null
            ? (AttachmentOutcome.NotFound, null, null)
            : (AttachmentOutcome.Ok, content, row);
    }

    /// <summary>
    /// Soft delete. The blob is left in place: the row is the audit trail, and a
    /// mistaken delete that also destroyed the bytes would be unrecoverable.
    /// Sweeping orphaned blobs is a separate, deliberate job.
    /// </summary>
    public async Task<AttachmentOutcome> DeleteAsync(long attachmentId, CancellationToken ct)
    {
        ContactAttachment? row = await _db.ContactAttachments
            .FirstOrDefaultAsync(a => a.ContactAttachmentId == attachmentId, ct);

        if (row is null)
        {
            return AttachmentOutcome.NotFound;
        }

        row.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return AttachmentOutcome.Ok;
    }

    /// <summary>
    /// Licences that have lapsed or are close to it. The point of holding an
    /// expiry at all: selling Schedule H stock to a chemist whose drug licence
    /// has expired is an offence, and nobody checks a date they cannot see.
    /// </summary>
    public async Task<IReadOnlyList<ExpiringLicenceItem>> ExpiringLicencesAsync(
        int withinDays, CancellationToken ct)
    {
        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        DateOnly cutoff = today.AddDays(withinDays);

        var rows = await _db.ContactLicences
            .Where(l => l.IsActive && l.ExpiresOn != null && l.ExpiresOn <= cutoff)
            .Join(
                _db.Contacts.Where(c => c.IsActive),
                l => l.ContactId,
                c => c.ContactId,
                (l, c) => new { Licence = l, Contact = c })
            .OrderBy(x => x.Licence.ExpiresOn)
            .ToListAsync(ct);

        return rows.Select(x => new ExpiringLicenceItem
        {
            ContactId = x.Contact.ContactId,
            ContactCode = x.Contact.ContactCode,
            DisplayName = x.Contact.DisplayName,
            ContactLicenceId = x.Licence.ContactLicenceId,
            LicenceType = x.Licence.LicenceType.ToString(),
            LicenceNumber = x.Licence.LicenceNumber,
            ExpiresOn = x.Licence.ExpiresOn!.Value,
            DaysUntilExpiry = x.Licence.ExpiresOn!.Value.DayNumber - today.DayNumber,
        }).ToList();
    }

    private static ContactAttachmentModel Map(ContactAttachment a) => new()
    {
        ContactAttachmentId = a.ContactAttachmentId,
        ContactId = a.ContactId,
        DocumentType = a.DocumentType.ToString(),
        FileName = a.FileName,
        ContentType = a.ContentType,
        FileSizeBytes = a.FileSizeBytes,
        Description = a.Description,
        ExpiryDate = a.ExpiryDate,
        UploadedAt = a.CreatedAt,
    };
}
