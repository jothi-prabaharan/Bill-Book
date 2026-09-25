using System.ComponentModel.DataAnnotations;
using MaintenanceContract.Entity.Enums;

namespace MaintenanceContract.Entity.Models;

// Requests and views for the amc API (S8, TK-68).

public sealed record AmcMessage(string Message);

public sealed class SaveContractRequest
{
    [Required(ErrorMessage = "Contract number is required.")]
    [MaxLength(30, ErrorMessage = "Contract number cannot exceed 30 characters.")]
    public string ContractNo { get; set; } = null!;

    [Range(1, long.MaxValue, ErrorMessage = "Choose the vendor.")]
    public long VendorContactId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    [Range(typeof(decimal), "0", "1000000000", ErrorMessage = "Contract value cannot be negative.")]
    public decimal ContractValue { get; set; }

    public BillingFrequency BillingFrequency { get; set; } = BillingFrequency.Annual;

    [Range(0, 365, ErrorMessage = "Visits per year must be between 0 and 365.")]
    public int VisitsPerYear { get; set; }

    public AmcCoverage AmcCoverage { get; set; } = AmcCoverage.NonComprehensive;

    [Range(0, 365, ErrorMessage = "Reminder days must be between 0 and 365.")]
    public int RenewalReminderDays { get; set; } = 30;

    [EmailAddress(ErrorMessage = "Reminder email must be a valid email address.")]
    [MaxLength(200, ErrorMessage = "Reminder email cannot exceed 200 characters.")]
    public string? ReminderEmail { get; set; }

    [MaxLength(1000, ErrorMessage = "Remarks cannot exceed 1000 characters.")]
    public string? Remarks { get; set; }

    /// <summary>The assets covered. Replaces the list.</summary>
    public List<long> FacilityAssetIds { get; set; } = [];
}

public sealed class TerminateContractRequest
{
    [Required(ErrorMessage = "Give a reason to terminate.")]
    [MaxLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
    public string Reason { get; set; } = null!;
}

public sealed class RecordVisitRequest
{
    public DateOnly VisitDate { get; set; }

    public VisitKind VisitKind { get; set; } = VisitKind.Scheduled;

    public long? FacilityAssetId { get; set; }

    [MaxLength(1000, ErrorMessage = "Remarks cannot exceed 1000 characters.")]
    public string? Remarks { get; set; }

    /// <summary>Raise a work order for the vendor to attend. Needs an asset.</summary>
    public bool RaiseWorkOrder { get; set; }
}

public sealed class ContractView
{
    public long AmcContractId { get; set; }

    public string ContractNo { get; set; } = null!;

    public long VendorContactId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public decimal ContractValue { get; set; }

    public BillingFrequency BillingFrequency { get; set; }

    public int VisitsPerYear { get; set; }

    public AmcCoverage AmcCoverage { get; set; }

    public int RenewalReminderDays { get; set; }

    public string? ReminderEmail { get; set; }

    /// <summary>Expired once the end date has passed, whatever is stored.</summary>
    public ContractStatus ContractStatus { get; set; }

    public string? TerminationReason { get; set; }

    public string? Remarks { get; set; }

    public List<long> FacilityAssetIds { get; set; } = [];

    public int VisitsMade { get; set; }
}

public sealed class VisitView
{
    public long AmcVisitId { get; set; }

    public long AmcContractId { get; set; }

    public DateOnly VisitDate { get; set; }

    public VisitKind VisitKind { get; set; }

    public long? FacilityAssetId { get; set; }

    public string? Remarks { get; set; }

    public long? WorkOrderId { get; set; }

    public string? WorkOrderNo { get; set; }
}
