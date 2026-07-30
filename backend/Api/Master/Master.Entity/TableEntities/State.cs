using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

public class State : AuditableEntity
{
    /// <summary>PK, not identity.</summary>
    public int StateId { get; set; }

    public int CountryId { get; set; }

    /// <summary>For India this is the 2-digit GST state code.</summary>
    [Required(ErrorMessage = "State code is required.")]
    [MaxLength(5, ErrorMessage = "State code cannot exceed 5 characters.")]
    public string StateCode { get; set; } = null!;

    [Required(ErrorMessage = "State name is required.")]
    [MaxLength(100, ErrorMessage = "State name cannot exceed 100 characters.")]
    public string StateName { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public Country? Country { get; set; }
}
