using Customer.Entity.TableEntities;
using Shared.Kernel.Customer;

namespace Customer.Repository.SeedData;

/// <summary>
/// The SLA every branch starts with (TK-18). The resolution hours are the ones
/// the ticket controller hard-coded before the table existed — Urgent 2 hours,
/// High 8, Medium 2 days, Low 7 days — so a branch sees no change until it
/// edits its own.
/// </summary>
public static class SlaPolicySeed
{
    public static readonly IReadOnlyList<(TicketPriority Priority, int ResponseHours, int ResolutionHours)> Defaults =
    [
        (TicketPriority.Urgent, 1, 2),
        (TicketPriority.High, 4, 8),
        (TicketPriority.Medium, 8, 48),
        (TicketPriority.Low, 24, 168),
    ];

    public static List<SlaPolicy> Build(Guid orgId) =>
        [.. Defaults.Select(d => new SlaPolicy
        {
            OrgId = orgId,
            Priority = d.Priority,
            ResponseHours = d.ResponseHours,
            ResolutionHours = d.ResolutionHours,
        })];
}
