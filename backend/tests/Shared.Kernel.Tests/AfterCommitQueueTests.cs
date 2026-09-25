using Microsoft.Extensions.Logging.Abstractions;
using Shared.Kernel.Persistence;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>Work queued to run once a request's transaction has committed (TK-92).</summary>
public sealed class AfterCommitQueueTests
{
    [Fact]
    public async Task Work_runs_in_order_and_the_queue_empties()
    {
        var queue = new AfterCommitQueue();
        var ran = new List<int>();
        queue.Enqueue(_ => { ran.Add(1); return Task.CompletedTask; });
        queue.Enqueue(_ => { ran.Add(2); return Task.CompletedTask; });

        await queue.RunAsync(NullLogger.Instance, default);

        Assert.Equal([1, 2], ran);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public async Task One_failure_does_not_stop_the_rest_or_reach_the_caller()
    {
        var queue = new AfterCommitQueue();
        bool second = false;
        queue.Enqueue(_ => throw new HttpRequestException("The IRP is down."));
        queue.Enqueue(_ => { second = true; return Task.CompletedTask; });

        await queue.RunAsync(NullLogger.Instance, default);

        Assert.True(second);
    }

    [Fact]
    public async Task Cleared_work_never_runs()
    {
        var queue = new AfterCommitQueue();
        bool ran = false;
        queue.Enqueue(_ => { ran = true; return Task.CompletedTask; });

        queue.Clear();
        await queue.RunAsync(NullLogger.Instance, default);

        Assert.False(ran);
    }
}
