using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>A user who works on a project, with what their hour bills at and costs (TK-104).</summary>
public class ProjectMember : OrgScopedEntity
{
    public long ProjectMemberId { get; set; }

    public long ProjectId { get; set; }

    /// <summary>A plain Guid: users live in the master database.</summary>
    public Guid UserId { get; set; }

    /// <summary>The rate their hour bills at, when the project bills by user.</summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "The hourly rate cannot be negative.")]
    public decimal? HourlyRate { get; set; }

    /// <summary>What an hour of theirs costs the business, for job profit.</summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "The cost rate cannot be negative.")]
    public decimal? CostRate { get; set; }
}
