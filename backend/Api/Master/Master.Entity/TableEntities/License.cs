using Master.Entity.Enums;
using Shared.Kernel.Apps;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

/// <summary>One per Customer. A Trial licence is created automatically at signup.</summary>
public class License : AuditableEntity
{
    public Guid LicenseId { get; set; }

    public Guid CustomerId { get; set; }

    /// <summary>
    /// The app this licence is for (TK-42). One licence per app per customer, so
    /// a lapsed RetailErp trial leaves Payroll running.
    /// </summary>
    public App App { get; set; } = App.RetailErp;

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
