using System.ComponentModel.DataAnnotations;

namespace Master.Entity.Models;

/// <summary>One exchange rate, as the lookup and the history return it (TK-24).</summary>
public sealed record ExchangeRateDto(
    long ExchangeRateId,
    string FromCurrencyCode,
    string ToCurrencyCode,
    DateOnly RateDate,
    decimal Rate,
    string Source);

/// <summary>One metal rate, as the lookup and the history return it (TK-24).</summary>
public sealed record MetalRateDto(
    long MetalRateId,
    string Metal,
    string PurityCode,
    DateOnly RateDate,
    decimal RatePerGram,
    string Source);

/// <summary>A hand-entered exchange rate. Entering the same pair and date again corrects it.</summary>
public class SaveExchangeRateRequest
{
    [Required(ErrorMessage = "From currency is required.")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "From currency must be a 3-letter ISO 4217 code.")]
    public string FromCurrencyCode { get; set; } = null!;

    [Required(ErrorMessage = "To currency is required.")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "To currency must be a 3-letter ISO 4217 code.")]
    public string ToCurrencyCode { get; set; } = null!;

    [Required(ErrorMessage = "Rate date is required.")]
    public DateOnly? RateDate { get; set; }

    [Range(typeof(decimal), "0.00000001", "9999999999", ErrorMessage = "Rate must be greater than zero.")]
    public decimal Rate { get; set; }
}

/// <summary>A hand-entered metal rate per gram, in INR. The same metal, purity and date again corrects it.</summary>
public class SaveMetalRateRequest
{
    [Required(ErrorMessage = "Metal is required.")]
    [MaxLength(10, ErrorMessage = "Metal cannot exceed 10 characters.")]
    public string Metal { get; set; } = null!;

    [Required(ErrorMessage = "Purity is required.")]
    [MaxLength(10, ErrorMessage = "Purity cannot exceed 10 characters.")]
    public string PurityCode { get; set; } = null!;

    [Required(ErrorMessage = "Rate date is required.")]
    public DateOnly? RateDate { get; set; }

    [Range(typeof(decimal), "0.0001", "99999999999999", ErrorMessage = "Rate per gram must be greater than zero.")]
    public decimal RatePerGram { get; set; }
}

/// <summary>What saving or removing a rate came to.</summary>
public enum RateSaveOutcome
{
    Ok = 0,
    UnknownCurrency = 1,
    SameCurrency = 2,
    UnknownMetal = 3,
    NotFound = 4,
    NotManual = 5,
}
