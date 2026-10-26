namespace Reporting.Entity.Enums;

/// <summary>
/// Why a sales credit note was raised, as Sales stores it (by name). Only a <see cref="SalesReturn"/>
/// brings goods back; the others change what was charged and leave the units sold as they were.
/// </summary>
public enum CreditNoteReason
{
    /// <summary>Goods came back. The only reason that moves stock.</summary>
    SalesReturn = 0,

    /// <summary>The invoice was priced wrong. Nothing physical happened.</summary>
    PriceCorrection = 1,

    /// <summary>A discount agreed after the invoice was raised.</summary>
    PostSaleDiscount = 2,

    /// <summary>Short delivery, damage, a gesture. No stock comes back.</summary>
    Deficiency = 3,

    /// <summary>The invoice should not have existed and is being cancelled in full.</summary>
    Cancellation = 4,
}
