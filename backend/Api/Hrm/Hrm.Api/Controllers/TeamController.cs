using Hrm.Entity.Enums;
using Hrm.Entity.Models;
using Hrm.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;

namespace Hrm.Api.Controllers;

/// <summary>
/// Manager team endpoints (H8, TK-55): direct and indirect reports.
/// Resolves the manager strictly from the caller's JWT user claim ('sub'), never an ID in the URL.
/// </summary>
[ApiController]
[Authorize]
[RequireApp(App.Hrms)]
[Route("api/hrm/team")]
[Route("api/team")]
public sealed class TeamController : ControllerBase
{
    private readonly HrmDbContext _db;
    private readonly ICurrentUser _currentUser;

    public TeamController(HrmDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet("members")]
    public async Task<IActionResult> GetTeamMembers(CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Forbid();
        }

        var manager = await _db.Employees
            .FirstOrDefaultAsync(x => x.UserId == userId && x.EmployeeStatus != EmployeeStatus.Exited, ct);

        if (manager is null)
        {
            return NotFound(new { message = "No active employee profile linked to your user account." });
        }

        var allEmployees = await _db.Employees
            .AsNoTracking()
            .Where(e => e.EmployeeStatus != EmployeeStatus.Exited)
            .Select(e => new
            {
                e.EmployeeId,
                e.EmployeeCode,
                FullName = e.FirstName + (e.LastName == null ? "" : " " + e.LastName),
                e.DepartmentId,
                e.DesignationId,
                e.WorkLocationId,
                e.Phone,
                e.WorkEmail,
                e.JoiningDate,
                e.ReportsToEmployeeId,
                e.EmployeeStatus
            })
            .ToListAsync(ct);

        var departments = await _db.Departments.AsNoTracking().ToDictionaryAsync(d => d.DepartmentId, d => d.Name, ct);
        var designations = await _db.Designations.AsNoTracking().ToDictionaryAsync(d => d.DesignationId, d => d.Name, ct);
        var locations = await _db.WorkLocations.AsNoTracking().ToDictionaryAsync(l => l.WorkLocationId, l => l.Name, ct);

        var result = new List<TeamMemberDto>();
        var queue = new Queue<(long EmpId, int Level)>();

        // Direct reports
        foreach (var emp in allEmployees.Where(e => e.ReportsToEmployeeId == manager.EmployeeId))
        {
            queue.Enqueue((emp.EmployeeId, 1));
        }

        var visited = new HashSet<long> { manager.EmployeeId };

        while (queue.Count > 0)
        {
            var (currentId, level) = queue.Dequeue();
            if (!visited.Add(currentId)) continue;

            var emp = allEmployees.FirstOrDefault(e => e.EmployeeId == currentId);
            if (emp is null) continue;

            string? managerName = emp.ReportsToEmployeeId.HasValue
                ? allEmployees.FirstOrDefault(m => m.EmployeeId == emp.ReportsToEmployeeId.Value)?.FullName
                : null;

            result.Add(new TeamMemberDto
            {
                EmployeeId = emp.EmployeeId,
                EmployeeCode = emp.EmployeeCode,
                FullName = emp.FullName,
                DepartmentName = emp.DepartmentId > 0 && departments.TryGetValue(emp.DepartmentId, out var dn) ? dn : null,
                DesignationName = emp.DesignationId > 0 && designations.TryGetValue(emp.DesignationId, out var dsn) ? dsn : null,
                WorkLocationName = emp.WorkLocationId > 0 && locations.TryGetValue(emp.WorkLocationId, out var ln) ? ln : null,
                Phone = emp.Phone,
                WorkEmail = emp.WorkEmail,
                JoiningDate = emp.JoiningDate,
                Level = level,
                ReportsToEmployeeId = emp.ReportsToEmployeeId,
                ReportsToName = managerName,
                Status = emp.EmployeeStatus.ToString()
            });

            // Enqueue downstream reports
            foreach (var child in allEmployees.Where(e => e.ReportsToEmployeeId == currentId))
            {
                if (!visited.Contains(child.EmployeeId))
                {
                    queue.Enqueue((child.EmployeeId, level + 1));
                }
            }
        }

        return Ok(result.OrderBy(r => r.Level).ThenBy(r => r.FullName).ToList());
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetTeamSummary(CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Forbid();
        }

        var manager = await _db.Employees
            .FirstOrDefaultAsync(x => x.UserId == userId && x.EmployeeStatus != EmployeeStatus.Exited, ct);

        if (manager is null)
        {
            return NotFound(new { message = "No active employee profile linked to your user account." });
        }

        var allEmployees = await _db.Employees
            .AsNoTracking()
            .Where(e => e.EmployeeStatus != EmployeeStatus.Exited)
            .Select(e => new { e.EmployeeId, e.ReportsToEmployeeId })
            .ToListAsync(ct);

        int direct = 0;
        int indirect = 0;
        var queue = new Queue<(long EmpId, int Level)>();

        foreach (var emp in allEmployees.Where(e => e.ReportsToEmployeeId == manager.EmployeeId))
        {
            queue.Enqueue((emp.EmployeeId, 1));
        }

        var visited = new HashSet<long> { manager.EmployeeId };

        while (queue.Count > 0)
        {
            var (currentId, level) = queue.Dequeue();
            if (!visited.Add(currentId)) continue;

            if (level == 1) direct++;
            else indirect++;

            foreach (var child in allEmployees.Where(e => e.ReportsToEmployeeId == currentId))
            {
                if (!visited.Contains(child.EmployeeId))
                {
                    queue.Enqueue((child.EmployeeId, level + 1));
                }
            }
        }

        return Ok(new TeamSummaryDto
        {
            TotalMembers = direct + indirect,
            DirectReports = direct,
            IndirectReports = indirect
        });
    }
}
