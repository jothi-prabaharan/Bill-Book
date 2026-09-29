namespace Shared.Kernel.CustomFields;

/// <summary>Which kind of custom field a printable document type carries (TK-112).</summary>
public static class CustomFieldKinds
{
    public static CustomFieldEntityKind? ForDocumentType(string documentTypeCode) => documentTypeCode.Trim().ToUpperInvariant() switch
    {
        "QTE" => CustomFieldEntityKind.Quote,
        "SOR" => CustomFieldEntityKind.SalesOrder,
        "DLC" => CustomFieldEntityKind.DeliveryChallan,
        "INV" or "POS" => CustomFieldEntityKind.Invoice,
        "CRN" => CustomFieldEntityKind.CreditNote,
        "POR" => CustomFieldEntityKind.PurchaseOrder,
        "GRN" => CustomFieldEntityKind.GoodsReceipt,
        "BIL" => CustomFieldEntityKind.Bill,
        "DBN" => CustomFieldEntityKind.DebitNote,
        _ => null,
    };
}
