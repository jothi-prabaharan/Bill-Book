namespace Reporting.Entity.Enums;

/// <summary>A budget's status, as Accounting stores it: by name, so these names must stay Accounting's (TK-204).</summary>
public enum BudgetStatus
{
    /// <summary>Being entered; its lines may still change.</summary>
    Draft = 1,

    /// <summary>Locked; a change is a revision, copied into a new draft.</summary>
    Approved = 2,

    /// <summary>Replaced by a revision.</summary>
    Superseded = 3,
}
