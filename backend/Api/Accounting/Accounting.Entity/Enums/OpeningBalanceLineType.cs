namespace Accounting.Entity.Enums;

/// <summary>
/// What an opening balance line is bringing across, which decides both what it
/// must name and who else has to be told about it.
///
/// <b>The three subledger kinds are not a convenience.</b> Bringing receivables
/// across as one figure against Accounts Receivable balances perfectly and
/// destroys the subledger: no contact owes anything, so no statement is right,
/// no aging report has a date to age from, and the first receipt has nothing to
/// settle against. The same is true of stock against Inventory. So a line names
/// the contact or the item it is really about, and the control account follows
/// from the kind rather than being chosen.
/// </summary>
public enum OpeningBalanceLineType
{
    /// <summary>
    /// A plain account balance — bank, cash, capital, retained earnings, a loan.
    /// Names the account directly and has no sub-dimension beneath it.
    /// </summary>
    GlAccount = 0,

    /// <summary>
    /// What one contact owed at go-live, on one document. Posts to that
    /// contact's trade balance beneath Accounts Receivable.
    /// </summary>
    ContactReceivable = 1,

    /// <summary>What was owed to one contact, on one document. The mirror, beneath Accounts Payable.</summary>
    ContactPayable = 2,

    /// <summary>
    /// Stock held at go-live: a quantity and a unit cost. <b>Accounting does not
    /// post this one</b> — Inventory records the movement, which seeds the
    /// weighted average and posts <c>Dr Inventory / Cr Opening Balance Equity</c>
    /// itself, because a cost Accounting invented would not match the stock the
    /// item is actually carrying.
    /// </summary>
    Item = 3,
}
