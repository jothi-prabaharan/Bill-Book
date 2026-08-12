namespace Master.Entity.Enums;

/// <summary>
/// The kind of account a contact is paid into. Distinct from Banking's own
/// account types: this describes someone else's account, and all we do with it
/// is send money to it.
/// </summary>
public enum BankAccountKind
{
    Savings = 0,
    Current = 1,
    Other = 2,
}
