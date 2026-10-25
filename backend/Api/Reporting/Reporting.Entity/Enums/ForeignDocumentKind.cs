namespace Reporting.Entity.Enums;

/// <summary>
/// What a row of the Foreign Currency Gain or Loss Details report (TK-221) is: a
/// document that still carries a foreign balance, which is where unrealized gain or
/// loss sits, or a settlement that posted a realized difference.
/// </summary>
public enum ForeignDocumentKind
{
    /// <summary>A document with a balance still open at the To date.</summary>
    Open = 1,

    /// <summary>A settlement in the period that posted an exchange difference.</summary>
    Settlement = 2,
}
