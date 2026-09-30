using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Audit;

/// <summary>
/// <c>ExecuteUpdate</c> and <c>ExecuteDelete</c> that write their edit log rows
/// (TK-118).
///
/// A bulk statement goes straight to the database and the change tracker never
/// hears of it, so the log written inside <c>SaveChanges</c> would miss exactly
/// the guarded updates the ledger and the registers rely on. These read the
/// affected rows first, run the statement, and — for an update — read them
/// again by key, then save one log row per row changed, in the caller's
/// transaction.
///
/// <b>They save.</b> The log rows are written with <c>SaveChanges</c>, which also
/// writes anything else the context holds pending. Every caller is inside the
/// request's transaction, so that is an earlier write in the same unit of work
/// rather than a separate one.
///
/// Use them on audited tables. A queue's status claim, a derived running
/// balance or a counter keeps the plain statement; each such call site says why.
/// </summary>
public static class EditLogBulk
{
    public static async Task<int> ExecuteDeleteLoggedAsync<T>(
        this IQueryable<T> query, TenantDbContext context, CancellationToken ct, string source = "ExecuteDelete")
        where T : class
    {
        IEntityType type = context.Model.FindEntityType(typeof(T))
            ?? throw new InvalidOperationException($"{typeof(T).Name} is not mapped by {context.GetType().Name}.");

        if (!EditLogWriter.IsAudited(type))
        {
            return await query.ExecuteDeleteAsync(ct);
        }

        List<T> before = await query.AsNoTracking().ToListAsync(ct);
        if (before.Count == 0)
        {
            return 0;
        }

        int deleted = await query.ExecuteDeleteAsync(ct);

        await SaveAsync(context, type, before.Select(row => (Before: (T?)row, After: (T?)null)), EditOperation.Delete, source, ct);
        return deleted;
    }

    public static async Task<int> ExecuteUpdateLoggedAsync<T>(
        this IQueryable<T> query,
        TenantDbContext context,
        Action<UpdateSettersBuilder<T>> setters,
        CancellationToken ct,
        string source = "ExecuteUpdate")
        where T : class
    {
        IEntityType type = context.Model.FindEntityType(typeof(T))
            ?? throw new InvalidOperationException($"{typeof(T).Name} is not mapped by {context.GetType().Name}.");

        if (!EditLogWriter.IsAudited(type))
        {
            return await query.ExecuteUpdateAsync(setters, ct);
        }

        IProperty key = SingleKey(type);
        List<T> before = await query.AsNoTracking().ToListAsync(ct);
        if (before.Count == 0)
        {
            return 0;
        }

        int updated = await query.ExecuteUpdateAsync(setters, ct);

        // Read back by key: the statement may have changed the very columns the
        // query filtered on, so the query itself would no longer find them.
        List<object> keys = [.. before.Select(row => key.PropertyInfo!.GetValue(row)!)];
        List<T> after = await ByKeys<T>(context, key, keys).ToListAsync(ct);
        Dictionary<object, T> afterByKey = after.ToDictionary(row => key.PropertyInfo!.GetValue(row)!);

        await SaveAsync(
            context, type,
            before.Select(row => (Before: (T?)row, After: afterByKey.GetValueOrDefault(key.PropertyInfo!.GetValue(row)!))),
            EditOperation.Update, source, ct);

        return updated;
    }

    private static async Task SaveAsync<T>(
        TenantDbContext context,
        IEntityType type,
        IEnumerable<(T? Before, T? After)> pairs,
        EditOperation operation,
        string source,
        CancellationToken ct)
        where T : class
    {
        (Guid customerId, Guid orgId) = context.RequireTenant();
        (Guid? user, string? userName) = context.EditLogActor();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool any = false;

        foreach ((T? before, T? after) in pairs)
        {
            // An updated row that could not be read back is compared with itself
            // and so writes nothing; every update in a tenant transaction reads back.
            EditLog? row = EditLogWriter.ForValues(
                context, type, operation,
                before is null ? null : p => Read(p, before),
                after is null ? (before is null ? null : p => Read(p, before)) : p => Read(p, after),
                user, userName, now, source);

            if (row is null)
            {
                continue;
            }

            row.CustomerId = customerId;
            row.OrgId = orgId;
            context.EditLogs.Add(row);
            any = true;
        }

        if (any)
        {
            await context.SaveChangesAsync(ct);
        }
    }

    private static object? Read(IProperty property, object row) =>
        property.PropertyInfo is { } info
            ? info.GetValue(row)
            : null;

    private static IProperty SingleKey(IEntityType type)
    {
        IReadOnlyList<IProperty> key = type.FindPrimaryKey()!.Properties;
        return key.Count == 1 && key[0].PropertyInfo is not null
            ? key[0]
            : throw new InvalidOperationException(
                $"{type.ClrType.Name} has a composite or shadow key; log its bulk update by reading the rows and saving them tracked.");
    }

    private static IQueryable<T> ByKeys<T>(TenantDbContext context, IProperty key, List<object> keys)
        where T : class
    {
        IQueryable<T> set = context.Set<T>().AsNoTracking();
        string name = key.Name;

        // Typed lists built outside the expression, so EF sends one array parameter.
        if (key.ClrType == typeof(long))
        {
            List<long> ids = [.. keys.Cast<long>()];
            return set.Where(e => ids.Contains(EF.Property<long>(e, name)));
        }

        if (key.ClrType == typeof(int))
        {
            List<int> ids = [.. keys.Cast<int>()];
            return set.Where(e => ids.Contains(EF.Property<int>(e, name)));
        }

        if (key.ClrType == typeof(Guid))
        {
            List<Guid> ids = [.. keys.Cast<Guid>()];
            return set.Where(e => ids.Contains(EF.Property<Guid>(e, name)));
        }

        throw new InvalidOperationException($"{typeof(T).Name}'s key type {key.ClrType.Name} is not supported for a logged bulk update.");
    }
}
