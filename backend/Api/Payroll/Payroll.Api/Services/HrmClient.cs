namespace Payroll.Api.Services;

public interface IHrmClient
{
    Task SettleEmployeeAsync(long employeeId, DateOnly lastWorkingDate, CancellationToken ct);
}

public sealed class HrmClient : IHrmClient
{
    private readonly HttpClient _http;

    public HrmClient(HttpClient http) => _http = http;

    public async Task SettleEmployeeAsync(long employeeId, DateOnly lastWorkingDate, CancellationToken ct)
    {
        try
        {
            await _http.PostAsync($"internal/hrm/employees/{employeeId}/settle?lastWorkingDate={lastWorkingDate:yyyy-MM-dd}", null, ct);
        }
        catch
        {
            // Best-effort if HRMS is not licensed
        }
    }
}
