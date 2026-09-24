using System.ComponentModel.DataAnnotations;

namespace Hrm.Entity.Models;

/// <summary>A code-and-name master: a designation or a cost centre (TK-48).</summary>
public class SaveOrgMasterRequest
{
    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(20, ErrorMessage = "Code cannot exceed 20 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}

public class SaveDepartmentRequest : SaveOrgMasterRequest
{
    public long? HeadEmployeeId { get; set; }

    public long? ParentDepartmentId { get; set; }
}

public class SaveGradeRequest : SaveOrgMasterRequest
{
    public int SortOrder { get; set; }

    [Range(0, 365, ErrorMessage = "Notice period must be between 0 and 365 days.")]
    public int NoticePeriodDays { get; set; } = 30;
}

public class SaveWorkLocationRequest : SaveOrgMasterRequest
{
    public int? StateId { get; set; }

    [MaxLength(200, ErrorMessage = "Address cannot exceed 200 characters.")]
    public string? AddressLine1 { get; set; }

    [MaxLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    public string? City { get; set; }

    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
    public decimal? Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
    public decimal? Longitude { get; set; }

    [Range(1, 100000, ErrorMessage = "The geo-fence must be between 1 and 100000 metres.")]
    public int? GeoFenceMetres { get; set; }
}

/// <summary>One row of any organisation master, as its list shows it.</summary>
public class OrgMasterRow
{
    public long Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    public long? HeadEmployeeId { get; set; }

    public long? ParentDepartmentId { get; set; }

    public int? SortOrder { get; set; }

    public int? NoticePeriodDays { get; set; }

    public int? StateId { get; set; }

    public string? AddressLine1 { get; set; }

    public string? City { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public int? GeoFenceMetres { get; set; }
}

public enum OrgMasterOutcome
{
    Ok = 1,
    NotFound = 2,

    /// <summary>Another row of the branch already has this code.</summary>
    DuplicateCode = 3,

    /// <summary>A parent or head that is not in this branch, or a department made its own ancestor.</summary>
    InvalidReference = 4,
}

public sealed record OrgMasterResult(OrgMasterOutcome Outcome, long Id = 0);

public class SaveAnnouncementRequest
{
    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
    public string Title { get; set; } = null!;

    [Required(ErrorMessage = "Body is required.")]
    [MaxLength(4000, ErrorMessage = "Body cannot exceed 4000 characters.")]
    public string Body { get; set; } = null!;

    [Required(ErrorMessage = "Publish date is required.")]
    public DateOnly PublishDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public Enums.AnnouncementAudience Audience { get; set; } = Enums.AnnouncementAudience.Everyone;

    public long? AudienceRefId { get; set; }

    public bool IsPinned { get; set; }
}

public class AnnouncementRow : SaveAnnouncementRequest
{
    public long AnnouncementId { get; set; }
}

public class SavePolicyDocumentRequest
{
    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
    public string Title { get; set; } = null!;

    [Required(ErrorMessage = "The file is required.")]
    [MaxLength(500, ErrorMessage = "File key cannot exceed 500 characters.")]
    public string AttachmentKey { get; set; } = null!;

    [Required(ErrorMessage = "Effective date is required.")]
    public DateOnly EffectiveDate { get; set; }

    public bool IsAcknowledgementRequired { get; set; }

    public bool IsActive { get; set; } = true;
}

public class PolicyDocumentRow : SavePolicyDocumentRequest
{
    public long PolicyDocumentId { get; set; }

    public int Acknowledgements { get; set; }
}

public enum NoticeOutcome
{
    Ok = 1,
    NotFound = 2,
    ExpiresBeforePublish = 3,

    /// <summary>A department, location or grade audience with no id.</summary>
    AudienceMissing = 4,
}

public sealed record NoticeResult(NoticeOutcome Outcome, long Id = 0);

/// <summary>A refusal's sentence for a person to read. Never names a table, column or figure.</summary>
public sealed record HrmMessage(string Message);
