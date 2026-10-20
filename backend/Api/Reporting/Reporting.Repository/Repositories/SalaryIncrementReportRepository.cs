using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Salary Increment Report (TK-198): every salary revision whose effective
/// date falls in a range, with the salary before and after, the increase in
/// money and per cent, and who recorded it and why.
///
/// It has its own endpoint and page rather than an <see cref="IReportSourceRepository"/>,
/// by the owner's request, so it is listed in the catalog through
/// <see cref="DedicatedReportPages"/> instead of through a source.
///
/// The rows come from <c>pay.SalaryRevisions</c> joined to the employee,
/// department and designation in <c>hrm</c>, all read models in the tenant
/// database, so the query filter scopes every table to the token's branch.
/// The department and designation are the employee's today: a revision does
/// not record what they were when it was made.
/// </summary>
public sealed class SalaryIncrementReportRepository
{
    /// <summary>The catalog key, and the page's path under <c>/reports</c>.</summary>
    public const string ReportKey = "salary-increment";

    private readonly ReportingDbContext _db;
    private readonly BatchedNameResolverRepository _names;

    public SalaryIncrementReportRepository(ReportingDbContext db, BatchedNameResolverRepository names)
    {
        _db = db;
        _names = names;
    }

    /// <summary>Why the request's dates cannot be run, or null. Pure.</summary>
    public static string? RangeRefusal(SalaryIncrementReportRequest request) =>
        request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from
            ? "To date cannot be before the from date."
            : null;

    /// <summary>The report for a request the controller has already validated.</summary>
    public async Task<SalaryIncrementReportResultDto> RunAsync(SalaryIncrementReportRequest request, CancellationToken ct)
    {
        DateOnly from = request.FromDate!.Value;
        DateOnly to = request.ToDate!.Value;

        List<SalaryIncrementQueryRow> found = await Query(
                _db.SalaryRevisions, _db.Employees, _db.Departments, _db.Designations,
                from, to, request.EmployeeId, request.DepartmentId)
            .ToListAsync(ct);

        // Users live in the master database: one batched call for every name.
        Dictionary<Guid, string> names = await _names.GetUserNamesAsync(found.Select(r => r.RecordedById), ct);

        List<SalaryIncrementReportRowDto> rows = [.. found.Select(r => ToRow(r, names))];

        return new SalaryIncrementReportResultDto
        {
            FromDate = from,
            ToDate = to,
            Rows = rows,
            Totals = Summarise(rows),
        };
    }

    /// <summary>The employees and departments the filters offer, by code and by name.</summary>
    public async Task<SalaryIncrementReportOptionsDto> OptionsAsync(CancellationToken ct) =>
        new()
        {
            Departments = await _db.Departments
                .OrderBy(d => d.Name)
                .Select(d => new SalaryIncrementDepartmentOptionDto { DepartmentId = d.DepartmentId, Name = d.Name })
                .ToListAsync(ct),
            Employees = await _db.Employees
                .OrderBy(e => e.EmployeeCode)
                .Select(e => new SalaryIncrementEmployeeOptionDto
                {
                    EmployeeId = e.EmployeeId,
                    EmployeeCode = e.EmployeeCode,
                    Name = e.FirstName + (e.LastName != null ? " " + e.LastName : ""),
                    DepartmentId = e.DepartmentId,
                })
                .ToListAsync(ct),
        };

