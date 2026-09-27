namespace Shared.Kernel.CustomFields;

/// <summary>
/// What a custom field belongs to (TK-110, design "Custom fields and custom
/// reports"). Line-level kinds are a later extension.
/// </summary>
public enum CustomFieldEntityKind
{
    Contact = 1,
    Item = 2,
    Quote = 3,
    SalesOrder = 4,
    DeliveryChallan = 5,
    Invoice = 6,
    CreditNote = 7,
    PurchaseOrder = 8,
    GoodsReceipt = 9,
    Bill = 10,
    DebitNote = 11,
    Lead = 12,
    Ticket = 13,
    Project = 14,
    Warehouse = 15,
}

/// <summary>A custom field's type, which decides what a value may be.</summary>
public enum CustomFieldDataType
{
    Text = 1,
    LongText = 2,
    Integer = 3,
    Decimal = 4,

    /// <summary>An ISO <c>yyyy-MM-dd</c> string.</summary>
    Date = 5,

    Boolean = 6,

    /// <summary>One of the field's active options.</summary>
    Dropdown = 7,

    /// <summary>Several of the field's active options, as an array.</summary>
    MultiSelect = 8,

    Email = 9,
    Url = 10,
    Phone = 11,
}
