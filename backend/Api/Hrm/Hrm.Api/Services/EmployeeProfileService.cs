using Hrm.Entity.Enums;
using Hrm.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Employees;

namespace Hrm.Api.Services;

/// <summary>
/// The employee as other services see them (TK-49): leave and attendance read
/// the grade, location and reporting line to pick a policy and a chain. No
/// statutory numbers, no pay, no bank details ever leave by this route.
/// </summary>
public sealed class EmployeeProfileService
{
    private readonly HrmDbContext _db;

    public EmployeeProfileService(HrmDbContext db) => _db = db;

    public async Task<List<EmployeeProfile>> FindAsync(EmployeeLookupRequest request, CancellationToken ct)
    {
        IQueryable<Hrm.Entity.TableEntities.Employee> query = _db.Employees.AsNoTracking();

        if (request.EmployeeId is long id)
        {
            query = query.Where(e => e.EmployeeId == id);
        }
        else if (request.UserId is Guid user)
        {
            query = query.Where(e => e.UserId == user);
        }
        else if (request.EmployeeIds.Count > 0)
        {
            query = query.Where(e => request.EmployeeIds.Contains(e.EmployeeId));
        }
        else
        {
            query = query.Where(e => e.EmployeeStatus != EmployeeStatus.Exited);
        }

        return await query
            .OrderBy(e => e.EmployeeCode)
            .Select(e => new EmployeeProfile
            {
                EmployeeId = e.EmployeeId,
                EmployeeCode = e.EmployeeCode,
                FullName = e.FirstName + (e.LastName == null ? "" : " " + e.LastName),
                UserId = e.UserId,
                DepartmentId = e.DepartmentId,
                GradeId = e.GradeId,
                WorkLocationId = e.WorkLocationId,
                ReportsToEmployeeId = e.ReportsToEmployeeId,
                JoiningDate = e.JoiningDate,
                ProbationEndDate = e.ProbationEndDate,
                ExitDate = e.ExitDate,
                Gender = e.Gender.ToString(),
                EmployeeStatus = e.EmployeeStatus.ToString(),
            })
            .ToListAsync(ct);
    }
}
