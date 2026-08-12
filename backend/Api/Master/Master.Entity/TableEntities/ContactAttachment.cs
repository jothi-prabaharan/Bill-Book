using System.ComponentModel.DataAnnotations;
using Master.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Master.Entity.TableEntities;

/// <summary>
/// A file held against a contact — GST certificate, agreement, cancelled cheque.
///
/// The row points at the file; the bytes live in blob storage. Keeping them out
/// of the database is what stops every backup and every restore carrying every
/// scanned document anyone ever uploaded.
/// </summary>
public class ContactAttachment : OrgScopedEntity
{
    public long ContactAttachmentId { get; set; }

    public long ContactId { get; set; }

    public AttachmentDocumentType DocumentType { get; set; } = AttachmentDocumentType.Other;

    /// <summary>The name the user uploaded, shown in the list. Never used as a path.</summary>
    [Required(ErrorMessage = "File name is required.")]
    [MaxLength(255, ErrorMessage = "File name cannot exceed 255 characters.")]
    public string FileName { get; set; } = null!;

    /// <summary>
    /// The storage key, generated rather than derived from the file name. Not a
    /// public URL: downloads go through the API, which checks the caller's
    /// organization before handing over a byte.
    /// </summary>
    [Required(ErrorMessage = "Storage path is required.")]
    [MaxLength(500, ErrorMessage = "Storage path cannot exceed 500 characters.")]
    public string StoragePath { get; set; } = null!;

    [Required(ErrorMessage = "Content type is required.")]
    [MaxLength(100, ErrorMessage = "Content type cannot exceed 100 characters.")]
    public string ContentType { get; set; } = null!;

    public long FileSizeBytes { get; set; }

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    /// <summary>Agreements and certificates expire; lets a report flag them before they do.</summary>
    public DateOnly? ExpiryDate { get; set; }

    /// <summary>Soft delete, so the audit trail and the blob both survive a mistake.</summary>
    public bool IsActive { get; set; } = true;
}
