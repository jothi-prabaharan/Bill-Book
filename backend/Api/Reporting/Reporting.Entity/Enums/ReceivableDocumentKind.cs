namespace Reporting.Entity.Enums;

/// <summary>
/// What a document on the Aged Receivables Detail is (TK-222, TK-223), told from the
/// transaction type code its ledger postings carry.
/// </summary>
public enum ReceivableDocumentKind
{
    Invoice = 1,
    CreditNote = 2,
    Receipt = 3,
    Payment = 4,
    Journal = 5,
    OpeningBalance = 6,
    Other = 7,
}
