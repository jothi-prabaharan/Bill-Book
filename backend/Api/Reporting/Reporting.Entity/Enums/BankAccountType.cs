namespace Reporting.Entity.Enums;

/// <summary>
/// What kind of account a bank account is, as Accounting stores it: by name, so
/// these names must stay Accounting's (TK-202).
/// </summary>
public enum BankAccountType
{
    Savings = 0,
    Current = 1,
    OverDraft = 2,
    CashCredit = 3,
    CreditCard = 4,
    Wallet = 5,
    Cash = 6,
    EEFC = 7,
}
