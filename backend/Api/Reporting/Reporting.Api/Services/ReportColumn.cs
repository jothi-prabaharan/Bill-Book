using System.Linq.Expressions;
using Reporting.Entity.Enums;

namespace Reporting.Api.Services;

/// <summary>
/// One column of a report, as its source declares it.
///
/// <b>The <see cref="Selector"/> is the security boundary of the whole engine.</b>
/// A caller sends column <i>keys</i>, never expressions; a key is looked up in the
/// report's own column map and the expression it finds is one the report itself
/// wrote. A key the report does not declare is a 400. That is why an arbitrary
/// filter from a browser can be handed to the database at all — no string from
/// outside ever becomes SQL, it only ever selects between expressions written here.
/// </summary>
public sealed class ReportColumn
{
    private ReportColumn(
        string key,
        ColumnDataType dataType,
        LambdaExpression selector,
        Type valueType,
        AggregateFunction aggregate,
        bool isSortable,
        bool isFilterable,
        bool isGroupable)
    {
        Key = key;
        DataType = dataType;
        Selector = selector;
        ValueType = valueType;
        DefaultAggregate = aggregate;
        IsSortable = isSortable;
        IsFilterable = isFilterable;
        IsGroupable = isGroupable;
    }

    public string Key { get; }

    public ColumnDataType DataType { get; }

    /// <summary><c>Expression&lt;Func&lt;TRow, TValue&gt;&gt;</c>, kept untyped so one map holds them all.</summary>
    public LambdaExpression Selector { get; }

    /// <summary>
    /// The selector's return type, with <c>Nullable&lt;&gt;</c> unwrapped. Filter
    /// values are parsed against this rather than guessed at, so "2026-04-01"
    /// becomes a <see cref="DateOnly"/> for a date column and stays a string for a
    /// text one.
    /// </summary>
    public Type ValueType { get; }

    /// <summary>
    /// What a group footer and the grand total compute. <see cref="AggregateFunction.None"/>
    /// for anything whose total is meaningless — a rate, an account code, a date.
    /// Summing those produces a number that looks like an answer.
    /// </summary>
    public AggregateFunction DefaultAggregate { get; }

    public bool IsSortable { get; }

    public bool IsFilterable { get; }

    public bool IsGroupable { get; }

    /// <summary>
    /// Declares a column. The generic parameters are inferred from the selector,
    /// so a report reads as a list of <c>Column(key, type, e =&gt; e.Something)</c>
    /// rather than as type arguments.
    /// </summary>
    public static ReportColumn Of<TRow, TValue>(
        string key,
        ColumnDataType dataType,
        Expression<Func<TRow, TValue>> selector,
        AggregateFunction aggregate = AggregateFunction.None,
        bool sortable = true,
        bool filterable = true,
        bool groupable = false)
    {
        Type value = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);

        // Groupable columns must be text, and the constraint is worth its
        // inconvenience. Grouping happens in SQL, on a key built by concatenating
        // the group columns; concatenating a date or an enum means asking Postgres
        // to render it, which it does in a format nobody chose and which changes
        // with server settings. A report that wants to group by account type
        // exposes the type's *name* as a column — which is what the reader wanted
        // to see in the group header anyway.
        if (groupable && value != typeof(string))
        {
            throw new InvalidOperationException(
                $"Column '{key}' is marked groupable but is {value.Name}. Grouping keys must be "
                + "text — expose a text column carrying the display value and group by that.");
        }

        return new ReportColumn(
            key, dataType, selector, value, aggregate, sortable, filterable, groupable);
    }

    /// <summary>
    /// Whether this column can carry a number worth totalling. Read by the
    /// execution service rather than assumed from <see cref="ColumnDataType"/>,
    /// because a report may deliberately decline to total a money column — a
    /// running balance being the obvious one, where a total is nonsense.
    /// </summary>
    public bool IsAggregatable => DefaultAggregate != AggregateFunction.None;

    private Func<object, object?>? _getter;

    /// <summary>
    /// Reads this column off an already-materialised row.
    ///
    /// <b>Compiled once and cached.</b> The obvious way to write this is
    /// <c>Selector.Compile().DynamicInvoke(row)</c> at the point of use, which
    /// compiles a fresh delegate for every column of every row — on a 200-row page
    /// of a 30-column report that is 6,000 compilations to render one page, and it
    /// does not show up as a slow query because it is not one.
    /// </summary>
    public object? Read(object row) => (_getter ??= BuildGetter())(row);

    private Func<object, object?> BuildGetter()
    {
        ParameterExpression parameter = Expression.Parameter(typeof(object), "row");
        ParameterExpression original = Selector.Parameters[0];

        Expression body = new Rebinder(original, Expression.Convert(parameter, original.Type))
            .Visit(Selector.Body)!;

        return Expression
            .Lambda<Func<object, object?>>(Expression.Convert(body, typeof(object)), parameter)
            .Compile();
    }

    private sealed class Rebinder(ParameterExpression from, Expression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }
}
