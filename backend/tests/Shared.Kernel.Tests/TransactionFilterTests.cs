using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Kernel.Persistence;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// What the filter commits and what it rolls back.
///
/// The case worth having a test for is the third one: an action that throws
/// nothing and returns <c>Conflict()</c> after its service has already written
/// two of three rows. Judging success by "no exception" keeps those two rows,
/// which is how a refused posting leaves half a document behind.
/// </summary>
public class TransactionFilterTests
{
    [Fact]
    public async Task A_successful_write_is_committed()
    {
        (FakeUnitOfWork unit, _) = await RunAsync("POST", () => new OkObjectResult(new { }));

        Assert.True(unit.Committed);
        Assert.False(unit.RolledBack);
        Assert.True(unit.Disposed);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(422)]
    [InlineData(500)]
    public async Task A_refusal_status_is_rolled_back_even_though_nothing_threw(int status)
    {
        (FakeUnitOfWork unit, _) = await RunAsync(
            "POST", () => new ObjectResult(new { }) { StatusCode = status });

        Assert.False(unit.Committed);
        Assert.True(unit.RolledBack);
    }

    [Fact]
    public async Task Forbid_is_rolled_back_even_though_it_carries_no_status_yet()
    {
        // ForbidResult has no StatusCode until it executes, which is after this
        // filter has had to decide. Checked by type for exactly that reason.
        (FakeUnitOfWork unit, _) = await RunAsync("POST", () => new ForbidResult());

        Assert.False(unit.Committed);
        Assert.True(unit.RolledBack);
    }

    [Fact]
    public async Task An_exception_rolls_back_and_is_left_for_the_error_handler()
    {
        var unit = new FakeUnitOfWork();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunAsync("POST", () => throw new InvalidOperationException("boom"), unit));

        Assert.False(unit.Committed);
        Assert.True(unit.RolledBack);
        Assert.True(unit.Disposed);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task A_read_never_opens_a_transaction(string method)
    {
        (FakeUnitOfWork unit, _) = await RunAsync(method, () => new OkObjectResult(new { }));

        Assert.False(unit.Begun);
        Assert.False(unit.Committed);
        Assert.False(unit.RolledBack);
    }

    [Fact]
    public async Task An_action_marked_NoTransaction_opens_none()
    {
        (FakeUnitOfWork unit, _) = await RunAsync(
            "POST", () => new OkObjectResult(new { }), metadata: new NoTransactionAttribute());

        Assert.False(unit.Begun);
    }

    [Fact]
    public async Task The_declared_isolation_level_reaches_the_unit_of_work()
    {
        (FakeUnitOfWork unit, _) = await RunAsync(
            "POST",
            () => new OkObjectResult(new { }),
            metadata: new TransactionalAttribute(IsolationLevel.Serializable));

        Assert.Equal(IsolationLevel.Serializable, unit.Level);
    }

    [Fact]
    public async Task Read_committed_is_the_default_and_is_left_to_the_provider()
    {
        (FakeUnitOfWork unit, _) = await RunAsync("POST", () => new OkObjectResult(new { }));

        Assert.Equal(IsolationLevel.Unspecified, unit.Level);
    }

    [Fact]
    public async Task Every_unit_of_work_is_settled_when_a_service_owns_two_contexts()
    {
        var first = new FakeUnitOfWork();
        var second = new FakeUnitOfWork();

        await RunAsync("POST", () => new OkObjectResult(new { }), first, second);

        Assert.True(first.Committed);
        Assert.True(second.Committed);
    }

    // ---- harness -----------------------------------------------------------

    private static async Task<(FakeUnitOfWork Unit, ActionExecutedContext Executed)> RunAsync(
        string method,
        Func<IActionResult> action,
        params FakeUnitOfWork[] units) =>
        await RunAsync(method, action, metadata: null, units);

    private static Task<(FakeUnitOfWork, ActionExecutedContext)> RunAsync(
        string method,
        Func<IActionResult> action,
        FakeUnitOfWork unit) =>
        RunAsync(method, action, metadata: null, [unit]);

    private static async Task<(FakeUnitOfWork, ActionExecutedContext)> RunAsync(
        string method,
        Func<IActionResult> action,
        object? metadata = null,
        FakeUnitOfWork[]? units = null)
    {
        units ??= [new FakeUnitOfWork()];

        var descriptor = new ControllerActionDescriptor
        {
            DisplayName = "Test.Action",
            EndpointMetadata = metadata is null ? [] : [metadata],
        };

        var httpContext = new DefaultHttpContext
        {
            RequestServices = new UnitOfWorkProvider(units),
        };
        httpContext.Request.Method = method;

        var actionContext = new ActionContext(httpContext, new RouteData(), descriptor);

        var executing = new ActionExecutingContext(
            actionContext, [], new Dictionary<string, object?>(), controller: null!);

        var executed = new ActionExecutedContext(actionContext, [], controller: null!);

        var filter = new TransactionFilter(NullLogger<TransactionFilter>.Instance);

        await filter.OnActionExecutionAsync(executing, () =>
        {
            try
            {
                executed.Result = action();
            }
            catch (Exception ex)
            {
                executed.Exception = ex;
            }

            return Task.FromResult(executed);
        });

        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            // The filter re-raises nothing itself — it lets the exception travel
            // on to the handler. Reproduced here so the test can assert it.
            throw executed.Exception;
        }

        return (units[0], executed);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public bool Begun { get; private set; }

        public bool Committed { get; private set; }

        public bool RolledBack { get; private set; }

        public bool Disposed { get; private set; }

        public IsolationLevel Level { get; private set; } = (IsolationLevel)(-1);

        public string Name => nameof(FakeUnitOfWork);

        public Task BeginAsync(IsolationLevel isolationLevel, CancellationToken ct)
        {
            Begun = true;
            Level = isolationLevel;
            return Task.CompletedTask;
        }

        public Task CommitAsync(CancellationToken ct)
        {
            Committed = true;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken ct)
        {
            RolledBack = true;
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            Disposed = true;
            return Task.CompletedTask;
        }
    }

    private sealed class UnitOfWorkProvider : IServiceProvider
    {
        private readonly IEnumerable<IUnitOfWork> _units;

        public UnitOfWorkProvider(IEnumerable<IUnitOfWork> units) => _units = units;

        public object? GetService(Type serviceType) =>
            serviceType == typeof(IEnumerable<IUnitOfWork>) ? _units : null;
    }
}
