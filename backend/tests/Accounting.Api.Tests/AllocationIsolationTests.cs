using System.Data;
using System.Reflection;
using Accounting.Api.Controllers;
using Shared.Kernel.Persistence;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// Both allocation routes declare Serializable where the transaction filter can
/// see it (TK-08).
///
/// <c>AllocationService.AllocateAsync</c> asks <c>BeginScopeAsync</c> for
/// Serializable. Under the host, the reliability filter has already opened the
/// request's transaction by then, at Read Committed unless the action says
/// otherwise — and Postgres cannot raise the level of a transaction that has
/// run a statement, so the scope refused every allocation. Tests that call the
/// controller directly have no filter, which is how it went unnoticed.
/// </summary>
public sealed class AllocationIsolationTests
{
    [Theory]
    [InlineData(typeof(AllocationsController))]
    [InlineData(typeof(InternalAllocationsController))]
    public void The_allocate_action_is_serializable(Type controller)
    {
        MethodInfo allocate = controller.GetMethod("Allocate")!;

        TransactionalAttribute? attribute = allocate.GetCustomAttribute<TransactionalAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(IsolationLevel.Serializable, attribute.IsolationLevel);
    }
}
