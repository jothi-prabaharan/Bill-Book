namespace Fee.Entity.Enums;

// The fee schema's fixed sets (S4, TK-64), stored by name.

/// <summary>How often a structure line falls due, counted from the school year's first month.</summary>
public enum FeeFrequency
{
    /// <summary>Once, in the year's first month.</summary>
    OneTime = 1,
    Monthly = 2,

    /// <summary>Months 1, 4, 7 and 10 of the year.</summary>
    Quarterly = 3,

    /// <summary>Three terms: months 1, 5 and 9 of the year.</summary>
    Termly = 4,

    /// <summary>Once a year, in its first month. Kept apart from OneTime for reports.</summary>
    Annual = 5,
}

public enum ConcessionKind
{
    Percent = 1,
    Amount = 2,
}

/// <summary>The document lifecycle, as <c>Shared.Kernel.Documents</c> has it: a posted document is never edited.</summary>
public enum FeeDocumentStatus
{
    Draft = 1,
    Posted = 2,
    Void = 3,
}

public enum PaymentMode
{
    Cash = 1,
    Cheque = 2,
    Upi = 3,
    Card = 4,
    BankTransfer = 5,
}
