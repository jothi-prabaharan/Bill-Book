using WorkOrder.Entity.Enums;

namespace WorkOrder.Api.Services;

/// <summary>
/// The work order's lifecycle (S6, TK-66), as one table, with one set of
/// refusal sentences so every screen and every service answers alike — the
/// shape <c>Shared.Kernel.Documents.DocumentLifecycle</c> gives the sales and
/// purchase documents.
/// </summary>
public static class WorkOrderLifecycle
{
    /// <summary>Where an action takes a work order from each status it is allowed in, or null.</summary>
    public static WorkOrderStatus? Next(WorkOrderStatus from, WorkOrderAction action) => (from, action) switch
    {
        (WorkOrderStatus.Open or WorkOrderStatus.Assigned, WorkOrderAction.Assign) => WorkOrderStatus.Assigned,
        (WorkOrderStatus.Assigned, WorkOrderAction.Start) => WorkOrderStatus.InProgress,
        (WorkOrderStatus.InProgress, WorkOrderAction.Hold) => WorkOrderStatus.OnHold,
        (WorkOrderStatus.OnHold, WorkOrderAction.Resume) => WorkOrderStatus.InProgress,
        (WorkOrderStatus.InProgress, WorkOrderAction.Complete) => WorkOrderStatus.Completed,
        (WorkOrderStatus.Completed, WorkOrderAction.Close) => WorkOrderStatus.Closed,
        (WorkOrderStatus.Open or WorkOrderStatus.Assigned, WorkOrderAction.Cancel) => WorkOrderStatus.Cancelled,
        _ => null,
    };

    /// <summary>Only an Open work order's header may change; later, it moves or its tasks are ticked.</summary>
    public static bool IsEditable(WorkOrderStatus status) => status == WorkOrderStatus.Open;

    /// <summary>Parts are issued while the work is under way.</summary>
    public static bool TakesParts(WorkOrderStatus status) =>
        status is WorkOrderStatus.Assigned or WorkOrderStatus.InProgress or WorkOrderStatus.OnHold;

    /// <summary>Tasks are ticked until the work order is finished with.</summary>
    public static bool TakesTicks(WorkOrderStatus status) => status is not (WorkOrderStatus.Closed or WorkOrderStatus.Cancelled);

    public static string Refusal(WorkOrderStatus from, WorkOrderAction action) =>
        $"A work order that is {Words(from)} cannot be {Verb(action)}.";

    public static string NotEditable(WorkOrderStatus status) =>
        $"A work order that is {Words(status)} cannot be edited. Change it through its status, or tick its tasks.";

    public static string Words(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.InProgress => "in progress",
        WorkOrderStatus.OnHold => "on hold",
        _ => status.ToString().ToLowerInvariant(),
    };

    private static string Verb(WorkOrderAction action) => action switch
    {
        WorkOrderAction.Assign => "assigned",
        WorkOrderAction.Start => "started",
        WorkOrderAction.Hold => "put on hold",
        WorkOrderAction.Resume => "resumed",
        WorkOrderAction.Complete => "completed",
        WorkOrderAction.Close => "closed",
        _ => "cancelled",
    };
}
