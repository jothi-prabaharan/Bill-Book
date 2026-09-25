using WorkOrder.Api.Services;
using WorkOrder.Entity.Enums;
using Xunit;

namespace WorkOrder.Api.Tests;

/// <summary>The work order's state table (S6, TK-66). Pure, so it needs no database.</summary>
public sealed class WorkOrderLifecycleTests
{
    [Theory]
    [InlineData(WorkOrderStatus.Open, WorkOrderAction.Assign, WorkOrderStatus.Assigned)]
    [InlineData(WorkOrderStatus.Assigned, WorkOrderAction.Assign, WorkOrderStatus.Assigned)]
    [InlineData(WorkOrderStatus.Assigned, WorkOrderAction.Start, WorkOrderStatus.InProgress)]
    [InlineData(WorkOrderStatus.InProgress, WorkOrderAction.Hold, WorkOrderStatus.OnHold)]
    [InlineData(WorkOrderStatus.OnHold, WorkOrderAction.Resume, WorkOrderStatus.InProgress)]
    [InlineData(WorkOrderStatus.InProgress, WorkOrderAction.Complete, WorkOrderStatus.Completed)]
    [InlineData(WorkOrderStatus.Completed, WorkOrderAction.Close, WorkOrderStatus.Closed)]
    [InlineData(WorkOrderStatus.Open, WorkOrderAction.Cancel, WorkOrderStatus.Cancelled)]
    public void An_allowed_move_lands_where_expected(WorkOrderStatus from, WorkOrderAction action, WorkOrderStatus to) =>
        Assert.Equal(to, WorkOrderLifecycle.Next(from, action));

    [Theory]
    [InlineData(WorkOrderStatus.Open, WorkOrderAction.Complete)]
    [InlineData(WorkOrderStatus.Open, WorkOrderAction.Close)]
    [InlineData(WorkOrderStatus.InProgress, WorkOrderAction.Close)]
    [InlineData(WorkOrderStatus.Closed, WorkOrderAction.Start)]
    [InlineData(WorkOrderStatus.Cancelled, WorkOrderAction.Assign)]
    [InlineData(WorkOrderStatus.Completed, WorkOrderAction.Cancel)]
    public void A_forbidden_move_is_refused_with_a_reason(WorkOrderStatus from, WorkOrderAction action)
    {
        Assert.Null(WorkOrderLifecycle.Next(from, action));
        Assert.False(string.IsNullOrWhiteSpace(WorkOrderLifecycle.Refusal(from, action)));
    }

    [Fact]
    public void Only_an_open_work_order_is_editable() =>
        Assert.Equal([WorkOrderStatus.Open], Enum.GetValues<WorkOrderStatus>().Where(WorkOrderLifecycle.IsEditable));

    [Fact]
    public void Parts_are_issued_only_while_the_work_is_under_way() =>
        Assert.Equal(
            [WorkOrderStatus.Assigned, WorkOrderStatus.InProgress, WorkOrderStatus.OnHold],
            Enum.GetValues<WorkOrderStatus>().Where(WorkOrderLifecycle.TakesParts));
}
