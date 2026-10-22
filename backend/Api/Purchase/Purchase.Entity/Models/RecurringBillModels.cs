using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Recurring;

namespace Purchase.Entity.Models;

/// <summary>A recurring bill schedule as the screen sends it (TK-139).</summary>
public class SaveRecurringBillScheduleRequest
{
    [Required(ErrorMessage = "Name the schedule.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [Range(1, long.MaxValue, ErrorMessage = "Choose the bill to repeat.")]
    public long TemplateBillId { get; set; }

    [EnumDataType(typeof(RecurrenceFrequency), ErrorMessage = "Choose how often it repeats.")]
    public RecurrenceFrequency Frequency { get; set; } = RecurrenceFrequency.Monthly;

    [Range(1, 12, ErrorMessage = "Repeat every 1 to 12 periods.")]
    public int Every { get; set; } = 1;

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [Range(1, 1000, ErrorMessage = "Stop after 1 to 1000 occurrences.")]
    public int? MaxOccurrences { get; set; }

    public bool AutoPost { get; set; }

    [MaxLength(30, ErrorMessage = "The prefix cannot exceed 30 characters.")]
    public string? VendorBillNoPrefix { get; set; }

    [MaxLength(2, ErrorMessage = "Place of supply must be a 2-digit state code.")]
    public string? PlaceOfSupplyStateCode { get; set; }
}

public class RecurringBillScheduleView
{
    public long RecurringBillScheduleId { get; set; }

    public string Name { get; set; } = null!;

    public long TemplateBillId { get; set; }

    public string? TemplateDocumentNo { get; set; }

    public long ContactId { get; set; }

    public decimal TemplateTotal { get; set; }

    public string Frequency { get; set; } = null!;

    public int Every { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public int? MaxOccurrences { get; set; }

    public DateOnly? NextRunDate { get; set; }

    public int OccurrencesCreated { get; set; }

    public string Status { get; set; } = null!;

    public bool AutoPost { get; set; }

    public string VendorBillNoPrefix { get; set; } = null!;

    public string? PlaceOfSupplyStateCode { get; set; }

    public DateTimeOffset? LastRunAt { get; set; }

    public string? LastError { get; set; }

    public List<RecurringBillRunView> Runs { get; set; } = [];
}

public class RecurringBillRunView
{
    public DateOnly OccurrenceDate { get; set; }

    public long BillId { get; set; }

    public string? DocumentNo { get; set; }

    public bool Posted { get; set; }

    public string? PostError { get; set; }
}

/// <summary>The worker's call: raise everything due in one branch (TK-139).</summary>
public class RunRecurringRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    /// <summary>Defaults to today.</summary>
    public DateOnly? AsOf { get; set; }
}

public class RunRecurringResponse
{
    public int Created { get; set; }

    public int Posted { get; set; }

    public int Failed { get; set; }

    /// <summary>Occurrences not raised because the plan's monthly limit on Purchase documents was reached (TK-210).</summary>
    public int Skipped { get; set; }
}

public enum RecurringScheduleOutcome
{
    Ok = 0,
    NotFound = 1,
    Invalid = 2,
    Conflict = 3,
}

public sealed record RecurringScheduleResult(
    RecurringScheduleOutcome Outcome, long ScheduleId = 0, string? Detail = null);
