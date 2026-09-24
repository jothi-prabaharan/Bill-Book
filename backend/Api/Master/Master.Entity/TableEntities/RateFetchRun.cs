using System.ComponentModel.DataAnnotations;
using Master.Entity.Enums;
using Shared.Kernel.Entities;
using Shared.Kernel.Errors;

namespace Master.Entity.TableEntities;

/// <summary>
/// One attempt to fetch a rate source for one day (TK-26): the RBI reference
/// rates now, IBJA's metal rates next (TK-25).
///
/// <b>The day's ledger and the day's task list.</b> A succeeded run for a source
/// and day is what makes a second run that day a no-op. A failed run is recorded
/// with <see cref="FollowUpStatus"/> Open and writes no rate, and the open rows
/// are the list somebody works through — the job <c>{schema}.ErrorLogs</c> does
/// for the tenant schemas. It cannot go there: an error log row is scoped to a
/// customer and a branch, and a rate fetch belongs to neither.
/// </summary>
public class RateFetchRun : AuditableEntity
{
    public long RateFetchRunId { get; set; }

    public RateSource Source { get; set; }

    /// <summary>The day, in India, that the run was for.</summary>
    public DateOnly RunDate { get; set; }

    /// <summary>The date the source said its rates were for; null when nothing was read.</summary>
    public DateOnly? RateDate { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset FinishedAt { get; set; }

    public RateFetchStatus Status { get; set; }

    /// <summary>New rows written. Zero on a rerun of rates already on file.</summary>
    public int RatesWritten { get; set; }

    [MaxLength(2000, ErrorMessage = "Error cannot exceed 2000 characters.")]
    public string? Error { get; set; }

    /// <summary>Open on every failure until a person closes it; null on success.</summary>
    public ErrorFollowUpStatus? FollowUpStatus { get; set; }
}
