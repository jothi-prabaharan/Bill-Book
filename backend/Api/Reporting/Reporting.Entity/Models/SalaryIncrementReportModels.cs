using System.ComponentModel.DataAnnotations;

namespace Reporting.Entity.Models;

// The Salary Increment Report (TK-198): its own request and answer, benchmarked
// on the increment reports of Xero (pay rate history), AutoCount (salary
// increment listing) and Odoo (contract wage changes) — who, from when, the
// salary before and after, the increase in money and per cent, and why.
//
// A salary here is the annual CTC Payroll stores on each revision
// (pay.SalaryRevisions.PreviousCtc and NewCtc); there is no separate basic or
// gross figure on a revision to report.

/// <summary>
/// What the report is asked for. The effective-date range is required; the
/// employee and department narrow it. The branch is always the token's: a
/// request naming another branch is refused with 403.
/// </summary>
public sealed class SalaryIncrementReportRequest
{
    /// <summary>The first effective date included.</summary>
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    /// <summary>The last effective date included; not before <see cref="FromDate"/>.</summary>
    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>One employee's revisions only.</summary>
    [Range(1, long.MaxValue, ErrorMessage = "Choose an employee from the list.")]
    public long? EmployeeId { get; set; }

    /// <summary>Revisions of employees in this department only.</summary>
    [Range(1, long.MaxValue, ErrorMessage = "Choose a department from the list.")]
    public long? DepartmentId { get; set; }

    /// <summary>
    /// The branch, when the caller names one. It must be the branch in the
    /// token; the query filter scopes the rows to that branch either way.
    /// </summary>
    public Guid? OrgId { get; set; }
}

/// <summary>One revision: a row of the report.</summary>
public sealed class SalaryIncrementReportRowDto
{
    public long SalaryRevisionId { get; set; }

    public long EmployeeId { get; set; }

    [MaxLength(30, ErrorMessage = "Employee code cannot exceed 30 characters.")]
    public string EmployeeCode { get; set; } = string.Empty;

    [MaxLength(201, ErrorMessage = "Employee name cannot exceed 201 characters.")]
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>The employee's department today; null when none is set.</summary>
    [MaxLength(100, ErrorMessage = "Department cannot exceed 100 characters.")]
    public string? Department { get; set; }

    /// <summary>The employee's designation today; null when none is set.</summary>
    [MaxLength(100, ErrorMessage = "Designation cannot exceed 100 characters.")]
    public string? Designation { get; set; }

    /// <summary>The date the revised salary takes effect.</summary>
    public DateOnly EffectiveDate { get; set; }

    /// <summary>Annual CTC before the revision.</summary>
    public decimal PreviousSalary { get; set; }

    /// <summary>Revised minus previous. Negative for a reduction.</summary>
    public decimal IncrementAmount { get; set; }

    /// <summary>The increment as a percentage of the previous salary, to two places; null when the previous salary was zero.</summary>
    public decimal? IncrementPercent { get; set; }

    /// <summary>Annual CTC from the effective date.</summary>
    public decimal RevisedSalary { get; set; }

    /// <summary>
    /// Who recorded the revision. Salary revisions are not approved in the
    /// product, so there is no approver to show; null when the name cannot be found.
    /// </summary>
    [MaxLength(200, ErrorMessage = "Recorded by cannot exceed 200 characters.")]
    public string? RecordedBy { get; set; }

    /// <summary>The reason given with the revision.</summary>
    [MaxLength(200, ErrorMessage = "Remarks cannot exceed 200 characters.")]
    public string? Remarks { get; set; }
}

/// <summary>The footer: what the rows add up to.</summary>
public sealed class SalaryIncrementReportTotalsDto
{
    public int RevisionCount { get; set; }

    public int EmployeeCount { get; set; }

    public decimal TotalPreviousSalary { get; set; }

    public decimal TotalIncrementAmount { get; set; }

    public decimal TotalRevisedSalary { get; set; }

    /// <summary>
    /// The mean of each revision's increment percentage, to two places,
    /// leaving out revisions from a zero salary; null when there are none.
    /// </summary>
    public decimal? AverageIncrementPercent { get; set; }
}

/// <summary>The report: its rows in effective-date order and their totals.</summary>
public sealed class SalaryIncrementReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public List<SalaryIncrementReportRowDto> Rows { get; set; } = [];

    public SalaryIncrementReportTotalsDto Totals { get; set; } = new();
}

/// <summary>The choices for the employee and department filters.</summary>
public sealed class SalaryIncrementReportOptionsDto
{
    public List<SalaryIncrementDepartmentOptionDto> Departments { get; set; } = [];

    public List<SalaryIncrementEmployeeOptionDto> Employees { get; set; } = [];
}

public sealed class SalaryIncrementDepartmentOptionDto
{
    public long DepartmentId { get; set; }

    [MaxLength(100, ErrorMessage = "Department cannot exceed 100 characters.")]
    public string Name { get; set; } = string.Empty;
}

public sealed class SalaryIncrementEmployeeOptionDto
{
    public long EmployeeId { get; set; }

    [MaxLength(30, ErrorMessage = "Employee code cannot exceed 30 characters.")]
    public string EmployeeCode { get; set; } = string.Empty;

    [MaxLength(201, ErrorMessage = "Employee name cannot exceed 201 characters.")]
    public string Name { get; set; } = string.Empty;

    public long DepartmentId { get; set; }
}
