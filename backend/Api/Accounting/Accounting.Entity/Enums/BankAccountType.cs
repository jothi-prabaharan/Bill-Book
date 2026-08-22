namespace Accounting.Entity.Enums;

/// <summary>
/// What kind of account this is. It decides the GL account's type and parent
/// group: overdrafts, cash credit and credit cards are Liabilities, because an
/// overdrawn account is borrowing and showing it as a negative asset is what
/// gets queried.
/// </summary>
public enum BankAccountType
{
    Savings = 0,
    Current = 1,
    OverDraft = 2,
    CashCredit = 3,
    CreditCard = 4,
    Wallet = 5,
    /// <summary>Cash in hand. Has no bank behind it.</summary>
    Cash = 6,
}
