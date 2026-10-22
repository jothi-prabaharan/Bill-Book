using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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
        Guid? orgId = null,
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
                int count = await ProcessSeedFileAsync(dbContext, file, orgId, ct);
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
        Guid? orgId = null,
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

        if (orgId.HasValue && orgId.Value != Guid.Empty)
        {
            PropertyInfo? orgProp = typeof(TEntity).GetProperty("OrgId");
            if (orgProp != null && orgProp.PropertyType == typeof(Guid) && orgProp.CanWrite)
            {
                foreach (TEntity item in items)
                {
                    if ((Guid)orgProp.GetValue(item)! == Guid.Empty)
                    {
                        orgProp.SetValue(item, orgId.Value);
                    }
                }
            }
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
        Guid? orgId,
        CancellationToken ct)
    {
        string fileName = Path.GetFileNameWithoutExtension(filePath);

        // Find entity type in model by CLR name, pluralization, or Table name
        var entityType = dbContext.Model.GetEntityTypes()
            .FirstOrDefault(t => IsMatchingEntityType(t, fileName));

        if (entityType is null)
        {
            _logger.LogDebug(
                "No matching entity type found in model for seed file '{File}'. If this file requires custom loading, use SeedFileAsync directly.",
                fileName);
            return 0;
        }

        Type clrType = entityType.ClrType;

        // Build key selector delegate via expression tree
        Delegate keySelectorDelegate = BuildKeySelector(entityType);

        string json = await File.ReadAllTextAsync(filePath, ct);
        Type listType = typeof(List<>).MakeGenericType(clrType);
        object? deserialized = JsonSerializer.Deserialize(json, listType, JsonOptions);

        if (deserialized is not System.Collections.IEnumerable enumerable)
        {
            return 0;
        }

        // Apply OrgId if applicable
        if (orgId.HasValue && orgId.Value != Guid.Empty)
        {
            PropertyInfo? orgProp = clrType.GetProperty("OrgId");
            if (orgProp != null && orgProp.PropertyType == typeof(Guid) && orgProp.CanWrite)
            {
                foreach (object item in enumerable)
                {
                    if ((Guid)orgProp.GetValue(item)! == Guid.Empty)
                    {
                        orgProp.SetValue(item, orgId.Value);
                    }
                }
            }
        }

        // Dynamically invoke SeedEntitiesAsync<TEntity>
        MethodInfo method = typeof(JsonSeedLoader).GetMethod(nameof(SeedEntitiesAsync))!
            .MakeGenericMethod(clrType);

        var task = (Task<int>)method.Invoke(this, [dbContext, enumerable, keySelectorDelegate, ct])!;
        return await task;
    }

    private static bool IsMatchingEntityType(IEntityType t, string fileName)
    {
        string clrName = t.ClrType.Name;
        string? tableName = t.GetTableName();

        if (string.Equals(clrName, fileName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tableName, fileName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Plural matching: "Accounts" -> "Account", "PaymentTerms" -> "PaymentTerm"
        if (string.Equals(clrName + "s", fileName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(clrName + "es", fileName, StringComparison.OrdinalIgnoreCase) ||
            (tableName != null && string.Equals(tableName + "s", fileName, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // "ies" to "y" matching: "MetalPurities" -> "MetalPurity", "Categories" -> "Category"
        if (fileName.EndsWith("ies", StringComparison.OrdinalIgnoreCase) &&
            clrName.EndsWith("y", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(fileName[..^3] + "y", clrName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static Delegate BuildKeySelector(IEntityType entityType)
    {
        Type clrType = entityType.ClrType;
        ParameterExpression param = Expression.Parameter(clrType, "e");

        // Determine key properties: prefer unique index or natural key over auto-increment identity PK
        List<PropertyInfo> keyProps = DetermineKeyProperties(entityType);

        Expression body;
        if (keyProps.Count == 1)
        {
            body = Expression.Convert(Expression.Property(param, keyProps[0]), typeof(object));
        }
        else
        {
            // Composite key: concatenate properties with delimiter e.g. "OrgId|AccountCode"
            var stringExpressions = keyProps.Select(p =>
            {
                MemberExpression propAccess = Expression.Property(param, p);
                MethodInfo toStringMethod = typeof(object).GetMethod(nameof(ToString), Type.EmptyTypes)!;
                Expression nullCheck = Expression.Condition(
                    Expression.Equal(Expression.Convert(propAccess, typeof(object)), Expression.Constant(null)),
                    Expression.Constant(string.Empty),
                    Expression.Call(Expression.Convert(propAccess, typeof(object)), toStringMethod));
                return nullCheck;
            }).ToList();

            // Interleave with "|"
            List<Expression> withDelimiters = new();
            for (int i = 0; i < stringExpressions.Count; i++)
            {
                if (i > 0) withDelimiters.Add(Expression.Constant("|"));
                withDelimiters.Add(stringExpressions[i]);
            }

            MethodInfo concatMethod = typeof(string).GetMethod(nameof(string.Concat), [typeof(string[])])!;
            NewArrayExpression newArray = Expression.NewArrayInit(typeof(string), withDelimiters);
            body = Expression.Call(concatMethod, newArray);
        }

        Type delegateType = typeof(Func<,>).MakeGenericType(clrType, typeof(object));
        return Expression.Lambda(delegateType, body, param).Compile();
    }

    private static List<PropertyInfo> DetermineKeyProperties(IEntityType entityType)
    {
        Type clrType = entityType.ClrType;

        // 1. Check if there is a unique index defined on the entity
        var uniqueIndex = entityType.GetIndexes()
            .FirstOrDefault(i => i.IsUnique && !i.Properties.Any(p => p.ValueGenerated == ValueGenerated.OnAdd));

        if (uniqueIndex != null)
        {
            return uniqueIndex.Properties
                .Select(p => clrType.GetProperty(p.Name)!)
                .Where(p => p != null)
                .ToList();
        }

        // 2. Check for natural code / system name properties
        PropertyInfo? orgProp = clrType.GetProperty("OrgId");
        string[] candidateNames = ["SystemName", "Code", "AccountCode", "SeriesCode", "SectionCode", "RoleSystemName", "TaxSystemName", "Priority", "TemplateName", "Key", "Name"];

        PropertyInfo? naturalProp = candidateNames
            .Select(name => clrType.GetProperties().FirstOrDefault(p =>
                string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) ||
                p.Name.EndsWith(name, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(p => p != null);

        if (naturalProp != null)
        {
            List<PropertyInfo> list = new();
            if (orgProp != null)
            {
                list.Add(orgProp);
            }
            list.Add(naturalProp);
            return list;
        }

        // 3. Fallback to Primary Key properties
        var primaryKey = entityType.FindPrimaryKey();
        if (primaryKey != null && primaryKey.Properties.Count > 0)
        {
            return primaryKey.Properties
                .Select(p => clrType.GetProperty(p.Name)!)
                .Where(p => p != null)
                .ToList();
        }

        throw new InvalidOperationException($"Unable to determine key selector for entity '{clrType.Name}'.");
    }
}
