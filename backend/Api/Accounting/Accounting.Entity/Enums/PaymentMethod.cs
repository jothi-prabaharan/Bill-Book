namespace Accounting.Entity.Enums;

/// <summary>
/// How the money actually moved. Reporting and reconciliation both read it: a
/// cheque takes days to clear and a UPI transfer does not, so a bank statement
/// and the ledger disagree for a while in one case and never in the other.
///
/// <b>Not the same thing as the bank account.</b> The account says whose money
/// it is; this says how it travelled. Cash paid out of a cash-in-hand account
/// and cash paid out of a till are the same method against different accounts.
/// </summary>
public enum PaymentMethod
{
    Cash = 0,
    Cheque = 1,

    /// <summary>NEFT, RTGS or IMPS — distinguished on the reference, not here.</summary>
    BankTransfer = 2,

    Upi = 3,
    Card = 4,
    DemandDraft = 5,
    Wallet = 6,
    Other = 7,
}
