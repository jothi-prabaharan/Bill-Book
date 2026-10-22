namespace Reporting.Entity.Enums;

/// <summary>
/// Accounting's <c>PaymentMethod</c> on Receive and Spend money. Accounting stores it by name, so these names must stay Accounting's;
/// the numbers are only this copy's (TK-213).
/// </summary>
public enum PaymentMethod
{
    Cash = 0,
    Cheque = 1,
    BankTransfer = 2,
    Upi = 3,
    Card = 4,
    DemandDraft = 5,
    Wallet = 6,
    Other = 7,
}
