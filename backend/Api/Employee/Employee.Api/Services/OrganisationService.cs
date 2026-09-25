using Employee.Entity.Models;
using Employee.Entity.TableEntities;
using Employee.Repository;
using Microsoft.EntityFrameworkCore;

namespace Employee.Api.Services;

/// <summary>
/// Organisation setup (H1, TK-48): departments, designations, grades, cost
/// centres and work locations. Codes are unique per branch; a row is
/// deactivated, never deleted, because employees and history rows name it.
/// Every read and write is the caller's branch's, through the query filter.
/// </summary>
public sealed class OrganisationService
{
    private readonly EmployeeDbContext _db;

    public OrganisationService(EmployeeDbContext db) => _db = db;

    // ---- Lists -----------------------------------------------------------

    public async Task<IReadOnlyList<OrgMasterRow>> DepartmentsAsync(CancellationToken ct) =>
        await _db.Departments.AsNoTracking().OrderBy(d => d.Name)
            .Select(d => new OrgMasterRow
            {
                Id = d.DepartmentId, Code = d.Code, Name = d.Name, IsActive = d.IsActive,
                HeadEmployeeId = d.HeadEmployeeId, ParentDepartmentId = d.ParentDepartmentId,
            })
            .ToListAsync(ct);

    public async Task<IReadOnlyList<OrgMasterRow>> DesignationsAsync(CancellationToken ct) =>
        await _db.Designations.AsNoTracking().OrderBy(d => d.Name)
            .Select(d => new OrgMasterRow { Id = d.DesignationId, Code = d.Code, Name = d.Name, IsActive = d.IsActive })
            .ToListAsync(ct);

    public async Task<IReadOnlyList<OrgMasterRow>> GradesAsync(CancellationToken ct) =>
        await _db.Grades.AsNoTracking().OrderBy(g => g.SortOrder).ThenBy(g => g.Name)
            .Select(g => new OrgMasterRow
            {
                Id = g.GradeId, Code = g.Code, Name = g.Name, IsActive = g.IsActive,
                SortOrder = g.SortOrder, NoticePeriodDays = g.NoticePeriodDays,
            })
            .ToListAsync(ct);

