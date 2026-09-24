using Hrm.Entity.Enums;
using Hrm.Entity.TableEntities;
using Hrm.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Approvals;
using Shared.Kernel.Employees;

namespace Hrm.Api.Services;

/// <summary>
/// The approvers only the employee graph can answer (D-26, TK-49): the
/// reporting chain, a named relationship, the department head and a named
/// employee. Master asks for them while resolving a chain; users and roles it
/// answers itself.
///
/// An approver who has left, or who has no login, is no approver: the level
/// comes back empty and Master's skip rules decide what that means.
/// </summary>
public sealed class EmployeeApproverService
{
    /// <summary>The longest reporting chain walked, so a loop in the data ends.</summary>
    public const int MaxDepth = 10;

    private readonly HrmDbContext _db;
    private readonly TimeProvider _clock;

    public EmployeeApproverService(HrmDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<List<EmployeeApproverAnswer>> ResolveAsync(ResolveEmployeesRequest request, CancellationToken ct)
    {
        Dictionary<long, Node> graph = await _db.Employees
            .AsNoTracking()
            .Select(e => new Node(e.EmployeeId, e.ReportsToEmployeeId, e.DepartmentId, e.UserId, e.EmployeeStatus))
            .ToDictionaryAsync(n => n.EmployeeId, ct);

        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var answers = new List<EmployeeApproverAnswer>();

        foreach (EmployeeApproverQuery level in request.Levels)
        {
            long? found = level.Kind switch
            {
                ApproverKind.ReportingChain => Manager(graph, request.EmployeeId, level.ReportingDepth ?? 1),
                ApproverKind.NamedEmployee => level.NamedEmployeeId,
                ApproverKind.DepartmentHead => await DepartmentHeadAsync(graph, request.EmployeeId, ct),
                ApproverKind.Relationship when level.RelationshipTypeId is long type =>
                    await RelatedAsync(request.EmployeeId, type, today, ct),
                _ => null,
            };

            answers.Add(Answer(graph, level.Sequence, found));
        }

        return answers;
    }

    /// <summary>
    /// The manager <paramref name="depth"/> levels up: 1 is the direct manager,
    /// 2 the manager's manager. Null when the chain ends first or loops.
    /// Public for tests.
    /// </summary>
    public static long? Manager(IReadOnlyDictionary<long, Node> graph, long employeeId, int depth)
    {
        long current = employeeId;
        var seen = new HashSet<long> { employeeId };

        for (int i = 0; i < Math.Clamp(depth, 1, MaxDepth); i++)
        {
            if (!graph.TryGetValue(current, out Node? node) || node.ReportsToEmployeeId is not long next || !seen.Add(next))
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    /// <summary>An answer carrying a login only when the approver can act. Public for tests.</summary>
    public static EmployeeApproverAnswer Answer(IReadOnlyDictionary<long, Node> graph, int sequence, long? employeeId)
    {
        if (employeeId is long id && graph.TryGetValue(id, out Node? node)
            && node.EmployeeStatus != EmployeeStatus.Exited && node.UserId is not null)
        {
            return new EmployeeApproverAnswer { Sequence = sequence, EmployeeId = id, UserId = node.UserId };
        }

        return new EmployeeApproverAnswer { Sequence = sequence, EmployeeId = employeeId };
    }

    /// <summary>
    /// The head of the employee's department. A head asking for their own
    /// leave goes to the head of the department above, so a head is never
    /// their own approver.
    /// </summary>
    private async Task<long?> DepartmentHeadAsync(IReadOnlyDictionary<long, Node> graph, long employeeId, CancellationToken ct)
    {
        if (!graph.TryGetValue(employeeId, out Node? node))
        {
            return null;
        }

        var departments = await _db.Departments
            .AsNoTracking()
            .Select(d => new { d.DepartmentId, d.HeadEmployeeId, d.ParentDepartmentId })
            .ToDictionaryAsync(d => d.DepartmentId, ct);

        long? departmentId = node.DepartmentId;
        var seen = new HashSet<long>();
        while (departmentId is long id && seen.Add(id) && departments.TryGetValue(id, out var department))
        {
            if (department.HeadEmployeeId is long head && head != employeeId)
            {
                return head;
            }

            departmentId = department.ParentDepartmentId;
        }

        return null;
    }

    private Task<long?> RelatedAsync(long employeeId, long relationshipTypeId, DateOnly today, CancellationToken ct) =>
        _db.EmployeeRelationships
            .AsNoTracking()
            .Where(r => r.EmployeeId == employeeId && r.RelationshipTypeId == relationshipTypeId
                && r.FromDate <= today && (r.ToDate == null || r.ToDate >= today))
            .OrderByDescending(r => r.FromDate)
            .Select(r => (long?)r.RelatedEmployeeId)
            .FirstOrDefaultAsync(ct);

    /// <summary>What an approver lookup needs of an employee, and nothing more.</summary>
    public sealed record Node(long EmployeeId, long? ReportsToEmployeeId, long DepartmentId, Guid? UserId, EmployeeStatus EmployeeStatus);
}
