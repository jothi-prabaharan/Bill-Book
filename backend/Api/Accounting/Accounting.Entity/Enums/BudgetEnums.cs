namespace Accounting.Entity.Enums;

/// <summary>Where a budget is in its life (TK-108, design "Budgeting").</summary>
public enum BudgetStatus
{
    /// <summary>Being entered. Lines may change.</summary>
    Draft = 1,

    /// <summary>Locked. Changing it is a revision, which copies it into a new draft.</summary>
    Approved = 2,

    /// <summary>An approved budget a later revision replaced. Kept as history.</summary>
    Superseded = 3,
}

/// <summary>What a new budget starts from.</summary>
public enum BudgetSource
{
    Blank = 1,

    /// <summary>Last financial year's profit-and-loss actuals from the ledger, adjusted by a percentage.</summary>
    LastYearActuals = 2,

    /// <summary>A copy of another budget's lines.</summary>
    OtherBudget = 3,
}
