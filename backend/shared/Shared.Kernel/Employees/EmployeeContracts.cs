namespace Shared.Kernel.Employees;

/// <summary>
/// What another service may know about an employee (TK-49), from Employee's
/// internal routes. No salary, no PAN, no bank details: leave and attendance
/// need where someone works and who they report to, nothing more.
/// </summary>
public sealed class EmployeeProfile
{
    public long EmployeeId { get; set; }

    public string EmployeeCode { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public Guid? UserId { get; set; }

    public long DepartmentId { get; set; }

    public long GradeId { get; set; }

    public long WorkLocationId { get; set; }

    public long? ReportsToEmployeeId { get; set; }

    public DateOnly JoiningDate { get; set; }

    public DateOnly? ProbationEndDate { get; set; }

    public DateOnly? ExitDate { get; set; }

    /// <summary>By name: Male, Female, Other, NotStated.</summary>
    public string Gender { get; set; } = null!;

    /// <summary>By name: Onboarding, Active, OnNotice, Exited.</summary>
    public string EmployeeStatus { get; set; } = null!;
}

/// <summary>The branch, named in the body of an internal call, and what is asked for.</summary>
public sealed class EmployeeLookupRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public long? EmployeeId { get; set; }

    public Guid? UserId { get; set; }

    /// <summary>Several at once, for a list screen.</summary>
    public List<long> EmployeeIds { get; set; } = [];
}

/// <summary>Internal contract for creating or finding an onboarding employee from an accepted offer (TK-57).</summary>
public sealed class OnboardEmployeeRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public long OfferId { get; set; }

    public string FirstName { get; set; } = null!;

    public string? LastName { get; set; }

    public string Email { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public long DepartmentId { get; set; }

    public long DesignationId { get; set; }

    public long GradeId { get; set; }

    public long WorkLocationId { get; set; }

    public DateOnly JoiningDate { get; set; }

    public string EmploymentType { get; set; } = "Permanent";

    public string? Remarks { get; set; }
}

public sealed class OnboardEmployeeResult
{
    public long EmployeeId { get; set; }

    public string EmployeeCode { get; set; } = null!;

    public bool AlreadyExisted { get; set; }
}


/// <summary>
/// Who an employee id is (School work orders, TK-66): an assignee is checked
/// through Employee's <c>internal/employees/lookup</c> before it is stored.
/// </summary>
public interface IEmployeeDirectory
{
    /// <summary>The employees among <paramref name="ids"/> in the current branch. Throws when Employee cannot be asked.</summary>
    Task<IReadOnlyDictionary<long, EmployeeProfile>> FindAsync(IEnumerable<long> ids, CancellationToken ct);
}

public sealed class HttpEmployeeDirectory : IEmployeeDirectory
{
    private readonly HttpClient _http;
    private readonly Shared.Kernel.Tenancy.ITenantContext _tenant;

    public HttpEmployeeDirectory(HttpClient http, Shared.Kernel.Tenancy.ITenantContext tenant)
    {
        _http = http;
        _tenant = tenant;
    }

    public async Task<IReadOnlyDictionary<long, EmployeeProfile>> FindAsync(IEnumerable<long> ids, CancellationToken ct)
    {
        List<long> wanted = [.. ids.Distinct()];
        if (wanted.Count == 0)
        {
            return new Dictionary<long, EmployeeProfile>();
        }

        using HttpResponseMessage response = await System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(_http, "internal/employees/lookup", new EmployeeLookupRequest
        {
            CustomerId = _tenant.CustomerId ?? Guid.Empty,
            OrgId = _tenant.OrgId ?? Guid.Empty,
            EmployeeIds = wanted,
        }, ct);
        response.EnsureSuccessStatusCode();

        List<EmployeeProfile> found = await System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<List<EmployeeProfile>>(response.Content, ct) ?? [];
        return found.ToDictionary(e => e.EmployeeId);
    }
}
