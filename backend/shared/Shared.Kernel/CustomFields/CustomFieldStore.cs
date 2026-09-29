using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

namespace Shared.Kernel.CustomFields;

/// <summary>
/// How a record's custom field values move between its <c>CustomFields</c>
/// jsonb column and the API (TK-111): read into a dictionary, written back with
/// empty values left out, and searched by the keys a branch marked searchable.
/// </summary>
public static class CustomFieldStore
{
    /// <summary>The stored values, or an empty dictionary when there are none.</summary>
    public static Dictionary<string, JsonElement> Read(JsonDocument? document)
    {
        Dictionary<string, JsonElement> values = new(StringComparer.Ordinal);
        if (document is null || document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return values;
        }

        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            values[property.Name] = property.Value.Clone();
        }

        return values;
    }

    /// <summary>
    /// One stored value as text, the way Postgres's <c>-&gt;&gt;</c> reads it:
    /// a string as itself, anything else as its JSON, a missing key or record
    /// as null. What a report shows for a custom field column (TK-113), so the
    /// materialised row agrees with the value the database grouped and filtered on.
    /// </summary>
    public static string? Text(JsonDocument? document, string key)
    {
        if (document is null
            || document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty(key, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => value.GetString(),
            _ => value.GetRawText(),
        };
    }

    /// <summary>The values to store: empty ones left out, and null when nothing is left.</summary>
    public static JsonDocument? Write(IReadOnlyDictionary<string, JsonElement>? values)
    {
        if (values is null)
        {
            return null;
        }

        Dictionary<string, JsonElement> kept = values
            .Where(v => !CustomFieldValidator.IsEmpty(v.Value))
            .ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);

        return kept.Count == 0 ? null : JsonSerializer.SerializeToDocument(kept);
    }

    /// <summary>
    /// A filter matching records whose value for any of <paramref name="keys"/>
    /// passes <paramref name="test"/>, the caller's own text match (an Npgsql
    /// service passes <c>v => EF.Functions.ILike(v!, pattern)</c>, which keeps
    /// the provider out of the kernel). Each key becomes
    /// <c>"CustomFields" ->> 'key'</c>. No keys, no match.
    /// </summary>
    public static Expression<Func<T, bool>> Matching<T>(
        Expression<Func<T, JsonDocument?>> column, IReadOnlyCollection<string> keys, Expression<Func<string?, bool>> test)
    {
        ParameterExpression row = column.Parameters[0];
        if (keys.Count == 0)
        {
            return Expression.Lambda<Func<T, bool>>(Expression.Constant(false), row);
        }

        MethodInfo getProperty = typeof(JsonElement).GetMethod(nameof(JsonElement.GetProperty), [typeof(string)])!;
        MethodInfo getString = typeof(JsonElement).GetMethod(nameof(JsonElement.GetString), Type.EmptyTypes)!;
        Expression root = Expression.Property(column.Body, nameof(JsonDocument.RootElement));
        Expression? any = null;

        foreach (string key in keys)
        {
            Expression value = Expression.Call(Expression.Call(root, getProperty, Expression.Constant(key)), getString);
            Expression match = new Swap(test.Parameters[0], value).Visit(test.Body);
            any = any is null ? match : Expression.OrElse(any, match);
        }

        return Expression.Lambda<Func<T, bool>>(any!, row);
    }

    /// <summary>Either filter, as one expression over one row parameter, so a list's usual search and its custom-field search run as a single WHERE.</summary>
    public static Expression<Func<T, bool>> Either<T>(Expression<Func<T, bool>> first, Expression<Func<T, bool>> second)
    {
        Expression body = new Swap(second.Parameters[0], first.Parameters[0]).Visit(second.Body);
        return Expression.Lambda<Func<T, bool>>(Expression.OrElse(first.Body, body), first.Parameters[0]);
    }

    private sealed class Swap(ParameterExpression from, Expression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
