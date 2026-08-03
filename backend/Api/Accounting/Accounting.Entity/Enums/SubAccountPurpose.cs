namespace Accounting.Entity.Enums;

/// <summary>
/// Which balance a sub-account holds beneath its control account.
///
/// It exists because a contact needs <b>three</b> sub-accounts under Accounts
/// Receivable and three under Accounts Payable, and without a discriminator all
/// three would share the key
/// (<c>AccountId</c>, <c>ReferenceType</c>, <c>ReferenceId</c>,
/// <c>TaxComponent</c>) and collide. It does for a contact exactly what
/// <see cref="TaxComponent"/> does for CGST, SGST and IGST under one tax parent.
///
/// <b>It is also what makes the balance sheet filable.</b> Schedule III of the
/// Companies Act requires advances to suppliers and advances from customers to
/// be shown separately from trade receivables and trade payables. Those advances
/// sit inside the AR and AP control accounts here, so the control account total
/// is not a Schedule III line on its own — this column is what splits it back
/// out, and any balance sheet that ignores it overstates trade receivables and
/// trade payables by the advances held.
/// </summary>
public enum SubAccountPurpose
{
    /// <summary>
    /// The trade balance itself — what is owed against documents. Every
    /// non-contact sub-account is this too: an item's inventory or revenue
    /// balance, a tax rate's collected balance.
    /// </summary>
    Primary = 0,

    /// <summary>
    /// Money that moved before any document existed — a deposit paid to a
    /// supplier, an advance received from a customer.
    /// </summary>
    PrepaymentAdvance = 1,

    /// <summary>
    /// The excess when a payment or receipt ran past what the document asked
    /// for. Held apart from a deliberate advance because the two arise
    /// differently, even though both are money held for the same contact.
    /// </summary>
    OverpaymentAdvance = 2,
}
