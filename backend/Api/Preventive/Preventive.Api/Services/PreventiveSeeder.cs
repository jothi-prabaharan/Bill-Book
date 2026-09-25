namespace Preventive.Api.Services;

/// <summary>
/// Preventive has no master data to seed (S7, TK-67): every plan is the
/// branch's own. The endpoint exists so every School service answers Master's
/// fan-out alike.
/// </summary>
public sealed class PreventiveSeeder
{
    public Task<Dictionary<string, int>> SeedForOrganizationAsync(Guid orgId, CancellationToken ct) =>
        Task.FromResult(new Dictionary<string, int>());
}
