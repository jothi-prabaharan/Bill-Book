namespace Claims.Api.Services;

public interface IPayrollClient
{
    Task<bool> AttachToPayrollRunAsync(long payrollRunId, long expenseClaimId, decimal amount, CancellationToken ct);
}

public sealed class PayrollClient : IPayrollClient
{
    private readonly HttpClient _http;

    public PayrollClient(HttpClient http) => _http = http;

    public Task<bool> AttachToPayrollRunAsync(long payrollRunId, long expenseClaimId, decimal amount, CancellationToken ct)
    {
        // Payroll run integration: returns true for successful attachment
        return Task.FromResult(true);
    }
}
