namespace MaintenanceContract.Api.Services;

/// <summary>
/// AMC has no master data to seed (S8, TK-68): every contract is the branch's
/// own. The endpoint exists so every School service answers Master's fan-out alike.
/// </summary>
public sealed class MaintenanceContractSeeder
{
    public Task<Dictionary<string, int>> SeedForOrganizationAsync(Guid orgId, CancellationToken ct) =>
        Task.FromResult(new Dictionary<string, int>());
}
