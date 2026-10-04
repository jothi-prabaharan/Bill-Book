using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Recurring;
using Shared.Kernel.Tenancy;

namespace Sales.Entity.TableEntities;

/// <summary>
/// An invoice raised again on a schedule (TK-139): monthly rent, an annual
/// maintenance fee. The schedule names an existing invoice as its template, and
/// each occurrence copies that invoice's customer and lines into a new one dated
/// on the occurrence, priced at the rates in force that day.
/// </summary>
public class RecurringInvoiceSchedule : OrgScopedEntity
{
    public long RecurringInvoiceScheduleId { get; set; }

    [Required(ErrorMessage = "Name the schedule.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    /// <summary>The invoice each occurrence copies. A real foreign key; it may not be deleted while a schedule names it.</summary>
    public long TemplateInvoiceId { get; set; }

    public RecurrenceFrequency Frequency { get; set; }

    /// <summary>Every 1 month, every 2 weeks, and so on.</summary>
    [Range(1, 12, ErrorMessage = "Repeat every 1 to 12 periods.")]
    public int Every { get; set; } = 1;

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    /// <summary>Stop after this many occurrences. Null is no limit.</summary>
    [Range(1, 1000, ErrorMessage = "Stop after 1 to 1000 occurrences.")]
    public int? MaxOccurrences { get; set; }

    /// <summary>
    /// The occurrence raised next, counted from the start date. Kept rather than
    /// derived from the run count, because a pause skips occurrences.
    /// </summary>
    public int NextOccurrenceIndex { get; set; }

    /// <summary>The date of occurrence <see cref="NextOccurrenceIndex"/>; null once the schedule has ended.</summary>
    public DateOnly? NextRunDate { get; set; }

    public int OccurrencesCreated { get; set; }

    public RecurringScheduleStatus Status { get; set; } = RecurringScheduleStatus.Active;

    /// <summary>Post each new invoice; otherwise it is left as a draft for someone to check.</summary>
    public bool AutoPost { get; set; }

    /// <summary>
    /// The two-digit state the supply is made in. An invoice stores only whether
    /// it was inter-state, so the template cannot say; left null, the customer's
    /// GSTIN decides, which is right for a registered customer.
    /// </summary>
    [MaxLength(2, ErrorMessage = "Place of supply must be a 2-digit state code.")]
    public string? PlaceOfSupplyStateCode { get; set; }

    public DateTimeOffset? LastRunAt { get; set; }

    /// <summary>Why the last occurrence could not be raised; cleared by the next one that is.</summary>
    [MaxLength(500, ErrorMessage = "Error text cannot exceed 500 characters.")]
    public string? LastError { get; set; }
}

/// <summary>
/// One occurrence a schedule raised. Unique on schedule and date: the fixed
/// document key that makes a rerun, or a second worker, raise nothing twice.
/// </summary>
public class RecurringInvoiceRun : OrgScopedEntity
{
    public long RecurringInvoiceRunId { get; set; }

    public long RecurringInvoiceScheduleId { get; set; }

    public DateOnly OccurrenceDate { get; set; }

    public int OccurrenceIndex { get; set; }

    public long InvoiceId { get; set; }

    public bool Posted { get; set; }

    /// <summary>Why posting was refused. The invoice stays a draft.</summary>
    [MaxLength(500, ErrorMessage = "Error text cannot exceed 500 characters.")]
    public string? PostError { get; set; }
}
