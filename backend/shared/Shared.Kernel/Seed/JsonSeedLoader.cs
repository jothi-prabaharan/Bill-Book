using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Shared.Kernel.Seed;

/// <summary>
/// Default implementation of <see cref="IJsonSeedLoader"/> that reads structured JSON files
/// and seeds EF Core DbContext tables idempotently.
/// </summary>
public class JsonSeedLoader : IJsonSeedLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly ILogger<JsonSeedLoader> _logger;

    public JsonSeedLoader(ILogger<JsonSeedLoader> logger)
    {
        _logger = logger;
    }

    public async Task<int> SeedFromDirectoryAsync(
        DbContext dbContext,
        string seedDirectoryPath,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (string.IsNullOrWhiteSpace(seedDirectoryPath) || !Directory.Exists(seedDirectoryPath))
        {
            _logger.LogDebug("Seed directory '{Path}' does not exist; skipping JSON seed loading.", seedDirectoryPath);
            return 0;
        }

        string[] jsonFiles = Directory.GetFiles(seedDirectoryPath, "*.json", SearchOption.TopDirectoryOnly);
        if (jsonFiles.Length == 0)
        {
            _logger.LogDebug("No JSON seed files found in '{Path}'.", seedDirectoryPath);
            return 0;
        }

        _logger.LogInformation(
            "Discovered {Count} JSON seed file(s) in '{Path}' for DbContext '{DbContext}'.",
            jsonFiles.Length, seedDirectoryPath, dbContext.GetType().Name);

        int totalInserted = 0;

        foreach (string file in jsonFiles.OrderBy(f => f))
        {
            try
            {
                int count = await ProcessSeedFileAsync(dbContext, file, ct);
                totalInserted += count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load and apply seed file '{File}' to '{DbContext}'.", file, dbContext.GetType().Name);
                throw;
            }
        }

        if (totalInserted > 0)
        {
            _logger.LogInformation("Successfully inserted {Count} new seed record(s) into '{DbContext}'.", totalInserted, dbContext.GetType().Name);
        }

        return totalInserted;
    }

    public async Task<int> SeedFileAsync<TEntity>(
        DbContext dbContext,
        string jsonFilePath,
        Func<TEntity, object> keySelector,
        CancellationToken ct = default) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(keySelector);

        if (!File.Exists(jsonFilePath))
        {
            _logger.LogWarning("Seed file '{File}' not found.", jsonFilePath);
            return 0;
        }

        string json = await File.ReadAllTextAsync(jsonFilePath, ct);
        List<TEntity>? items = JsonSerializer.Deserialize<List<TEntity>>(json, JsonOptions);

        if (items is null || items.Count == 0)
        {
            return 0;
        }

        return await SeedEntitiesAsync(dbContext, items, keySelector, ct);
    }

    public async Task<int> SeedEntitiesAsync<TEntity>(
        DbContext dbContext,
        IEnumerable<TEntity> entities,
        Func<TEntity, object> keySelector,
        CancellationToken ct = default) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(keySelector);

        DbSet<TEntity> set = dbContext.Set<TEntity>();

        // Query existing keys from database (ignoring query filters so we see global/system rows)
        List<TEntity> existingList = await set.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        HashSet<object> existingKeys = new(existingList.Select(keySelector));

        List<TEntity> toInsert = new();
        foreach (TEntity entity in entities)
        {
            object key = keySelector(entity);
            if (!existingKeys.Contains(key))
            {
                toInsert.Add(entity);
                existingKeys.Add(key);
            }
        }

        if (toInsert.Count == 0)
        {
            return 0;
        }

        await set.AddRangeAsync(toInsert, ct);
        await dbContext.SaveChangesAsync(ct);
        return toInsert.Count;
    }

    /// <summary>
    /// Processes a single JSON seed file. Attempts to match the file name to an entity type
    /// registered in the EF model.
    /// </summary>
    protected virtual async Task<int> ProcessSeedFileAsync(
        DbContext dbContext,
        string filePath,
        CancellationToken ct)
    {
        string fileName = Path.GetFileNameWithoutExtension(filePath);

        // Find entity type in model by CLR name or Table name
        var entityType = dbContext.Model.GetEntityTypes()
            .FirstOrDefault(t => 
                string.Equals(t.ClrType.Name, fileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t.GetTableName(), fileName, StringComparison.OrdinalIgnoreCase));

        if (entityType is null)
        {
            _logger.LogDebug(
                "No matching entity type found in model for seed file '{File}'. If this file requires custom loading, use SeedFileAsync directly.",
                fileName);
            return 0;
        }

        var clrType = entityType.ClrType;
        var primaryKey = entityType.FindPrimaryKey();
        if (primaryKey is null || primaryKey.Properties.Count == 0)
        {
            _logger.LogWarning("Entity '{Entity}' has no primary key; skipping dynamic JSON seed.", clrType.Name);
            return 0;
        }

        var keyProperty = clrType.GetProperty(primaryKey.Properties[0].Name);
        if (keyProperty is null)
        {
            return 0;
        }

        string json = await File.ReadAllTextAsync(filePath, ct);
        var listType = typeof(List<>).MakeGenericType(clrType);
        var deserialized = JsonSerializer.Deserialize(json, listType, JsonOptions);

        if (deserialized is not System.Collections.IEnumerable enumerable)
        {
            return 0;
        }

        // Dynamically invoke SeedEntitiesAsync<TEntity>
        var method = typeof(JsonSeedLoader).GetMethod(nameof(SeedEntitiesAsync))!
            .MakeGenericMethod(clrType);

        Func<object, object> keySelector = obj => keyProperty.GetValue(obj)!;

        // Create generic Func<TEntity, object>
        var delegateType = typeof(Func<,>).MakeGenericType(clrType, typeof(object));
        var typedKeySelector = Delegate.CreateDelegate(delegateType, keySelector.Target, keySelector.Method);

        var task = (Task<int>)method.Invoke(this, [dbContext, enumerable, typedKeySelector, ct])!;
        return await task;
    }
}
