using System.Reflection;
using System.Globalization;
using System.Linq.Expressions;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;

namespace Reporting.Api.Services;

/// <summary>
/// Turns a request into <c>Where</c>, <c>OrderBy</c> and <c>Skip</c>/<c>Take</c>
/// over a report's <see cref="IQueryable{T}"/>.
///
/// <b>Everything here is expression trees, and nothing here is SQL.</b> Hard rule
/// 1 forbids raw SQL, and the security argument runs the same way: a filter names
/// a column key, the key selects an expression the report itself declared, and
/// that expression is composed into the query. There is no path by which caller
/// text reaches the database as anything but a parameter value.
///
/// One report gets every filter, sort and page the grid offers without writing any
/// of it — which is the whole reason forty-five reports cost one engine.
/// </summary>
public static class ReportQueryBuilder<TRow>
    where TRow : class
{
    private static readonly MethodInfo StringContains =
        typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

    private static readonly MethodInfo StringStartsWith =
        typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!;

    private static readonly MethodInfo StringEndsWith =
        typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string)])!;

    /// <summary>
    /// Applies every filter, ANDed. There is no OR and no bracketing: the screens
    /// this replaces had neither, and a filter language nobody asked for is a
    /// parser nobody maintains.
    /// </summary>
    public static IQueryable<TRow> ApplyFilters(
        IQueryable<TRow> query,
        IReadOnlyList<ReportFilterModel> filters,
        IReadOnlyDictionary<string, ReportColumn> columns)
    {
        foreach (ReportFilterModel filter in filters)
        {
            ReportColumn column = Resolve(filter.Column, columns, "filtered on");

            if (!column.IsFilterable)
            {
                throw new ReportQueryException($"Column '{filter.Column}' cannot be filtered on.");
            }

            query = query.Where(PredicateFor(column, filter));
        }

        return query;
    }

    /// <summary>
    /// Applies the sort keys in the order given — the first is the primary key,
    /// the rest are <c>ThenBy</c>.
    ///
    /// <b>An unsorted page is a meaningless page.</b> Postgres does not promise an
    /// order without <c>ORDER BY</c>, so paging an unordered query can show the
    /// same row twice and never show another. When a request names no sort the
    /// caller supplies a fallback, and it must be unique enough to be total.
    /// </summary>
    public static IQueryable<TRow> ApplySorts(
        IQueryable<TRow> query,
        IReadOnlyList<ReportSortModel> sorts,
        IReadOnlyDictionary<string, ReportColumn> columns,
        LambdaExpression fallback)
    {
        if (sorts.Count == 0)
        {
            return OrderBy(query, fallback, SortDirection.Asc, first: true);
        }

        bool first = true;

        foreach (ReportSortModel sort in sorts)
        {
            ReportColumn column = Resolve(sort.Column, columns, "sorted by");

            if (!column.IsSortable)
            {
                throw new ReportQueryException($"Column '{sort.Column}' cannot be sorted by.");
            }

            query = OrderBy(query, column.Selector, sort.Direction, first);
            first = false;
        }

        // The tie-break. Without it two rows equal on every sort key may swap
        // between one page and the next, which reads as a row going missing.
        return OrderBy(query, fallback, SortDirection.Asc, first: false);
    }

    public static IQueryable<TRow> Page(IQueryable<TRow> query, ReportPageModel page) =>
        query.Skip((page.Number - 1) * page.Size).Take(page.Size);

    /// <summary>
    /// A <c>Sum</c> over one column of the filtered set, as a decimal.
    ///
    /// Returns null rather than zero when the column holds no numbers — an empty
    /// result and a result totalling zero are different facts, and a report that
    /// prints 0.00 for "nothing here" invites somebody to reconcile against it.
    /// </summary>
    public static IQueryable<decimal?> SelectDecimal(IQueryable<TRow> query, ReportColumn column) =>
        query.Select(DecimalSelector(column));

    /// <summary>A column as a nullable decimal, for summing and averaging.</summary>
    public static Expression<Func<TRow, decimal?>> DecimalSelector(ReportColumn column)
    {
        ParameterExpression parameter = Expression.Parameter(typeof(TRow), "e");
        Expression member = Rebind(column.Selector, parameter);

        Expression asDecimal = column.ValueType == typeof(decimal)
            ? Expression.Convert(member, typeof(decimal?))
            : Expression.Convert(Expression.Convert(member, typeof(decimal)), typeof(decimal?));

        return Expression.Lambda<Func<TRow, decimal?>>(asDecimal, parameter);
    }

    /// <summary>The separator between group levels inside a composite key.</summary>
    public const char GroupSeparator = '';

    /// <summary>
    /// A key selector over the group columns, as <b>one concatenated string</b>.
    ///
    /// One string rather than a key object, because that is the shape Postgres
    /// groups on without argument: <c>a || b</c> translates, a member-init key is
    /// at the mercy of the provider, and a key whose type varies per column drags
    /// reflection through every call site. The levels are split apart again when
    /// the footers are assembled.
    ///
    /// The separator is the ASCII unit separator — a character no ledger
    /// description, account name or contact ever contains, so a value cannot forge
    /// a group boundary.
    /// </summary>
    public static Expression<Func<TRow, string>> GroupKeySelector(
        IReadOnlyList<string> groupBy, IReadOnlyDictionary<string, ReportColumn> columns)
    {
        ParameterExpression parameter = Expression.Parameter(typeof(TRow), "e");
        Expression? key = null;

        foreach (string columnKey in groupBy)
        {
            ReportColumn column = Resolve(columnKey, columns, "grouped by");

            if (!column.IsGroupable)
            {
                throw new ReportQueryException($"Column '{columnKey}' cannot be grouped by.");
            }

            // Null and empty group alike. A group header reading "(none)" is the
            // presentation layer's business, not the query's.
            Expression member = Expression.Coalesce(
                Rebind(column.Selector, parameter), Expression.Constant(string.Empty));

            key = key is null
                ? member
                : Expression.Call(
                    ConcatThree,
                    key,
                    Expression.Constant(GroupSeparator.ToString()),
                    member);
        }

        if (key is null)
        {
            throw new ReportQueryException("Grouping needs at least one column.");
        }

        return Expression.Lambda<Func<TRow, string>>(key, parameter);
    }

    private static readonly MethodInfo ConcatThree =
        typeof(string).GetMethod(
            nameof(string.Concat), [typeof(string), typeof(string), typeof(string)])!;

    private static ReportColumn Resolve(
        string key, IReadOnlyDictionary<string, ReportColumn> columns, string verb)
    {
        // The check that makes the whole thing safe. A key the report does not
        // declare never reaches the database — and it is an error rather than a
        // silently dropped clause, because a filter that vanishes returns more
        // rows than were asked for and looks exactly like a correct answer.
        if (!columns.TryGetValue(key, out ReportColumn? column))
        {
            throw new ReportQueryException(
                $"This report has no column '{key}', so it cannot be {verb}.");
        }

        return column;
    }

    private static Expression<Func<TRow, bool>> PredicateFor(
        ReportColumn column, ReportFilterModel filter)
    {
        ParameterExpression parameter = Expression.Parameter(typeof(TRow), "e");
        Expression member = Rebind(column.Selector, parameter);

        Expression body = filter.Operator switch
        {
            FilterOperator.IsNull => Expression.Equal(member, NullFor(member.Type)),
            FilterOperator.IsNotNull => Expression.NotEqual(member, NullFor(member.Type)),

            FilterOperator.Contains => StringCall(member, StringContains, filter.Value),
            FilterOperator.NotContains =>
                Expression.Not(StringCall(member, StringContains, filter.Value)),
            FilterOperator.StartsWith => StringCall(member, StringStartsWith, filter.Value),
            FilterOperator.EndsWith => StringCall(member, StringEndsWith, filter.Value),

            FilterOperator.Between => Expression.AndAlso(
                Compare(member, column, filter.Value, Expression.GreaterThanOrEqual),
                Compare(member, column, filter.Value2, Expression.LessThanOrEqual)),

            FilterOperator.In => InList(member, column, filter.Values),
            FilterOperator.NotIn => Expression.Not(InList(member, column, filter.Values)),

            FilterOperator.Equals => Compare(member, column, filter.Value, Expression.Equal),
            FilterOperator.NotEquals => Compare(member, column, filter.Value, Expression.NotEqual),
            FilterOperator.GreaterThan =>
                Compare(member, column, filter.Value, Expression.GreaterThan),
            FilterOperator.GreaterOrEqual =>
                Compare(member, column, filter.Value, Expression.GreaterThanOrEqual),
            FilterOperator.LessThan => Compare(member, column, filter.Value, Expression.LessThan),
            FilterOperator.LessOrEqual =>
                Compare(member, column, filter.Value, Expression.LessThanOrEqual),

            _ => throw new ReportQueryException(
                $"Filter operator '{filter.Operator}' is not supported."),
        };

        return Expression.Lambda<Func<TRow, bool>>(body, parameter);
    }

    private static Expression StringCall(Expression member, MethodInfo method, string? value)
    {
        if (member.Type != typeof(string))
        {
            throw new ReportQueryException(
                "Contains, starts-with and ends-with apply to text columns only.");
        }

        if (string.IsNullOrEmpty(value))
        {
            throw new ReportQueryException("This filter needs a value.");
        }

        // Null-guarded: in SQL a null never matches, but the same expression is
        // run against in-memory sequences by the tests, where it would throw.
        return Expression.AndAlso(
            Expression.NotEqual(member, Expression.Constant(null, typeof(string))),
            Expression.Call(member, method, Expression.Constant(value, typeof(string))));
    }

    private static Expression Compare(
        Expression member,
        ReportColumn column,
        string? raw,
        Func<Expression, Expression, BinaryExpression> compare)
    {
        object? value = Parse(raw, column);
        Expression constant = Expression.Constant(value, member.Type);

        return compare(member, constant);
    }

    private static Expression InList(
        Expression member, ReportColumn column, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            throw new ReportQueryException("An 'in' filter needs at least one value.");
        }

        // Built as a chain of equalities rather than List.Contains so the same
        // expression runs against a database and against an in-memory sequence.
        Expression? body = null;

        foreach (string raw in values)
        {
            Expression equal = Expression.Equal(
                member, Expression.Constant(Parse(raw, column), member.Type));

            body = body is null ? equal : Expression.OrElse(body, equal);
        }

        return body!;
    }

    /// <summary>
    /// Parses a filter's text against the column's declared type. Invariant
    /// culture throughout: the wire format is not the user's locale, and a date
    /// that parses differently on a machine set to en-GB is a bug nobody
    /// reproduces.
    /// </summary>
    private static object? Parse(string? raw, ReportColumn column)
    {
        if (raw is null)
        {
            return null;
        }

        Type target = column.ValueType;

        try
        {
            if (target == typeof(string))
            {
                return raw;
            }

            if (target.IsEnum)
            {
                return Enum.Parse(target, raw, ignoreCase: true);
            }

            if (target == typeof(DateOnly))
            {
                return DateOnly.Parse(raw, CultureInfo.InvariantCulture);
            }

            if (target == typeof(DateTimeOffset))
            {
                return DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture);
            }

            if (target == typeof(Guid))
            {
                return Guid.Parse(raw);
            }

            return Convert.ChangeType(raw, target, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
        {
            throw new ReportQueryException(
                $"'{raw}' is not a valid value for the '{column.Key}' column.");
        }
    }

    private static Expression NullFor(Type type) =>
        type.IsValueType && Nullable.GetUnderlyingType(type) is null
            ? throw new ReportQueryException("This column is never empty, so it cannot be filtered as empty.")
            : Expression.Constant(null, type);

    private static IQueryable<TRow> OrderBy(
        IQueryable<TRow> query, LambdaExpression selector, SortDirection direction, bool first)
    {
        ParameterExpression parameter = Expression.Parameter(typeof(TRow), "e");
        Expression member = Rebind(selector, parameter);
        LambdaExpression keySelector = Expression.Lambda(member, parameter);

        string method = (first, direction) switch
        {
            (true, SortDirection.Asc) => nameof(Queryable.OrderBy),
            (true, _) => nameof(Queryable.OrderByDescending),
            (false, SortDirection.Asc) => nameof(Queryable.ThenBy),
            _ => nameof(Queryable.ThenByDescending),
        };

        // The one place reflection is unavoidable: the key type varies per column,
        // and Queryable's ordering methods are generic in it.
        MethodCallExpression call = Expression.Call(
            typeof(Queryable),
            method,
            [typeof(TRow), member.Type],
            query.Expression,
            Expression.Quote(keySelector));

        return (IQueryable<TRow>)query.Provider.CreateQuery<TRow>(call);
    }

    /// <summary>
    /// Re-points a selector written against its own parameter at ours, so
    /// expressions declared separately compose into one query.
    /// </summary>
    private static Expression Rebind(LambdaExpression selector, ParameterExpression parameter) =>
        new ParameterReplacer(selector.Parameters[0], parameter).Visit(selector.Body)!;

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to)
        : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }
}

/// <summary>
/// A request the engine refused. Carries a message meant for the person who asked
/// rather than for a log, because every one of these is something they can fix:
/// a column that does not exist, a value that will not parse, a filter on a column
/// that does not accept one.
/// </summary>
public sealed class ReportQueryException(string message) : Exception(message);
