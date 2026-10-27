using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Purchase Analysis (TK-239): what was bought in a period, added up by item, item category or
// vendor: quantity, gross value, discount, net purchases, the average cost of a unit and each
// group's share of the whole. Benchmarked on Xero's Purchases by Item and by Supplier, AutoCount's
// Purchase Analysis and Odoo's Purchase Analysis. Everything is in the branch's base currency; net
// purchases are before tax. Posted bills, less posted debit notes; fixed assets bought on a bill are
// left out unless asked for, since they are not stock.

/// <summary>
/// What the report is asked for: the bill-date period, both dates required (D-67), what to add
/// purchases up by, optionally one vendor and one item category, whether debit notes reduce the
/// figures (they do, unless turned off), whether fixed assets bought on a bill are included (they
/// are not, unless asked for), and optionally a second period to compare net purchases with. The
/// branch is always the token's: a request naming another branch is refused with 403.
/// </summary>
public sealed class PurchaseAnalysisReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>What to add purchases up by; items when blank.</summary>
    public PurchaseAnalysisGroupBy GroupBy { get; set; } = PurchaseAnalysisGroupBy.Item;

    /// <summary>Only this vendor; every vendor when blank.</summary>
    public long? ContactId { get; set; }

    /// <summary>Only items in this category; every category when blank.</summary>
    public long? ItemCategoryId { get; set; }

    /// <summary>Whether posted debit notes reduce the figures. On unless asked otherwise: a return is not a purchase.</summary>
    public bool IncludeDebitNotes { get; set; } = true;

    /// <summary>Whether lines bought as fixed assets are counted. Off unless asked for: an asset is not stock.</summary>
    public bool IncludeFixedAssets { get; set; }

    /// <summary>The start of a second period to compare net purchases with; with <see cref="CompareToDate"/>, both or neither.</summary>
    public DateOnly? CompareFromDate { get; set; }

    /// <summary>The end of the second period.</summary>
    public DateOnly? CompareToDate { get; set; }

    [Range(1, 100000, ErrorMessage = "Page number must be 1 or more.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 50;

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>One item, category or vendor with what was bought from it, in base currency.</summary>
public sealed class PurchaseAnalysisGroupDto
{
    /// <summary>The item, category or vendor id; nought for the row of lines with none.</summary>
    public long Key { get; set; }

    [MaxLength(50, ErrorMessage = "Code cannot exceed 50 characters.")]
    public string Code { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Units bought less units returned, in each item's base unit.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Quantity times price, as keyed: a price that includes tax shows it here.</summary>
    public decimal Gross { get; set; }

    public decimal Discount { get; set; }

    /// <summary>What was bought for before tax, after discount.</summary>
    public decimal Net { get; set; }

    /// <summary>Net purchases per unit; given only when adding up by item, where a unit means one thing, for an item (not for lines with none), and null with no units.</summary>
    public decimal? AverageCost { get; set; }

    /// <summary>This group's share of all net purchases, as a percentage; null when the total is nought.</summary>
    public decimal? SharePercent { get; set; }

    /// <summary>Net purchases in the comparison period; null when none was asked for.</summary>
    public decimal? CompareNet { get; set; }

    /// <summary>How much net purchases grew or fell against the comparison period, as a percentage of it; null when none was asked for or it had none.</summary>
    public decimal? ChangePercent { get; set; }
}

/// <summary>Everything the filters match added up, not only the groups on this page.</summary>
public sealed class PurchaseAnalysisTotalsDto
{
    public decimal Quantity { get; set; }

    public decimal Gross { get; set; }

    public decimal Discount { get; set; }

    public decimal Net { get; set; }

    public decimal? CompareNet { get; set; }

    public decimal? ChangePercent { get; set; }
}

public sealed class PurchaseAnalysisReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public DateOnly? CompareFromDate { get; set; }

    public DateOnly? CompareToDate { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    public PurchaseAnalysisGroupBy GroupBy { get; set; }

    public bool IncludesDebitNotes { get; set; }

    public bool IncludesFixedAssets { get; set; }

    /// <summary>This page of groups, largest net purchases first.</summary>
    public List<PurchaseAnalysisGroupDto> Groups { get; set; } = [];

    /// <summary>How many groups the filters match, all pages.</summary>
    public int TotalGroups { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public PurchaseAnalysisTotalsDto Totals { get; set; } = new();
}
