using Master.Entity.Enums;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

/// <summary>One per Customer (D-36, TK-127). A Trial licence is created automatically at signup.</summary>
public class License : AuditableEntity
{
    public Guid LicenseId { get; set; }

    public Guid CustomerId { get; set; }

    public LicenseType LicenseType { get; set; } = LicenseType.Trial;

    public DateOnly StartDate { get; set; }

    /// <summary>Trial = StartDate + 14 days.</summary>
    public DateOnly ExpiryDate { get; set; }

    /// <summary>
    /// Users a branch may hold, active or not (TK-209). Null means no limit,
    /// which is Elite's: the 10,000 an unbilled customer used to carry as a
    /// stand-in is gone.
    /// </summary>
    public int? MaxUsers { get; set; } = 3;

    /// <summary>
    /// Sales documents a branch may create in a calendar month (D-69, TK-210).
    /// Null means no limit, which is Elite's.
    /// </summary>
    public int? MonthlySalesTransactionLimit { get; set; }

    /// <summary>Purchase documents a branch may create in a calendar month; null means no limit.</summary>
    public int? MonthlyPurchaseTransactionLimit { get; set; }

    public int MaxOrganizations { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    /// <summary>Read-only access window after expiry, if any.</summary>
    public int GraceDays { get; set; }
}
