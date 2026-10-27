namespace Reporting.Entity.Enums;

/// <summary>
/// What a document on the Aged Payables Details is (TK-234), told from the transaction type code its ledger
/// postings carry: a bill, a debit note against one, a payment, a receipt (a vendor's refund), or one of the
/// documents that carry no kind of their own.
/// </summary>
public enum PayableDocumentKind
{
    Bill = 1,
    DebitNote = 2,
    Payment = 3,
    Receipt = 4,
    Journal = 5,
    OpeningBalance = 6,
    Other = 7,
}
