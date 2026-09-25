using System.Net.Http.Json;

namespace Performance.Api.Services;

public interface IPayrollClient
{
    Task<bool> ReviseSalaryAsync(long employeeId, decimal newCtc, DateOnly effectiveFrom, string? reason, CancellationToken ct);
}

public sealed class PayrollClient : IPayrollClient
{
    private readonly HttpClient _http;

    public PayrollClient(HttpClient http) => _http = http;

    public async Task<bool> ReviseSalaryAsync(long employeeId, decimal newCtc, DateOnly effectiveFrom, string? reason, CancellationToken ct)
    {
        try
        {
            var req = new
            {
                EmployeeId = employeeId,
                NewCtc = newCtc,
                EffectiveFrom = effectiveFrom,
                Reason = reason ?? "Performance appraisal increment"
            };
            var res = await _http.PostAsJsonAsync("api/payroll/employee-salaries/revisions", req, ct);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
