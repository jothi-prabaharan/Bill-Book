using System.ComponentModel.DataAnnotations;
using Master.Entity.Enums;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

/// <summary>
/// One currency pair's rate on one date, from one source (TK-24): one unit of
/// <see cref="FromCurrencyCode"/> is worth <see cref="Rate"/> of
/// <see cref="ToCurrencyCode"/>. RBI publishes against INR, so a scraped row is
/// always <c>USD → INR</c> and never the reverse.
///
/// <b>Global, in the master database</b>: a rate is a fact about the market, not
/// about a customer, so there is no <c>CustomerId</c>, no <c>OrgId</c> and no
/// query filter. For the same reason only a platform operator may write one.
///
/// <b>History, not today's figure.</b> A document stores the rate it used as a
/// snapshot (CLAUDE.md, Multi-currency) and never looks it up again, so nothing
/// here is ever overwritten by a later date.
/// </summary>
public class ExchangeRate : AuditableEntity
{
    public long ExchangeRateId { get; set; }

    [Required(ErrorMessage = "From currency is required.")]
    [MaxLength(3, ErrorMessage = "From currency must be a 3-letter ISO 4217 code.")]
    public string FromCurrencyCode { get; set; } = null!;

    [Required(ErrorMessage = "To currency is required.")]
    [MaxLength(3, ErrorMessage = "To currency must be a 3-letter ISO 4217 code.")]
    public string ToCurrencyCode { get; set; } = null!;

    public DateOnly RateDate { get; set; }

    [Range(typeof(decimal), "0.00000001", "9999999999", ErrorMessage = "Rate must be greater than zero.")]
    public decimal Rate { get; set; }

    public RateSource Source { get; set; }
}
