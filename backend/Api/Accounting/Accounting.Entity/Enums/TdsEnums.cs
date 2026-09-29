namespace Accounting.Entity.Enums;

/// <summary>
/// How a section's aggregate threshold is applied once the year's total passes it (TK-117).
/// </summary>
public enum TdsThresholdBasis
{
    /// <summary>
    /// Everything credited in the year is taxed once the threshold is crossed,
    /// including what was credited before it (194C, 194J and most others): the
    /// document that crosses it catches up on the earlier ones.
    /// </summary>
    WholeAmount = 1,

    /// <summary>Only the amount above the threshold is taxed (194Q, purchase of goods).</summary>
    ExcessOnly = 2,
}

/// <summary>Which side of a deduction the branch is on.</summary>
public enum TdsDirection
{
    /// <summary>The branch deducted it from a vendor and owes it to the government: TDS Payable.</summary>
    Deducted = 1,

    /// <summary>A customer deducted it from the branch: TDS Receivable, claimed against the branch's own tax.</summary>
    Suffered = 2,
}

/// <summary>What gave rise to a register row.</summary>
public enum TdsEntryKind
{
    /// <summary>A vendor's bill credited to their account.</summary>
    Credit = 1,

    /// <summary>A deposit paid to a vendor before their bill: TDS is due on whichever comes first.</summary>
    Advance = 2,

    /// <summary>A customer's payment from which they withheld TDS.</summary>
    Receipt = 3,
}

/// <summary>
/// Which of a section's rates a deductee takes. Read off the fourth character
/// of the PAN: P (individual) and H (HUF) take the individual rate, every other
/// holder the other rate, and no valid PAN the higher section 206AA rate.
/// </summary>
public enum TdsDeducteeKind
{
    IndividualOrHuf = 1,
    Other = 2,
    NoPan = 3,
}

/// <summary>How a Form 26AS row compares with the TDS the books say customers withheld.</summary>
public enum Form26asMatchState
{
    Matched = 1,
    AmountDiffers = 2,

    /// <summary>In 26AS, but no receipt records that deductor withholding it.</summary>
    MissingInBooks = 3,

    /// <summary>A receipt records TDS the deductor has not reported: the credit at risk.</summary>
    MissingIn26as = 4,
}
