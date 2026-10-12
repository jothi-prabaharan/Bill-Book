namespace Accounting.Entity.Enums;

/// <summary>
/// Which side of the business a create page's option list is for (TK-192). Tax
/// rates, payment terms and accounts each carry a flag per side, and a sales
/// document is offered only what is flagged for sales.
/// </summary>
public enum ChoiceUsage
{
    Sales,
    Purchase,

    /// <summary>Money in and out: accounts flagged usable on payments.</summary>
    Payment,

    /// <summary>No side: everything active.</summary>
    Any,
}
