using System.ComponentModel.DataAnnotations;
using Accounting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// A job the branch does for a client, or for itself (TK-104, design "Project
/// accounting"). <b>A project is a ledger dimension</b>: the ledger rows carry
/// its id, so what a job earned and cost is a query over the same rows every
/// other report reads. A completed or cancelled project accepts no postings.
/// </summary>
public class Project : OrgScopedEntity
{
    public long ProjectId { get; set; }

    /// <summary>From the <c>PRJ</c> numbering series, unique per branch.</summary>
    [Required(ErrorMessage = "Project code is required.")]
    [MaxLength(20, ErrorMessage = "Project code cannot exceed 20 characters.")]
    public string ProjectCode { get; set; } = null!;

    [Required(ErrorMessage = "Project name is required.")]
    [MaxLength(150, ErrorMessage = "Project name cannot exceed 150 characters.")]
    public string ProjectName { get; set; } = null!;

    [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; set; }

    /// <summary>The client. Null for an internal project. No FK: contacts live in Master.</summary>
    public long? ContactId { get; set; }

    public ProjectBillingMethod BillingMethod { get; set; } = ProjectBillingMethod.TimeAndMaterials;

    /// <summary>Where a time-and-materials project's rate comes from. Null otherwise.</summary>
    public ProjectRateBasis? RateBasis { get; set; }

    /// <summary>The rate when <see cref="RateBasis"/> is <see cref="ProjectRateBasis.ProjectRate"/>.</summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "The hourly rate cannot be negative.")]
    public decimal? HourlyRate { get; set; }

    /// <summary>The whole fee, for a fixed-fee project.</summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "The fixed fee cannot be negative.")]
    public decimal? FixedFee { get; set; }

    /// <summary>The cost budget, in base currency. The hours budget is on tasks.</summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "The budget cannot be negative.")]
    public decimal? BudgetAmount { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Active;

    /// <summary>The client's billing currency.</summary>
    [Required(ErrorMessage = "Currency code is required.")]
    [MaxLength(3, ErrorMessage = "Currency code must be a 3-letter code.")]
    public string CurrencyCode { get; set; } = null!;
}
