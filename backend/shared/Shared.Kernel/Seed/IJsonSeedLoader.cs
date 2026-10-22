using Microsoft.EntityFrameworkCore;

namespace Shared.Kernel.Seed;

/// <summary>
/// Contract for dynamic, idempotent seed data loading from JSON files.
/// </summary>
public interface IJsonSeedLoader
{
    /// <summary>
    /// Loads and applies seed data from JSON files located in <paramref name="seedDirectoryPath"/>
    /// for the given <paramref name="dbContext"/>.
    /// </summary>
    /// <param name="dbContext">The target EF Core DbContext.</param>
    /// <param name="seedDirectoryPath">Directory containing the *.json seed files.</param>
    /// <param name="orgId">Optional organization ID to populate into org-scoped entities if not specified in JSON.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The total number of newly inserted seed entities.</returns>
    Task<int> SeedFromDirectoryAsync(DbContext dbContext, string seedDirectoryPath, Guid? orgId = null, CancellationToken ct = default);

    /// <summary>
    /// Idempotently seeds entities of type <typeparamref name="TEntity"/> from a specific JSON file.
    /// Existing records matching <paramref name="keySelector"/> are preserved without duplicate inserts.
    /// </summary>
    /// <typeparam name="TEntity">The EF Core entity type.</typeparam>
    /// <param name="dbContext">The target EF Core DbContext.</param>
    /// <param name="jsonFilePath">Full path to the JSON file.</param>
    /// <param name="keySelector">Function returning the key used for identity check (e.g. ID, code, or composite key).</param>
    /// <param name="orgId">Optional organization ID to populate if empty.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of entities inserted.</returns>
    Task<int> SeedFileAsync<TEntity>(
        DbContext dbContext,
        string jsonFilePath,
        Func<TEntity, object> keySelector,
        Guid? orgId = null,
        CancellationToken ct = default) where TEntity : class;

    /// <summary>
    /// Idempotently seeds an explicit collection of entities.
    /// </summary>
    Task<int> SeedEntitiesAsync<TEntity>(
        DbContext dbContext,
        IEnumerable<TEntity> entities,
        Func<TEntity, object> keySelector,
        CancellationToken ct = default) where TEntity : class;
}
