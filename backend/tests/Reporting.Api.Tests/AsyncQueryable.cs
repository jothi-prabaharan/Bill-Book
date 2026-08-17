using System.Collections;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace Reporting.Api.Tests;

/// <summary>
/// An in-memory <see cref="IQueryable{T}"/> that EF Core's async operators accept.
///
/// <b>Why this is needed at all.</b> <c>ToListAsync</c>, <c>CountAsync</c> and
/// <c>SumAsync</c> refuse any provider that does not implement
/// <c>IAsyncQueryProvider</c> — they are EF extension methods, not LINQ ones. The
/// engine uses them because it queries a database in production; a plain
/// <c>List.AsQueryable()</c> therefore throws rather than answering, and the test
/// would be testing the harness.
///
/// <b>What it does not prove.</b> That the expressions translate to SQL. LINQ to
/// Objects will happily evaluate things Postgres cannot, so a test passing here is
/// evidence about composition and shape, not about translation. Translation is
/// established by running the reports against a real server.
/// </summary>
public static class AsyncQueryable
{
    public static IQueryable<T> Of<T>(IEnumerable<T> items) => new AsyncEnumerableQuery<T>(items);
}

internal sealed class AsyncEnumerableQuery<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public AsyncEnumerableQuery(IEnumerable<T> enumerable)
        : base(enumerable)
    {
    }

    public AsyncEnumerableQuery(Expression expression)
        : base(expression)
    {
    }

    IQueryProvider IQueryable.Provider => new AsyncQueryProvider<T>(this);

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default)
    {
        foreach (T item in this)
        {
            ct.ThrowIfCancellationRequested();
            yield return item;
            await Task.CompletedTask;
        }
    }
}

internal sealed class AsyncQueryProvider<TEntity> : IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;

    internal AsyncQueryProvider(IQueryProvider inner) => _inner = inner;

    public IQueryable CreateQuery(Expression expression) =>
        new AsyncEnumerableQuery<TEntity>(expression);

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
        new AsyncEnumerableQuery<TElement>(expression);

    public object? Execute(Expression expression) => _inner.Execute(expression);

    public TResult Execute<TResult>(Expression expression) => _inner.Execute<TResult>(expression);

    /// <summary>
    /// Unwraps the <c>Task&lt;T&gt;</c> EF's async operators expect. They call this
    /// with <c>TResult</c> already being the task type, so the result is wrapped
    /// rather than awaited.
    /// </summary>
    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken ct = default)
    {
        Type resultType = typeof(TResult).GetGenericArguments()[0];

        object? result = typeof(IQueryProvider)
            .GetMethods()
            .Single(m => m.Name == nameof(IQueryProvider.Execute) && m.IsGenericMethod)
            .MakeGenericMethod(resultType)
            .Invoke(_inner, [expression]);

        return (TResult)typeof(Task)
            .GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(resultType)
            .Invoke(null, [result])!;
    }
}

internal sealed class AsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _inner;

    public AsyncEnumerator(IEnumerator<T> inner) => _inner = inner;

    public T Current => _inner.Current;

    public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(_inner.MoveNext());

    public ValueTask DisposeAsync()
    {
        _inner.Dispose();
        return ValueTask.CompletedTask;
    }
}
