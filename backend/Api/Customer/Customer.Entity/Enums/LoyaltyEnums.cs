namespace Customer.Entity.Enums;

/// <summary>What moved a contact's loyalty points (TK-166).</summary>
public enum LoyaltyTransactionKind
{
    /// <summary>A posted bill earned them.</summary>
    Earned = 1,

    /// <summary>The bill that earned them was voided, and they were taken back.</summary>
    Reversed = 2,

    /// <summary>Spent against a bill. Not built: how it posts waits on D-52.</summary>
    Redeemed = 3,

    /// <summary>Corrected by hand.</summary>
    Adjusted = 4,
}

/// <summary>The document behind a loyalty transaction (TK-166).</summary>
public enum LoyaltySource
{
    /// <summary>A sales invoice; a restaurant bill posts as one.</summary>
    Invoice = 1,
}
