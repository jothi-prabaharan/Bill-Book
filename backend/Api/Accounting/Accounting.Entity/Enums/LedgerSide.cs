namespace Accounting.Entity.Enums;

/// <summary>
/// The side of an account's balance that is its normal one: where it grows.
/// An asset or an expense grows on the debit side, and everything else on the
/// credit side; a contra account runs opposite its type.
/// </summary>
public enum LedgerSide
{
    Debit,
    Credit,
}