    public async Task<IReadOnlyList<OrgMasterRow>> CostCentresAsync(CancellationToken ct) =>
        await _db.CostCentres.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new OrgMasterRow { Id = c.CostCentreId, Code = c.Code, Name = c.Name, IsActive = c.IsActive })
            .ToListAsync(ct);

    public async Task<IReadOnlyList<OrgMasterRow>> WorkLocationsAsync(CancellationToken ct) =>
        await _db.WorkLocations.AsNoTracking().OrderBy(w => w.Name)
            .Select(w => new OrgMasterRow
            {
                Id = w.WorkLocationId, Code = w.Code, Name = w.Name, IsActive = w.IsActive,
                StateId = w.StateId, AddressLine1 = w.AddressLine1, City = w.City,
                Latitude = w.Latitude, Longitude = w.Longitude, GeoFenceMetres = w.GeoFenceMetres,
            })
            .ToListAsync(ct);

    // ---- Saves (id null creates) ----------------------------------------

    public async Task<OrgMasterResult> SaveDepartmentAsync(long? id, SaveDepartmentRequest request, CancellationToken ct)
    {
        string code = request.Code.Trim().ToUpperInvariant();
        if (await _db.Departments.AnyAsync(d => d.Code == code && d.DepartmentId != (id ?? 0), ct))
        {
            return new OrgMasterResult(OrgMasterOutcome.DuplicateCode);
        }

        if (request.HeadEmployeeId is long head && !await _db.Employees.AnyAsync(e => e.EmployeeId == head, ct))
        {
            return new OrgMasterResult(OrgMasterOutcome.InvalidReference);
        }

        if (request.ParentDepartmentId is long parent
            && (!await _db.Departments.AnyAsync(d => d.DepartmentId == parent, ct)
                || (id is long self && await IsAncestorOrSelfAsync(self, parent, ct))))
        {
            return new OrgMasterResult(OrgMasterOutcome.InvalidReference);
        }

        Department? row = id is long existing
            ? await _db.Departments.FirstOrDefaultAsync(d => d.DepartmentId == existing, ct)
            : new Department();
        if (row is null)
        {
            return new OrgMasterResult(OrgMasterOutcome.NotFound);
        }

        row.Code = code;
        row.Name = request.Name.Trim();
        row.IsActive = request.IsActive;
        row.HeadEmployeeId = request.HeadEmployeeId;
        row.ParentDepartmentId = request.ParentDepartmentId;
        if (id is null)
        {
            _db.Departments.Add(row);
        }

        await _db.SaveChangesAsync(ct);
        return new OrgMasterResult(OrgMasterOutcome.Ok, row.DepartmentId);
    }

    public async Task<OrgMasterResult> SaveDesignationAsync(long? id, SaveOrgMasterRequest request, CancellationToken ct)
    {
        string code = request.Code.Trim().ToUpperInvariant();
        if (await _db.Designations.AnyAsync(d => d.Code == code && d.DesignationId != (id ?? 0), ct))
        {
            return new OrgMasterResult(OrgMasterOutcome.DuplicateCode);
        }

        Designation? row = id is long existing
            ? await _db.Designations.FirstOrDefaultAsync(d => d.DesignationId == existing, ct)
            : new Designation();
        if (row is null)
        {
            return new OrgMasterResult(OrgMasterOutcome.NotFound);
        }

        row.Code = code;
        row.Name = request.Name.Trim();
        row.IsActive = request.IsActive;
        if (id is null)
        {
            _db.Designations.Add(row);
        }

        await _db.SaveChangesAsync(ct);
        return new OrgMasterResult(OrgMasterOutcome.Ok, row.DesignationId);
    }

    public async Task<OrgMasterResult> SaveGradeAsync(long? id, SaveGradeRequest request, CancellationToken ct)
    {
        string code = request.Code.Trim().ToUpperInvariant();
        if (await _db.Grades.AnyAsync(g => g.Code == code && g.GradeId != (id ?? 0), ct))
        {
            return new OrgMasterResult(OrgMasterOutcome.DuplicateCode);
        }

        Grade? row = id is long existing
            ? await _db.Grades.FirstOrDefaultAsync(g => g.GradeId == existing, ct)
            : new Grade();
        if (row is null)
        {
            return new OrgMasterResult(OrgMasterOutcome.NotFound);
        }

        row.Code = code;
        row.Name = request.Name.Trim();
        row.IsActive = request.IsActive;
        row.SortOrder = request.SortOrder;
        row.NoticePeriodDays = request.NoticePeriodDays;
        if (id is null)
        {
            _db.Grades.Add(row);
        }

        await _db.SaveChangesAsync(ct);
        return new OrgMasterResult(OrgMasterOutcome.Ok, row.GradeId);
    }

    public async Task<OrgMasterResult> SaveCostCentreAsync(long? id, SaveOrgMasterRequest request, CancellationToken ct)
    {
        string code = request.Code.Trim().ToUpperInvariant();
        if (await _db.CostCentres.AnyAsync(c => c.Code == code && c.CostCentreId != (id ?? 0), ct))
        {
            return new OrgMasterResult(OrgMasterOutcome.DuplicateCode);
        }

        CostCentre? row = id is long existing
            ? await _db.CostCentres.FirstOrDefaultAsync(c => c.CostCentreId == existing, ct)
            : new CostCentre();
        if (row is null)
        {
            return new OrgMasterResult(OrgMasterOutcome.NotFound);
        }

        row.Code = code;
        row.Name = request.Name.Trim();
        row.IsActive = request.IsActive;
        if (id is null)
        {
            _db.CostCentres.Add(row);
        }

        await _db.SaveChangesAsync(ct);
        return new OrgMasterResult(OrgMasterOutcome.Ok, row.CostCentreId);
    }

    public async Task<OrgMasterResult> SaveWorkLocationAsync(long? id, SaveWorkLocationRequest request, CancellationToken ct)
    {
        string code = request.Code.Trim().ToUpperInvariant();
        if (await _db.WorkLocations.AnyAsync(w => w.Code == code && w.WorkLocationId != (id ?? 0), ct))
        {
            return new OrgMasterResult(OrgMasterOutcome.DuplicateCode);
        }

        WorkLocation? row = id is long existing
            ? await _db.WorkLocations.FirstOrDefaultAsync(w => w.WorkLocationId == existing, ct)
            : new WorkLocation();
        if (row is null)
        {
            return new OrgMasterResult(OrgMasterOutcome.NotFound);
        }

        row.Code = code;
        row.Name = request.Name.Trim();
        row.IsActive = request.IsActive;
        row.StateId = request.StateId;
        row.AddressLine1 = request.AddressLine1;
        row.City = request.City;
        row.Latitude = request.Latitude;
        row.Longitude = request.Longitude;
        row.GeoFenceMetres = request.GeoFenceMetres;
        if (id is null)
        {
            _db.WorkLocations.Add(row);
        }

        await _db.SaveChangesAsync(ct);
        return new OrgMasterResult(OrgMasterOutcome.Ok, row.WorkLocationId);
    }

    /// <summary>Whether <paramref name="department"/> is <paramref name="candidate"/> or one of its ancestors.</summary>
    private async Task<bool> IsAncestorOrSelfAsync(long department, long candidate, CancellationToken ct)
    {
        Dictionary<long, long?> parents = await _db.Departments.AsNoTracking()
            .ToDictionaryAsync(d => d.DepartmentId, d => d.ParentDepartmentId, ct);

        // Bounded by the number of departments, so a cycle already in the data
        // cannot loop for ever.
        int steps = 0;
        for (long? at = candidate; at is long current && steps <= parents.Count; at = parents.GetValueOrDefault(current), steps++)
        {
            if (current == department)
            {
                return true;
            }
        }

        return false;
    }
}