    /// <summary>
    /// The revisions in range, oldest effective date first, then by employee
    /// code. Built from plain queryables so it runs against the read models
    /// here and against lists in a test. LINQ only.
    /// </summary>
    public static IQueryable<SalaryIncrementQueryRow> Query(
        IQueryable<SalaryRevisionRead> revisions,
        IQueryable<EmployeeRecordRead> employees,
        IQueryable<DepartmentRead> departments,
        IQueryable<DesignationRead> designations,
        DateOnly from,
        DateOnly to,
        long? employeeId,
        long? departmentId)
    {
        IQueryable<SalaryRevisionRead> inRange = revisions.Where(r => r.EffectiveFrom >= from && r.EffectiveFrom <= to);

        if (employeeId is long employee)
        {
            inRange = inRange.Where(r => r.EmployeeId == employee);
        }

        var joined =
            from r in inRange
            join e in employees on r.EmployeeId equals e.EmployeeId
            join d in departments on e.DepartmentId equals d.DepartmentId into gd
            from d in gd.DefaultIfEmpty()
            join g in designations on e.DesignationId equals g.DesignationId into gg
            from g in gg.DefaultIfEmpty()
            select new { r, e, d, g };

        if (departmentId is long department)
        {
            joined = joined.Where(x => x.e.DepartmentId == department);
        }

        return joined
            .OrderBy(x => x.r.EffectiveFrom)
            .ThenBy(x => x.e.EmployeeCode)
            .ThenBy(x => x.r.SalaryRevisionId)
            .Select(x => new SalaryIncrementQueryRow
            {
                SalaryRevisionId = x.r.SalaryRevisionId,
                EmployeeId = x.e.EmployeeId,
                EmployeeCode = x.e.EmployeeCode,
                EmployeeName = x.e.FirstName + (x.e.LastName != null ? " " + x.e.LastName : ""),
                Department = x.d != null ? x.d.Name : null,
                Designation = x.g != null ? x.g.Name : null,
                EffectiveDate = x.r.EffectiveFrom,
                PreviousSalary = x.r.PreviousCtc,
                RevisedSalary = x.r.NewCtc,
                RecordedById = x.r.CreatedBy,
                Remarks = x.r.Reason,
            });
    }

    /// <summary>
    /// The increase as a percentage of the previous salary, rounded half away
    /// from zero to two places. Null from a zero salary, where a percentage
    /// means nothing. Pure.
    /// </summary>
    public static decimal? IncrementPercent(decimal previous, decimal revised) =>
        previous == 0m ? null : Math.Round((revised - previous) / previous * 100m, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The footer. The average percentage is the mean of each revision's own
    /// percentage, worked from the salaries rather than the rounded figures, so
    /// rounding is applied once. Pure.
    /// </summary>
    public static SalaryIncrementReportTotalsDto Summarise(IReadOnlyList<SalaryIncrementReportRowDto> rows)
    {
        List<decimal> ratios =
        [
            .. rows
                .Where(r => r.PreviousSalary != 0m)
                .Select(r => (r.RevisedSalary - r.PreviousSalary) / r.PreviousSalary * 100m),
        ];

        return new SalaryIncrementReportTotalsDto
        {
            RevisionCount = rows.Count,
            EmployeeCount = rows.Select(r => r.EmployeeId).Distinct().Count(),
            TotalPreviousSalary = rows.Sum(r => r.PreviousSalary),
            TotalIncrementAmount = rows.Sum(r => r.IncrementAmount),
            TotalRevisedSalary = rows.Sum(r => r.RevisedSalary),
            AverageIncrementPercent = ratios.Count == 0
                ? null
                : Math.Round(ratios.Average(), 2, MidpointRounding.AwayFromZero),
        };
    }

    private static SalaryIncrementReportRowDto ToRow(SalaryIncrementQueryRow r, IReadOnlyDictionary<Guid, string> names) =>
        new()
        {
            SalaryRevisionId = r.SalaryRevisionId,
            EmployeeId = r.EmployeeId,
            EmployeeCode = r.EmployeeCode,
            EmployeeName = r.EmployeeName,
            Department = r.Department,
            Designation = r.Designation,
            EffectiveDate = r.EffectiveDate,
            PreviousSalary = r.PreviousSalary,
            IncrementAmount = r.RevisedSalary - r.PreviousSalary,
            IncrementPercent = IncrementPercent(r.PreviousSalary, r.RevisedSalary),
            RevisedSalary = r.RevisedSalary,
            RecordedBy = r.RecordedById is Guid id && names.TryGetValue(id, out string? name) ? name : null,
            Remarks = r.Remarks,
        };
}

/// <summary>One revision as the query reads it, before the figures are worked out.</summary>
public sealed class SalaryIncrementQueryRow
{
    public long SalaryRevisionId { get; set; }
    public long EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public decimal PreviousSalary { get; set; }
    public decimal RevisedSalary { get; set; }
    public Guid? RecordedById { get; set; }
    public string? Remarks { get; set; }
}
