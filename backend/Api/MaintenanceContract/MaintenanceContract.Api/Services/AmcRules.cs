using MaintenanceContract.Entity.Enums;

namespace MaintenanceContract.Api.Services;

/// <summary>A contract's rules (S8, TK-68). Pure, so they are tested without a database.</summary>
public static class AmcRules
{
    /// <summary>The status a contract shows on <paramref name="today"/>: an Active one past its end date is Expired.</summary>
    public static ContractStatus Effective(ContractStatus stored, DateOnly endDate, DateOnly today) =>
        stored == ContractStatus.Active && endDate < today ? ContractStatus.Expired : stored;

    /// <summary>Whether a contract covers its assets on <paramref name="today"/>.</summary>
    public static bool IsInForce(ContractStatus stored, DateOnly startDate, DateOnly endDate, DateOnly today) =>
        stored == ContractStatus.Active && startDate <= today && today <= endDate;

    /// <summary>Two contracts' terms overlap when neither ends before the other starts.</summary>
    public static bool Overlaps(DateOnly startA, DateOnly endA, DateOnly startB, DateOnly endB) =>
        startA <= endB && startB <= endA;

    /// <summary>
    /// Whether a renewal reminder is due on <paramref name="today"/>: the contract
    /// is Active, asks for reminders, and today falls in the reminder window
    /// ending on the end date.
    /// </summary>
    public static bool IsRenewalDue(ContractStatus stored, DateOnly endDate, int reminderDays, DateOnly today) =>
        stored == ContractStatus.Active
        && reminderDays > 0
        && today <= endDate
        && endDate.AddDays(-reminderDays) <= today;

    /// <summary>Only a Draft contract changes its terms; an Active one may change only its covered assets, reminder and remarks.</summary>
    public static bool TermsEditable(ContractStatus stored) => stored == ContractStatus.Draft;

    public static bool TakesChanges(ContractStatus stored) => stored is ContractStatus.Draft or ContractStatus.Active;
}
