namespace Purchase.Entity.Enums;

/// <summary>What reconciling one supplier invoice against the books found (TK-116).</summary>
public enum Gstr2bMatchState
{
    /// <summary>In 2B and in the books, and they agree within a rupee.</summary>
    Matched = 1,

    /// <summary>In both, but the taxable value, the tax or the date differs.</summary>
    Mismatched = 2,

    /// <summary>The supplier reported it and no bill records it.</summary>
    MissingInBooks = 3,

    /// <summary>A bill records it and the supplier has not reported it: the credit at risk.</summary>
    MissingIn2b = 4,
}
