using System.ComponentModel.DataAnnotations;
using Master.Entity.Enums;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

/// <summary>
/// One metal and purity's price per gram, in INR, on one date, from one source
/// (TK-24). <see cref="PurityCode"/> is the purity as the trade writes it — 24K,
/// 22K, 999, 925 — matching Inventory's <c>MetalPurity</c> codes, which cannot be
/// a foreign key because they are another service's per-branch rows.
///
/// Global and history-only, like <see cref="ExchangeRate"/>.
/// </summary>
public class MetalRate : AuditableEntity
{
    public long MetalRateId { get; set; }

    public Metal Metal { get; set; }

    [Required(ErrorMessage = "Purity is required.")]
    [MaxLength(10, ErrorMessage = "Purity cannot exceed 10 characters.")]
    public string PurityCode { get; set; } = null!;

    public DateOnly RateDate { get; set; }

    [Range(typeof(decimal), "0.0001", "99999999999999", ErrorMessage = "Rate per gram must be greater than zero.")]
    public decimal RatePerGram { get; set; }

    public RateSource Source { get; set; }
}
