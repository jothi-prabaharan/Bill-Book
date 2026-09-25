using System.Net.Http.Json;

namespace Recruitment.Api.Services;

public interface IPayrollClient
{
    Task<bool> AssignSalaryAsync(long employeeId, long salaryStructureId, decimal annualCtc, DateOnly effectiveFrom, CancellationToken ct);
}

public sealed class PayrollClient : IPayrollClient
{
    private readonly HttpClient _http;

    public PayrollClient(HttpClient http) => _http = http;

    public async Task<bool> AssignSalaryAsync(long employeeId, long salaryStructureId, decimal annualCtc, DateOnly effectiveFrom, CancellationToken ct)
    {
        try
        {
            var req = new
            {
                EmployeeId = employeeId,
                SalaryStructureId = salaryStructureId,
                AnnualCtc = annualCtc,
                EffectiveFrom = effectiveFrom,
            };
            var res = await _http.PostAsJsonAsync("api/payroll/employee-salaries", req, ct);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
