using System.Net.Http.Json;
using Shared.Kernel.Approvals;

namespace Master.Api.Services;

/// <summary>
/// Asks Hrm for the approvers only it can find — the reporting chain, a
/// relationship, a department head, a named employee (D-26, TK-49). Master
/// resolves users and roles itself; the employee graph is Hrm's.
/// </summary>
public interface IHrmApproverClient
{
    /// <summary>One answer per query, or throws when Hrm cannot be asked.</summary>
    Task<IReadOnlyList<EmployeeApproverAnswer>> ResolveAsync(ResolveEmployeesRequest request, CancellationToken ct);
}

public sealed class HrmApproverClient : IHrmApproverClient
{
    private readonly HttpClient _http;

    public HrmApproverClient(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<EmployeeApproverAnswer>> ResolveAsync(ResolveEmployeesRequest request, CancellationToken ct)
    {
        if (_http.BaseAddress is null)
        {
            throw new InvalidOperationException("Hrm:BaseUrl is not configured, so employee approvers cannot be found.");
        }

        using HttpResponseMessage response = await _http.PostAsJsonAsync("internal/approval-chains/resolve-employees", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<EmployeeApproverAnswer>>(ct) ?? [];
    }
}
