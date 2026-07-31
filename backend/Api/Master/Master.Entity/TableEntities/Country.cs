using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

public class Country : AuditableEntity
{
    /// <summary>PK, not identity — explicit ids for seeding.</summary>
    public int CountryId { get; set; }

    [Required(ErrorMessage = "Country code is required.")]
    [MaxLength(2, ErrorMessage = "Country code must be a 2-letter ISO code.")]
    public string CountryCode { get; set; } = null!;

    [Required(ErrorMessage = "Country name is required.")]
    [MaxLength(100, ErrorMessage = "Country name cannot exceed 100 characters.")]
    public string CountryName { get; set; } = null!;

    [Required(ErrorMessage = "Currency code is required.")]
    [MaxLength(3, ErrorMessage = "Currency code must be a 3-letter ISO code.")]
    public string CurrencyCode { get; set; } = null!;

    [MaxLength(5, ErrorMessage = "Phone code cannot exceed 5 characters.")]
    public string? PhoneCode { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<State> States { get; set; } = new List<State>();
}
