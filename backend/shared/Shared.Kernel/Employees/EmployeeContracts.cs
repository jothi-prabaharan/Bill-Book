namespace Shared.Kernel.Employees;

/// <summary>
/// What another service may know about an employee (TK-49), from Hrm's
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
