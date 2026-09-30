using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Shared.Kernel.Entities;
using Shared.Kernel.Errors;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Audit;

/// <summary>
/// Turns what a <c>SaveChanges</c> is about to write into <see cref="EditLog"/>
/// rows (TK-118).
///
/// <b>Two halves, either side of the save.</b> <see cref="Capture"/> runs before
/// it, because an update's old values and a delete's row are only readable then.
/// <see cref="Materialize"/> runs after it, because an insert's key is generated
/// by the database. <see cref="TenantDbContext"/> calls both inside one
/// transaction, so the log commits or rolls back with the change.
///
/// <b>What is left out.</b> The log and the error log themselves; any table or
/// column marked <see cref="NotAuditedAttribute"/>; tables that are not
/// tenant-scoped; and the columns the log row already carries — who, when, the
/// tenant and the <c>xmin</c> version. An update that changes nothing else
/// writes no row.
/// </summary>
public static class EditLogWriter
{
    private static readonly HashSet<string> Carried = new(StringComparer.Ordinal)
    {
        nameof(AuditableEntity.Version),
        nameof(AuditableEntity.CreatedAt),
        nameof(AuditableEntity.CreatedBy),
        nameof(AuditableEntity.ModifiedAt),
        nameof(AuditableEntity.ModifiedBy),
        nameof(OrgScopedEntity.CustomerId),
        nameof(OrgScopedEntity.OrgId),
    };

    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>A change seen before the save, finished after it.</summary>
    public sealed class PendingEdit
    {
        internal PendingEdit(EntityEntry entry, EditOperation operation)
        {
            Entry = entry;
            Operation = operation;
        }

        internal EntityEntry Entry { get; }

        internal EditOperation Operation { get; }

        internal string? Key { get; set; }

        internal (string Name, string Key)? Root { get; set; }

        internal Dictionary<string, Dictionary<string, object?>>? Changes { get; set; }
    }

    /// <summary>Whether a table's changes are logged.</summary>
    public static bool IsAudited(IEntityType type) =>
        type.ClrType != typeof(EditLog)
        && type.ClrType != typeof(ErrorLog)
        && typeof(OrgScopedEntity).IsAssignableFrom(type.ClrType)
        && !type.IsOwned()
        && type.FindPrimaryKey() is not null
        && type.GetTableName() is not null
        && !type.ClrType.IsDefined(typeof(NotAuditedAttribute), inherit: true);

    /// <summary>
    /// Everything the save is about to write to an audited table. Updates and
    /// deletes are read now; inserts are finished by <see cref="Materialize"/>.
    /// </summary>
    public static List<PendingEdit> Capture(DbContext context)
    {
        context.ChangeTracker.DetectChanges();
        List<PendingEdit> pending = [];

        foreach (EntityEntry entry in context.ChangeTracker.Entries().ToList())
        {
            if (!IsAudited(entry.Metadata))
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    pending.Add(new PendingEdit(entry, EditOperation.Insert));
                    break;

                case EntityState.Modified:
                    Dictionary<string, Dictionary<string, object?>> changed = [];
                    foreach (PropertyEntry property in entry.Properties)
                    {
                        if (!property.IsModified || !Logged(property.Metadata)
                            || ValuesEqual(property.OriginalValue, property.CurrentValue))
                        {
                            continue;
                        }

                        changed[property.Metadata.Name] = new()
                        {
                            ["old"] = Plain(property.OriginalValue),
                            ["new"] = Plain(property.CurrentValue),
                        };
                    }

                    if (changed.Count > 0)
                    {
                        pending.Add(new PendingEdit(entry, EditOperation.Update)
                        {
                            Key = KeyOf(entry.Metadata, p => entry.Property(p.Name).OriginalValue),
                            Root = RootOf(context, entry.Metadata, p => entry.Property(p.Name).OriginalValue),
                            Changes = changed,
                        });
                    }

                    break;

                case EntityState.Deleted:
                    pending.Add(new PendingEdit(entry, EditOperation.Delete)
                    {
                        Key = KeyOf(entry.Metadata, p => entry.Property(p.Name).OriginalValue),
                        Root = RootOf(context, entry.Metadata, p => entry.Property(p.Name).OriginalValue),
                        Changes = Snapshot(entry.Metadata, p => entry.Property(p.Name).OriginalValue, "old"),
                    });
                    break;
            }
        }

