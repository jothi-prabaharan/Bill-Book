using System.ComponentModel.DataAnnotations;
using Master.Entity.Enums;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

/// <summary>The single source for currency code, symbol and display formatting.</summary>
public class Currency : AuditableEntity
{
    /// <summary>PK, not identity — explicit ids for seeding.</summary>
    public int CurrencyId { get; set; }

    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(3, ErrorMessage = "Code must be a 3-letter ISO 4217 code.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(60, ErrorMessage = "Name cannot exceed 60 characters.")]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Symbol is required.")]
    [MaxLength(10, ErrorMessage = "Symbol cannot exceed 10 characters.")]
    public string Symbol { get; set; } = null!;

    /// <summary>Display grouping mask, e.g. ###,###,##0.00 (Western) or ##,##,##0.00 (Indian).</summary>
    [Required(ErrorMessage = "Format is required.")]
    [MaxLength(30, ErrorMessage = "Format cannot exceed 30 characters.")]
    public string Format { get; set; } = null!;

    /// <summary>Drives money rounding, not just display.</summary>
    public int DecimalPlaces { get; set; } = 2;

    public SymbolPosition SymbolPosition { get; set; } = SymbolPosition.Prefix;

    public bool IsActive { get; set; } = true;
}
