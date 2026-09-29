
namespace Facility.Repository.Repositories;

/// <summary>
/// Facility has no master data to seed (S5, TK-65): every campus's buildings
/// are its own. The endpoint exists so every School service answers Master's
/// fan-out alike.
/// </summary>
public sealed class FacilitySeederRepository
{
    public Task<Dictionary<string, int>> SeedForOrganizationAsync(Guid orgId, CancellationToken ct) =>
        Task.FromResult(new Dictionary<string, int>());
}
