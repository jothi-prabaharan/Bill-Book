using System.Net.Http.Json;
using Shared.Kernel.Employees;

namespace Performance.Api.Services;

public interface IEmployeeClient
{
    Task<EmployeeProfile?> FindByUserIdAsync(Guid customerId, Guid orgId, Guid userId, CancellationToken ct);
    Task<EmployeeProfile?> FindByIdAsync(Guid customerId, Guid orgId, long employeeId, CancellationToken ct);
    Task<List<EmployeeProfile>> LookupAsync(Guid customerId, Guid orgId, List<long> employeeIds, CancellationToken ct);
}

public sealed class EmployeeClient : IEmployeeClient
{
    private readonly HttpClient _http;

    public EmployeeClient(HttpClient http) => _http = http;

    public async Task<EmployeeProfile?> FindByUserIdAsync(Guid customerId, Guid orgId, Guid userId, CancellationToken ct)
    {
        try
        {
            var req = new EmployeeLookupRequest
            {
                CustomerId = customerId,
                OrgId = orgId,
                UserId = userId
            };
            var res = await _http.PostAsJsonAsync("internal/employees/lookup", req, ct);
            if (!res.IsSuccessStatusCode) return null;
            var list = await res.Content.ReadFromJsonAsync<List<EmployeeProfile>>(cancellationToken: ct);
            return list?.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public async Task<EmployeeProfile?> FindByIdAsync(Guid customerId, Guid orgId, long employeeId, CancellationToken ct)
    {
        try
        {
            var req = new EmployeeLookupRequest
            {
                CustomerId = customerId,
                OrgId = orgId,
                EmployeeId = employeeId
            };
            var res = await _http.PostAsJsonAsync("internal/employees/lookup", req, ct);
            if (!res.IsSuccessStatusCode) return null;
            var list = await res.Content.ReadFromJsonAsync<List<EmployeeProfile>>(cancellationToken: ct);
            return list?.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<EmployeeProfile>> LookupAsync(Guid customerId, Guid orgId, List<long> employeeIds, CancellationToken ct)
    {
        try
        {
            var req = new EmployeeLookupRequest
            {
                CustomerId = customerId,
                OrgId = orgId,
                EmployeeIds = employeeIds
            };
            var res = await _http.PostAsJsonAsync("internal/employees/lookup", req, ct);
            if (!res.IsSuccessStatusCode) return [];
            var list = await res.Content.ReadFromJsonAsync<List<EmployeeProfile>>(cancellationToken: ct);
            return list ?? [];
        }
        catch
        {
            return [];
        }
    }
}
