using Platform.Entity.Enums;
using Shared.Kernel.Entities;

namespace Platform.Entity.TableEntities;

/// <summary>One per Customer. A Trial licence is created automatically at signup.</summary>
public class License : AuditableEntity
{
    public Guid LicenseId { get; set; }

    public Guid CustomerId { get; set; }

    public LicenseType LicenseType { get; set; } = LicenseType.Trial;

    public DateOnly StartDate { get; set; }

    /// <summary>Trial = StartDate + 14 days.</summary>
    public DateOnly ExpiryDate { get; set; }

    public int MaxUsers { get; set; } = 3;

    public int MaxOrganizations { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    /// <summary>Read-only access window after expiry, if any.</summary>
    public int GraceDays { get; set; }
}
