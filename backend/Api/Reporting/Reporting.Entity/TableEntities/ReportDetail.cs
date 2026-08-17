using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Reporting.Entity.TableEntities;

/// <summary>
/// One row per column per report — how a column is <i>presented</i>.
///
/// The division of labour with <c>IReportSource</c> matters and is easy to get
/// backwards: <b>the source is the authority on what a column means</b> (which
/// property it reads, how it is computed, whether it can be filtered at all in
/// principle), and <b>this row is the authority on how it appears</b> (its
/// header, its width, whether it is on by default). A startup check compares the
/// two sets of column keys and fails fast when they diverge, because a seeded
/// column with no source silently never renders and a source column with no seed
/// row silently cannot be chosen.
/// </summary>
public class ReportDetail : OrgScopedEntity
{
    public long ReportDetailId { get; set; }

    public long ReportId { get; set; }

    /// <summary>Matches a <c>ReportColumn.Key</c> on the report's source.</summary>
    [Required(ErrorMessage = "Column key is required.")]
    [MaxLength(60, ErrorMessage = "Column key cannot exceed 60 characters.")]
    public string ColumnKey { get; set; } = null!;

    /// <summary>
    /// The heading. May contain <c>%CurCode%</c>, which is replaced with the
    /// branch's base currency code when the report runs — so one seeded row
    /// reads "Debit(INR)" in one branch and "Debit(AED)" in another.
    /// </summary>
    [Required(ErrorMessage = "Column header is required.")]
    [MaxLength(120, ErrorMessage = "Column header cannot exceed 120 characters.")]
    public string Header { get; set; } = null!;

    public ColumnDataType DataType { get; set; }

    /// <summary>Whether the column is in the initial selection, before anyone chooses.</summary>
    public bool IsDefault { get; set; }

    public bool IsFilterable { get; set; } = true;

    public bool IsSortable { get; set; } = true;

    public bool IsGroupable { get; set; }

    public bool IsPivotable { get; set; }

    /// <summary>
    /// What a group footer and the grand total compute for this column.
    /// <see cref="AggregateFunction.None"/> for anything whose total is
    /// meaningless — a rate, a code, a date.
    /// </summary>
    public AggregateFunction DefaultAggregate { get; set; } = AggregateFunction.None;

    public ColumnAlignment Alignment { get; set; } = ColumnAlignment.Left;

    /// <summary>Preferred width in pixels. Null lets the grid decide.</summary>
    public int? Width { get; set; }

    public int SortOrder { get; set; }

    /// <summary>
    /// Shows in the card title at ~360px, where a row becomes a card and only two
    /// or three columns can lead. Typically the document number and the date.
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// Fetched but never offered in the chooser — an internal key a row links by,
    /// such as Trial Balance's account id.
    /// </summary>
    public bool IsHidden { get; set; }
}
