using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// Business Performance (TK-203): its own request and answer, benchmarked on
// Xero's Business Performance report (the D-15 ratios, grouped as profitability,
// efficiency, financial position and cash, beside a comparison period), Odoo's
// executive summary (each ratio with the change from the period before) and
// AutoCount's ratio analysis (the figures each ratio divides). Money is in the
// branch's base currency.

/// <summary>
/// What the report is asked for. The from and to dates are required (D-67);
/// a comparison period, when given, needs both of its dates. The branch is
/// always the token's: a request naming another branch is refused with 403.
/// </summary>
public sealed class BusinessPerformanceReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>The first day of a period to show beside it, such as the period before or the same period a year earlier.</summary>
    public DateOnly? CompareFromDate { get; set; }

    public DateOnly? CompareToDate { get; set; }

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>One ratio for the period, and for the comparison period when asked.</summary>
public sealed class BusinessPerformanceRatioDto
{
    public BusinessPerformanceMetric Metric { get; set; }

    public BusinessPerformanceGroup Group { get; set; }

    [MaxLength(100, ErrorMessage = "Title cannot exceed 100 characters.")]
    public string Title { get; set; } = string.Empty;

    public BusinessPerformanceUnit Unit { get; set; }

    /// <summary>How the ratio is worked out, in words.</summary>
    [MaxLength(200, ErrorMessage = "Calculation cannot exceed 200 characters.")]
    public string Calculation { get; set; } = string.Empty;

    /// <summary>Null when there is nothing to divide by, or no books to read.</summary>
    public decimal? Value { get; set; }

    /// <summary>The amount divided; for the cash balance, the balance itself.</summary>
    public decimal? Numerator { get; set; }

    /// <summary>The amount divided by; null for the cash balance, which is not a ratio.</summary>
    public decimal? Denominator { get; set; }

    public decimal? CompareValue { get; set; }

    public decimal? CompareNumerator { get; set; }

    public decimal? CompareDenominator { get; set; }

    /// <summary>The value less the comparison's, in the ratio's own unit; null unless both have a value.</summary>
    public decimal? Change { get; set; }
}

/// <summary>The ratios in their defined order, and the periods they are for.</summary>
public sealed class BusinessPerformanceReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    /// <summary>Days in the period, both ends counted: what the payment-day ratios and the yearly return scale by.</summary>
    public int Days { get; set; }

    public DateOnly? CompareFromDate { get; set; }

    public DateOnly? CompareToDate { get; set; }

    public int? CompareDays { get; set; }

    public List<BusinessPerformanceRatioDto> Ratios { get; set; } = [];
}
