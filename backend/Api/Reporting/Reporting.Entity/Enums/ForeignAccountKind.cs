namespace Reporting.Entity.Enums;

/// <summary>
/// What kind of account holds a foreign-currency balance on the Foreign Currency
/// Gain or Loss report (TK-220): money owed to the branch, money the branch owes,
/// or money in a bank or cash account kept in a foreign currency.
/// </summary>
public enum ForeignAccountKind
{
    Receivable = 1,
    Payable = 2,
    Bank = 3,
}
