using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

/// <summary>
/// One sort key. Their order in the request is their priority.
///
/// A report with a running balance <b>forces</b> its own ordering and refuses
/// these, because a running balance is defined over an ordered set — reordering
/// the rows would leave the balance column arithmetically true of an order the
/// reader cannot see.
/// </summary>
public class ReportSortModel
{
    [Required(ErrorMessage = "A sort must name a column.")]
    [MaxLength(60, ErrorMessage = "Column key cannot exceed 60 characters.")]
    public string Column { get; set; } = null!;

    public SortDirection Direction { get; set; } = SortDirection.Asc;
}
