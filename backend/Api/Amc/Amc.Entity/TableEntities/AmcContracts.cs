using System.ComponentModel.DataAnnotations;
using Amc.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Amc.Entity.TableEntities;

/// <summary>
/// An annual maintenance contract with a vendor (S8, TK-68). Its value is billed
/// through Purchase as a bill and never posted from here.
/// </summary>
public class AmcContract : OrgScopedEntity
{
    public long AmcContractId { get; set; }

    /// <summary>The vendor's own number for the contract.</summary>
    [Required(ErrorMessage = "Contract number is required.")]
    [MaxLength(30, ErrorMessage = "Contract number cannot exceed 30 characters.")]
    public string ContractNo { get; set; } = null!;

    /// <summary>Unenforced: <c>con.Contacts</c>, an active vendor, checked through Master.</summary>
    public long VendorContactId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    [Range(typeof(decimal), "0", "1000000000", ErrorMessage = "Contract value cannot be negative.")]
    public decimal ContractValue { get; set; }

    public BillingFrequency BillingFrequency { get; set; } = BillingFrequency.Annual;

    [Range(0, 365, ErrorMessage = "Visits per year must be between 0 and 365.")]
    public int VisitsPerYear { get; set; }

    public AmcCoverage AmcCoverage { get; set; } = AmcCoverage.NonComprehensive;

    /// <summary>Remind this many days before the end date; 0 sends no reminder.</summary>
    [Range(0, 365, ErrorMessage = "Reminder days must be between 0 and 365.")]
    public int RenewalReminderDays { get; set; } = 30;

    /// <summary>Who the renewal reminder is written to.</summary>
    [EmailAddress(ErrorMessage = "Reminder email must be a valid email address.")]
    [MaxLength(200, ErrorMessage = "Reminder email cannot exceed 200 characters.")]
    public string? ReminderEmail { get; set; }

    public ContractStatus ContractStatus { get; set; } = ContractStatus.Draft;

    [MaxLength(500, ErrorMessage = "Termination reason cannot exceed 500 characters.")]
    public string? TerminationReason { get; set; }

    [MaxLength(1000, ErrorMessage = "Remarks cannot exceed 1000 characters.")]
    public string? Remarks { get; set; }
}

/// <summary>An asset a contract covers. An asset may be under only one active contract.</summary>
public class AmcCoveredAsset : OrgScopedEntity
{
    public long AmcCoveredAssetId { get; set; }

    public long AmcContractId { get; set; }

    /// <summary>Unenforced: <c>fac.FacilityAssets</c>, checked through Facility.</summary>
    public long FacilityAssetId { get; set; }
}

/// <summary>A vendor's visit, scheduled or for a breakdown. It may raise a work order.</summary>
public class AmcVisit : OrgScopedEntity
{
    public long AmcVisitId { get; set; }

    public long AmcContractId { get; set; }

    public DateOnly VisitDate { get; set; }

    public VisitKind VisitKind { get; set; } = VisitKind.Scheduled;

    /// <summary>Unenforced: <c>fac.FacilityAssets</c>. One the contract covers.</summary>
    public long? FacilityAssetId { get; set; }

    [MaxLength(1000, ErrorMessage = "Remarks cannot exceed 1000 characters.")]
    public string? Remarks { get; set; }

    /// <summary>Unenforced: <c>wrk.WorkOrders</c>.</summary>
    public long? WorkOrderId { get; set; }

    [MaxLength(30, ErrorMessage = "Work order number cannot exceed 30 characters.")]
    public string? WorkOrderNo { get; set; }
}
