namespace Accounting.Entity.Enums;

/// <summary>What a savings scheme adds at maturity (TK-141).</summary>
public enum SchemeBonusType
{
    None = 0,

    /// <summary>A fixed sum, whatever the instalment.</summary>
    FixedAmount = 1,

    /// <summary>A number of instalments — "pay 11, the 12th is on us".</summary>
    Instalments = 2,

    /// <summary>A percentage of everything the customer pays in.</summary>
    Percent = 3,
}

/// <summary>Where one customer's enrolment stands (TK-141).</summary>
public enum EnrolmentStatus
{
    /// <summary>Instalments still to pay.</summary>
    Active = 1,

    /// <summary>Every instalment paid: redeemable with its bonus.</summary>
    Matured = 2,

    /// <summary>Spent against an invoice. Final.</summary>
    Redeemed = 3,

    /// <summary>An instalment went unpaid past the grace days. No bonus; what was paid is redeemable or refundable.</summary>
    Lapsed = 4,

    /// <summary>What was paid went back to the customer. Final.</summary>
    Refunded = 5,
}
