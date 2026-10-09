using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Shared.Kernel.Tenancy;

namespace Site.Entity.TableEntities;

/// <summary>
/// Work-proof photos uploaded on clock-out (TK-171).
/// </summary>
[Table("SiteAttendancePhotos", Schema = "sit")]
public class SiteAttendancePhoto : OrgScopedEntity
{
    [Key]
    public long PhotoId { get; set; }

    [Required(ErrorMessage = "Attendance is required.")]
    public long AttendanceId { get; set; }

    [Required(ErrorMessage = "Storage key is required.")]
    [StringLength(255, ErrorMessage = "Storage key cannot exceed 255 characters.")]
    public string StorageKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "File name is required.")]
    [StringLength(200, ErrorMessage = "File name cannot exceed 200 characters.")]
    public string FileName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Content type is required.")]
    [StringLength(100, ErrorMessage = "Content type cannot exceed 100 characters.")]
    public string ContentType { get; set; } = string.Empty;

    [Range(1, 50000000, ErrorMessage = "File size must be between 1 byte and 50MB.")]
    public long FileSize { get; set; }

    [Required(ErrorMessage = "Uploaded at timestamp is required.")]
    public DateTime UploadedAt { get; set; }
}