        return pending;
    }

    /// <summary>The log rows for what <see cref="Capture"/> saw, now the save has run.</summary>
    public static List<EditLog> Materialize(DbContext context, IReadOnlyList<PendingEdit> pending, Guid? userId, string? userName, DateTimeOffset now)
    {
        List<EditLog> rows = new(pending.Count);
        string? requestId = RequestId();

        foreach (PendingEdit edit in pending)
        {
            IEntityType type = edit.Entry.Metadata;

            if (edit.Operation == EditOperation.Insert)
            {
                Func<IProperty, object?> current = p => edit.Entry.Property(p.Name).CurrentValue;
                edit.Key = KeyOf(type, current);
                edit.Root = RootOf(context, type, current);
                edit.Changes = Snapshot(type, current, "new");
            }

            rows.Add(Row(type, edit.Operation, edit.Key!, edit.Root!.Value, edit.Changes!,
                userId ?? ActorOf(edit.Entry), userName, now, requestId, "Tracked"));
        }

        return rows;
    }

    /// <summary>A log row built from values read directly, for a bulk statement.</summary>
    public static EditLog? ForValues(
        DbContext context,
        IEntityType type,
        EditOperation operation,
        Func<IProperty, object?>? before,
        Func<IProperty, object?>? after,
        Guid? userId,
        string? userName,
        DateTimeOffset now,
        string source)
    {
        Func<IProperty, object?> identity = before ?? after!;
        Dictionary<string, Dictionary<string, object?>> changes = operation switch
        {
            EditOperation.Insert => Snapshot(type, after!, "new"),
            EditOperation.Delete => Snapshot(type, before!, "old"),
            _ => Diff(type, before!, after!),
        };

        if (changes.Count == 0)
        {
            return null;
        }

        return Row(type, operation, KeyOf(type, identity), RootOf(context, type, identity), changes,
            userId, userName, now, RequestId(), source);
    }

    private static EditLog Row(
        IEntityType type,
        EditOperation operation,
        string key,
        (string Name, string Key) root,
        Dictionary<string, Dictionary<string, object?>> changes,
        Guid? userId,
        string? userName,
        DateTimeOffset now,
        string? requestId,
        string source) =>
        new()
        {
            EntityName = Clip(type.GetTableName()!, 100),
            EntityKey = Clip(key, 200),
            RootEntityName = Clip(root.Name, 100),
            RootEntityKey = Clip(root.Key, 200),
            Operation = operation,
            Changes = JsonSerializer.SerializeToDocument(changes, Json),
            UserId = userId,
            UserName = userName is null ? null : Clip(userName, 200),
            ChangedAt = now,
            RequestId = requestId,
            Source = Clip(source, 40),
        };

    private static bool Logged(IProperty property) =>
        !Carried.Contains(property.Name)
        && property.PropertyInfo?.IsDefined(typeof(NotAuditedAttribute), inherit: true) != true;

    private static Dictionary<string, Dictionary<string, object?>> Snapshot(
        IEntityType type, Func<IProperty, object?> value, string side)
    {
        Dictionary<string, Dictionary<string, object?>> values = [];
        foreach (IProperty property in type.GetProperties().Where(Logged))
        {
            object? v = value(property);
            if (v is not null)
            {
                values[property.Name] = new() { [side] = Plain(v) };
            }
        }

        return values;
    }

    private static Dictionary<string, Dictionary<string, object?>> Diff(
        IEntityType type, Func<IProperty, object?> before, Func<IProperty, object?> after)
    {
        Dictionary<string, Dictionary<string, object?>> changed = [];
        foreach (IProperty property in type.GetProperties().Where(Logged))
        {
            object? old = before(property);
            object? now = after(property);
            if (!ValuesEqual(old, now))
            {
                changed[property.Name] = new() { ["old"] = Plain(old), ["new"] = Plain(now) };
            }
        }

        return changed;
    }

    /// <summary>
    /// The document or master a row belongs to. A line points at its header
    /// through a required foreign key whose principal holds the lines as a
    /// collection; the climb continues while the header is itself tracked, so a
    /// bill line's tax row lands on the bill. A row with no such parent is its
    /// own root.
    /// </summary>
    private static (string Name, string Key) RootOf(DbContext context, IEntityType type, Func<IProperty, object?> value, int depth = 0)
    {
        foreach (IForeignKey fk in type.GetForeignKeys())
        {
            if (fk.PrincipalToDependent is not { IsCollection: true }
                || !fk.IsRequired
                || !fk.PrincipalKey.IsPrimaryKey()
                || fk.PrincipalEntityType == type)
            {
                continue;
            }

            object?[] keys = [.. fk.Properties.Select(value)];
            if (keys.Any(k => k is null))
            {
                continue;
            }

            IEntityType principal = fk.PrincipalEntityType;

            if (depth < 3 && Tracked(context, principal, keys) is { } parent)
            {
                return RootOf(context, principal, p => parent.Property(p.Name).CurrentValue, depth + 1);
            }

            return (principal.GetTableName() ?? principal.ClrType.Name, Join(keys));
        }

        return (type.GetTableName() ?? type.ClrType.Name, KeyOf(type, value));
    }

    private static EntityEntry? Tracked(DbContext context, IEntityType type, object?[] keys)
    {
        IReadOnlyList<IProperty> keyProperties = type.FindPrimaryKey()!.Properties;
        return context.ChangeTracker.Entries().FirstOrDefault(e =>
            e.Metadata == type
            && keyProperties.Select((p, i) => Equals(e.Property(p.Name).CurrentValue, keys[i])).All(x => x));
    }

    private static string KeyOf(IEntityType type, Func<IProperty, object?> value) =>
        Join([.. type.FindPrimaryKey()!.Properties.Select(value)]);

    private static string Join(object?[] parts) =>
        string.Join("|", parts.Select(p => Convert.ToString(p, CultureInfo.InvariantCulture)));

    private static Guid? ActorOf(EntityEntry entry) => entry.Entity switch
    {
        AuditableEntity audited when entry.State != EntityState.Deleted => audited.ModifiedBy ?? audited.CreatedBy,
        _ => null,
    };

    private static string? RequestId() => Activity.Current?.TraceId.ToString();

    internal static bool ValuesEqual(object? a, object? b) => (a, b) switch
    {
        (null, null) => true,
        (null, _) or (_, null) => false,
        (byte[] x, byte[] y) => x.AsSpan().SequenceEqual(y),
        (JsonDocument x, JsonDocument y) => x.RootElement.GetRawText() == y.RootElement.GetRawText(),
        _ => a.Equals(b),
    };

    /// <summary>A value the log can hold: a JSON document as its element, everything else as it is.</summary>
    private static object? Plain(object? value) => value switch
    {
        JsonDocument document => document.RootElement.Clone(),
        _ => value,
    };

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max];
}
