using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Numbering;

/// <summary>
/// A generated-code series: contact codes, item codes, and every document
/// number. One table rather than a generator per master, because they all need
/// the same four things — a prefix, zero padding, a financial-year segment and
/// a per-branch variant — and only one of them can be got subtly wrong.
///
/// Lives in Shared.Kernel rather than a service's Entity project so each
/// service can map it into its own DbContext and allocate inside its own
/// transaction. Accounting owns the migration; every other service maps it with
/// ExcludeFromMigrations. An HTTP hop to fetch a number cannot join the
/// caller's transaction, and a rolled-back document would leave a consumed
/// number behind — which for a GST invoice series is a gap an auditor asks about.
/// </summary>
public class NumberingSeries : OrgScopedEntity
{
    public long NumberingSeriesId { get; set; }

    /// <summary>Seeded rows only: the immutable canonical name. Null for user-created series.</summary>
    [MaxLength(30, ErrorMessage = "Series system name cannot exceed 30 characters.")]
    public string? SeriesSystemName { get; set; }

    /// <summary>
    /// The lookup key callers pass: CUSTOMER, VENDOR, ITEM, WAREHOUSE, BANK, or
    /// for documents the three-letter transaction type code such as INV or CRN.
    /// </summary>
    [Required(ErrorMessage = "Series code is required.")]
    [MaxLength(30, ErrorMessage = "Series code cannot exceed 30 characters.")]
    public string SeriesCode { get; set; } = null!;

    /// <summary>Display name. Editable even on seeded series.</summary>
    [Required(ErrorMessage = "Series name is required.")]
    [MaxLength(50, ErrorMessage = "Series name cannot exceed 50 characters.")]
    public string SeriesName { get; set; } = null!;

    public SeriesFor SeriesFor { get; set; } = SeriesFor.Master;

    [MaxLength(15, ErrorMessage = "Prefix cannot exceed 15 characters.")]
    public string? Prefix { get; set; }

    [MaxLength(15, ErrorMessage = "Suffix cannot exceed 15 characters.")]
    public string? Suffix { get; set; }

    /// <summary>
    /// Placed between segments, not after the prefix specifically. Empty runs
    /// the segments together — INV2526000042 rather than INV/2526/000042.
    /// </summary>
    [MaxLength(1, ErrorMessage = "Separator cannot exceed 1 character.")]
    public string? Separator { get; set; } = "-";

    public bool IncludeFinancialYear { get; set; }

    public FinancialYearFormat FinancialYearFormat { get; set; } = FinancialYearFormat.Compact;

    public bool IncludeBranchCode { get; set; }

    /// <summary>
    /// The branch's own code — <c>Organization.OrgCode</c> — copied onto the
    /// series rather than read from it, so generating a number never crosses a
    /// database boundary. Organizations live in the master database; the number
    /// is composed in the customer database.
    ///
    /// Copying also means renaming a branch later cannot restyle numbers already
    /// issued under the old code.
    /// </summary>
    [MaxLength(10, ErrorMessage = "Branch code cannot exceed 10 characters.")]
    public string? BranchCode { get; set; }

    /// <summary>Zero-padding width. 5 gives 00001.</summary>
    [Range(1, 12, ErrorMessage = "Number length must be between 1 and 12.")]
    public int NumberLength { get; set; } = 5;

    [Range(0, long.MaxValue, ErrorMessage = "Start number cannot be negative.")]
    public long StartNumber { get; set; } = 1;

    /// <summary>The counter. Incremented by a compare-and-swap, never read-then-write.</summary>
    public long NextNumber { get; set; } = 1;

    public NumberResetFrequency ResetFrequency { get; set; } = NumberResetFrequency.Never;

    /// <summary>When the counter last restarted. Null until the first reset.</summary>
    public DateOnly? LastResetOn { get; set; }

    /// <summary>
    /// Lets a user type their own code instead of consuming the series. Refused
    /// on document series — a hand-typed invoice number breaks consecutiveness.
    /// </summary>
    public bool AllowManualOverride { get; set; }

    /// <summary>Which series a form preselects when several share a code and branch.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Seeded series: renamable, never deletable.</summary>
    public bool IsSystem { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }
}
