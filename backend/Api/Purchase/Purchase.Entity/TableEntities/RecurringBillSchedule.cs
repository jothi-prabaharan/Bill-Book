using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Recurring;
using Shared.Kernel.Tenancy;

namespace Purchase.Entity.TableEntities;

/// <summary>
/// A bill entered again on a schedule (TK-139): rent, a maintenance contract, a
/// subscription. The schedule names an existing bill as its template, and each
/// occurrence copies that bill's vendor and lines into a new one dated on the
/// occurrence, priced at the rates in force that day.
/// </summary>
public class RecurringBillSchedule : OrgScopedEntity
{
    public long RecurringBillScheduleId { get; set; }

    [Required(ErrorMessage = "Name the schedule.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    /// <summary>The bill each occurrence copies. A real foreign key; it may not be deleted while a schedule names it.</summary>
    public long TemplateBillId { get; set; }

    public RecurrenceFrequency Frequency { get; set; }

    [Range(1, 12, ErrorMessage = "Repeat every 1 to 12 periods.")]
    public int Every { get; set; } = 1;

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [Range(1, 1000, ErrorMessage = "Stop after 1 to 1000 occurrences.")]
    public int? MaxOccurrences { get; set; }

    /// <summary>The occurrence raised next, counted from the start date; a pause skips occurrences.</summary>
    public int NextOccurrenceIndex { get; set; }

    /// <summary>The date of occurrence <see cref="NextOccurrenceIndex"/>; null once the schedule has ended.</summary>
    public DateOnly? NextRunDate { get; set; }

    public int OccurrencesCreated { get; set; }

    public RecurringScheduleStatus Status { get; set; } = RecurringScheduleStatus.Active;

    /// <summary>
    /// Post each new bill. Off by default for bills: the vendor's own bill
    /// number is not known until their bill arrives.
    /// </summary>
    public bool AutoPost { get; set; }

    /// <summary>
    /// Each new bill's vendor bill number is this prefix and the occurrence date
    /// (<c>REC-20261101</c>), unique per vendor, until the real number is typed in.
    /// </summary>
    [Required(ErrorMessage = "Give a vendor bill number prefix.")]
    [MaxLength(30, ErrorMessage = "The prefix cannot exceed 30 characters.")]
    public string VendorBillNoPrefix { get; set; } = "REC-";

    /// <summary>The two-digit state the supply is made in; left null, the vendor's GSTIN decides.</summary>
    [MaxLength(2, ErrorMessage = "Place of supply must be a 2-digit state code.")]
    public string? PlaceOfSupplyStateCode { get; set; }

    public DateTimeOffset? LastRunAt { get; set; }

    [MaxLength(500, ErrorMessage = "Error text cannot exceed 500 characters.")]
    public string? LastError { get; set; }
}

/// <summary>
/// One occurrence a schedule raised. Unique on schedule and date: the fixed
/// document key that makes a rerun, or a second worker, raise nothing twice.
/// </summary>
public class RecurringBillRun : OrgScopedEntity
{
    public long RecurringBillRunId { get; set; }

    public long RecurringBillScheduleId { get; set; }

    public DateOnly OccurrenceDate { get; set; }

    public int OccurrenceIndex { get; set; }

    public long BillId { get; set; }

    public bool Posted { get; set; }

    /// <summary>Why posting was refused. The bill stays a draft.</summary>
    [MaxLength(500, ErrorMessage = "Error text cannot exceed 500 characters.")]
    public string? PostError { get; set; }
}
