namespace Inventory.Entity.Enums;

/// <summary>
/// What a scanned symbol contains. Gs1DataMatrix matters most: a pharma pack
/// encodes GTIN, batch and expiry in one symbol, so the scanner has to parse it
/// rather than match it whole.
/// </summary>
public enum BarcodeType
{
    Ean13 = 0,
    Code128 = 1,
    Gs1DataMatrix = 2,
    QrCode = 3,
    /// <summary>A jeweller's tag, carrying the piece's own serial.</summary>
    JewelleryTag = 4,
}
