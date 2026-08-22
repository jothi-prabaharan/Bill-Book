namespace Accounting.Api.Services;

/// <summary>
/// What each kind of money movement means in the general ledger.
///
/// <b>The source decides the account, not the document type.</b> A spend-money
/// document carries six meanings and a receive-money five; which control account
/// a line hits, and which of the contact's balances beneath it, follows from the
/// line's <c>LedgerSourceId</c> alone. That is why this is a lookup rather than a
/// branch inside each service.
///
/// <b>Grouped by the direction the balance runs.</b> Everything owed to the
/// organization sits under Accounts Receivable and everything the organization
/// owes under Accounts Payable — including the advances, which are sub-accounts
/// beneath those two rather than control accounts of their own. So a deposit paid
/// to a supplier is a <i>receivable</i> (they owe us goods) and a customer's
/// advance is a <i>payable</i> (we owe them), which reads oddly until you
/// remember the grouping is by direction rather than by counterparty.
/// </summary>
public static class MoneyPostingMap
{
    /// <summary>Accounting's seeded control accounts, by the name it resolves them on.</summary>
    private const string Receivable = "Accounts Receivable";

    private const string Payable = "Accounts Payable";

    /// <summary>
    /// <c>Accounting.Entity.Enums.SubAccountPurpose</c>, by value. Banking does not
    /// reference Accounting's assemblies — services talk over HTTP — so the value
    /// travels as a number, the same way Inventory sends its sub-account
    /// reference type. A change to that enum's numbering is a change here.
    /// </summary>
    public const int Primary = 0;

    public const int PrepaymentAdvance = 1;

    public const int OverpaymentAdvance = 2;

    /// <summary><c>mst.LedgerTypes</c> 3 — CONTROL. Money movements have no finer dimension.</summary>
    public const int ControlLedgerType = 3;

    /// <summary><c>mst.LedgerSources</c> 11 — a transfer between the organization's own accounts.</summary>
    public const int MoneyTransferSource = 11;

    /// <summary>
    /// The kind of document a line under this source settles, or null when it
    /// settles nothing.
    ///
    /// <b>The kind follows from the source; it is not a second choice beside
    /// it.</b> A bill payment can only ever settle a bill, and an advance placed
    /// before any document exists settles nothing at all — so a line free to name
    /// an invoice on a bill payment is a line free to trace a payment to a
    /// document it did not pay. That link is what a statement reconciles on, and
    /// a wrong one is worse than a missing one: it looks answered.
    ///
    /// Null for a source not in the map at all, which the callers refuse before
    /// they reach this.
    /// </summary>
    public static string? Settles(int ledgerSourceId) => ledgerSourceId switch
    {
        2 => "BIL",   // BILLPAYMENT
        6 => "CRN",   // CREDITNOTEREFUND
        3 => "INV",   // INVOICEPAYMENT
        7 => "DBN",   // DEBITNOTEREFUND

        // 4, 8, 9, 16, 17, 18, 19, 20 — advances placed, overpayments, and
        // refunds of them. There is no document behind an advance: that is what
        // makes it one.
        //
        // 5 (INVOICEREFUND) is retired: a customer refund now goes through the
        // credit note, source 6. It is absent from the maps and the screens on
        // purpose, so a line carrying it is refused rather than guessed at.
        _ => null,
    };

    /// <summary>
    /// The source an unallocated remainder belongs under — the excess when a
    /// payment ran past what the documents on it settle.
    ///
    /// <b>An overpayment is an advance, not a negative balance.</b> Left on the
    /// trade balance it turns the contact's payable into a debit, which reads as
    /// "they owe us" on every aging report and is not what happened. So the
    /// remainder goes to the <b>overpayment</b> advance beneath the other
    /// control account, where it is visible as money held rather than as a
    /// balance running the wrong way — and held apart from deliberate
    /// prepayments, so a balance sheet can tell a deposit placed from an excess
    /// paid.
    /// </summary>
    public static int Overpayment(bool spending) => spending ? 16 : 17;

    /// <summary>
    /// The account a <b>spend</b>-money line debits. Null when the source is not
    /// one money can leave under — which is a refusal, not a default, because a
    /// line that posted to a guessed account would be wrong in a balance nobody
    /// re-reads.
    /// </summary>
    public static MoneyLeg? ForSpend(int ledgerSourceId) => ledgerSourceId switch
    {
        // Settling what we owe on a bill.
        2 => new MoneyLeg(Payable, Primary),

        // A deposit put down with a supplier before their bill exists. An
        // asset: they owe us goods.
        8 => new MoneyLeg(Receivable, PrepaymentAdvance),

        // The excess when a payment ran past the bill — money they owe us, but
        // an *overpayment* rather than a deliberate prepayment. Held apart from
        // the deposit balance, so a balance sheet can tell the two apart.
        16 => new MoneyLeg(Receivable, OverpaymentAdvance),

        // Paying a customer back for goods they returned. Clears the credit
        // balance the credit note left on their receivable.
        6 => new MoneyLeg(Receivable, Primary),

        // Giving back money a customer overpaid — clearing the overpayment
        // balance the excess created.
        18 => new MoneyLeg(Payable, OverpaymentAdvance),

        // Giving back an advance a customer placed — clearing the prepayment
        // balance the advance was placed in. Same control account, different
        // balance: the source says which one is being returned.
        19 => new MoneyLeg(Payable, PrepaymentAdvance),

        _ => null,
    };

    /// <summary>The account a <b>receive</b>-money line credits. The mirror of the above.</summary>
    public static MoneyLeg? ForReceive(int ledgerSourceId) => ledgerSourceId switch
    {
        // Settling what a customer owes on an invoice.
        3 => new MoneyLeg(Receivable, Primary),

        // An advance taken from a customer before their invoice exists. A
        // liability: we owe them goods.
        9 => new MoneyLeg(Payable, PrepaymentAdvance),

        // The excess when a receipt ran past the invoice — an overpayment, held
        // apart from a deliberate advance.
        17 => new MoneyLeg(Payable, OverpaymentAdvance),

        // A supplier paying us back for goods we returned. Clears the debit
        // balance the debit note left on their payable.
        7 => new MoneyLeg(Payable, Primary),

        // A supplier returning what we overpaid them, which clears the
        // overpayment balance the excess created.
        4 => new MoneyLeg(Receivable, OverpaymentAdvance),

        // A supplier returning the deposit we placed with them before their
        // bill existed — clearing the prepayment balance the deposit was
        // placed in. The mirror of source 18/19 on the spend side: a refund
        // clears the balance the money was placed in, not the other advance.
        20 => new MoneyLeg(Receivable, PrepaymentAdvance),

        _ => null,
    };
}

/// <summary>
/// One side of a money line: which control account, and which of the contact's
/// balances beneath it.
/// </summary>
/// <param name="AccountSystemName">
/// Named rather than numbered, because an account id is a per-branch number in a
/// database Banking does not read. The bank leg is the one exception — Banking
/// holds that id because Accounting issued it when the bank account was created.
/// </param>
public sealed record MoneyLeg(string AccountSystemName, int SubAccountPurpose);
