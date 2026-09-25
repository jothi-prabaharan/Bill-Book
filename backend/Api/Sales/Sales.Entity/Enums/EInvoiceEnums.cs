namespace Sales.Entity.Enums;

/// <summary>Which document an e-invoice registers (TK-91). One e-invoice per document, ever.</summary>
public enum EInvoiceSource
{
    Invoice = 1,

    CreditNote = 2,

    /// <summary>Reserved: Sales has no debit note yet.</summary>
    DebitNote = 3,
}

/// <summary>Where an e-invoice stands at the IRP.</summary>
public enum EInvoiceStatus
{
    /// <summary>Written with the posting; not yet sent, or waiting for its next attempt.</summary>
    Pending = 1,

    /// <summary>The IRP issued an IRN.</summary>
    Registered = 2,

    /// <summary>Refused, locally or by the IRP, with a reason a person has to act on.</summary>
    Failed = 3,

    /// <summary>Cancelled at the IRP within 24 hours, or never sent and voided with the document.</summary>
    Cancelled = 4,
}

/// <summary>The IRP's own cancel reason codes, so the numbers are the wire values.</summary>
public enum EInvoiceCancelReason
{
    Duplicate = 1,

    DataEntryMistake = 2,

    OrderCancelled = 3,

    Other = 4,
}

/// <summary>Which document an e-way bill moves goods for.</summary>
public enum EwayBillSource
{
    Invoice = 1,

    DeliveryChallan = 2,

    /// <summary>A return movement.</summary>
    CreditNote = 3,
}

/// <summary>How the e-way bill came to exist.</summary>
public enum EwayBillOrigin
{
    /// <summary>Asked for with the IRN, in the same call.</summary>
    ByIrn = 1,

    /// <summary>Generated on its own from the document, with Part B.</summary>
    Standalone = 2,

    /// <summary>Made outside the product and typed in; the portal was never asked.</summary>
    Manual = 3,
}

/// <summary>Where an e-way bill stands.</summary>
public enum EwayBillStatus
{
    Pending = 1,

    Generated = 2,

    Failed = 3,

    Cancelled = 4,

    Expired = 5,
}

/// <summary>The portal's transport mode codes, so the numbers are the wire values.</summary>
public enum TransportMode
{
    Road = 1,

    Rail = 2,

    Air = 3,

    Ship = 4,
}

/// <summary>
/// The e-way bill portal's cancel reason codes. Not the IRN's: the same four
/// reasons are numbered differently on the two portals.
/// </summary>
public enum EwayBillCancelReason
{
    Duplicate = 1,

    OrderCancelled = 2,

    DataEntryMistake = 3,

    Other = 4,
}
