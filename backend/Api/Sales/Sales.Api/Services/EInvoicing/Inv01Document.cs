using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sales.Api.Services.EInvoicing;

// The notified e-invoice schema (INV-01, version 1.1), as the IRP reads it
// (TK-91). Property names are the schema's own, character for character, so
// they are written as they are rather than through a naming policy; the
// serialiser options below keep them that way and leave out what is null.

public sealed record Inv01Document
{
    public string Version { get; init; } = "1.1";

    public required Inv01Transaction TranDtls { get; init; }

    public required Inv01DocumentDetails DocDtls { get; init; }

    public required Inv01Party SellerDtls { get; init; }

    public required Inv01Buyer BuyerDtls { get; init; }

    public required IReadOnlyList<Inv01Item> ItemList { get; init; }

    public required Inv01Values ValDtls { get; init; }

    /// <summary>The invoice a credit note corrects.</summary>
    public Inv01References? RefDtls { get; init; }

    /// <summary>Transport details, when an e-way bill is asked for with the IRN (TK-93).</summary>
    public Inv01EwayBill? EwbDtls { get; init; }
}

public sealed record Inv01Transaction
{
    public string TaxSch { get; init; } = "GST";

    /// <summary><c>B2B</c>, <c>SEZWP</c>, <c>SEZWOP</c>, <c>EXPWP</c> or <c>EXPWOP</c>.</summary>
    public required string SupTyp { get; init; }

    /// <summary>Reverse charge, <c>Y</c> or <c>N</c>.</summary>
    public string RegRev { get; init; } = "N";

    /// <summary>IGST charged on an intra-state supply, <c>Y</c> or <c>N</c>.</summary>
    public string IgstOnIntra { get; init; } = "N";
}

public sealed record Inv01DocumentDetails
{
    /// <summary><c>INV</c>, <c>CRN</c> or <c>DBN</c>.</summary>
    public required string Typ { get; init; }

    public required string No { get; init; }

    /// <summary><c>dd/MM/yyyy</c>.</summary>
    public required string Dt { get; init; }
}

public record Inv01Party
{
    public required string Gstin { get; init; }

    public required string LglNm { get; init; }

    public string? TrdNm { get; init; }

    public required string Addr1 { get; init; }

    public string? Addr2 { get; init; }

    public required string Loc { get; init; }

    public int Pin { get; init; }

    /// <summary>The two-digit state code of the address.</summary>
    public required string Stcd { get; init; }

    public string? Ph { get; init; }

    public string? Em { get; init; }
}

public sealed record Inv01Buyer : Inv01Party
{
    /// <summary>Place of supply: the two-digit state code, or <c>96</c> for an export.</summary>
    public required string Pos { get; init; }
}

public sealed record Inv01Item
{
    public required string SlNo { get; init; }

    public string? PrdDesc { get; init; }

    /// <summary><c>Y</c> for a service (an SAC, which starts 99), <c>N</c> for goods.</summary>
    public required string IsServc { get; init; }

    public required string HsnCd { get; init; }

    public decimal Qty { get; init; }

    /// <summary>The UQC.</summary>
    public string? Unit { get; init; }

    public decimal UnitPrice { get; init; }

    /// <summary>Quantity times unit price, before discount.</summary>
    public decimal TotAmt { get; init; }

    public decimal Discount { get; init; }

    /// <summary>The assessable (taxable) value: <see cref="TotAmt"/> less <see cref="Discount"/>.</summary>
    public decimal AssAmt { get; init; }

    public decimal GstRt { get; init; }

    public decimal IgstAmt { get; init; }

    public decimal CgstAmt { get; init; }

    /// <summary>SGST, or UTGST in a Union Territory.</summary>
    public decimal SgstAmt { get; init; }

    public decimal CesRt { get; init; }

    public decimal CesAmt { get; init; }

    public decimal TotItemVal { get; init; }
}

public sealed record Inv01Values
{
    public decimal AssVal { get; init; }

    public decimal CgstVal { get; init; }

    public decimal SgstVal { get; init; }

    public decimal IgstVal { get; init; }

    public decimal CesVal { get; init; }

    /// <summary>A discount below the lines: a settlement discount that did not reduce the taxable value.</summary>
    public decimal Discount { get; init; }

    public decimal OthChrg { get; init; }

    public decimal RndOffAmt { get; init; }

    public decimal TotInvVal { get; init; }
}

public sealed record Inv01References
{
    public required IReadOnlyList<Inv01PrecedingDocument> PrecDocDtls { get; init; }
}

public sealed record Inv01PrecedingDocument
{
    public required string InvNo { get; init; }

    /// <summary><c>dd/MM/yyyy</c>.</summary>
    public required string InvDt { get; init; }
}

public sealed record Inv01EwayBill
{
    public string? TransId { get; init; }

    public string? TransName { get; init; }

    public int Distance { get; init; }

    /// <summary>The portal's mode code, <c>1</c> road to <c>4</c> ship.</summary>
    public string? TransMode { get; init; }

    public string? VehNo { get; init; }

    /// <summary><c>R</c> regular or <c>O</c> over-dimensional cargo.</summary>
    public string? VehType { get; init; }
}

/// <summary>How an INV-01 document is written: the schema's names as they are, nulls left out.</summary>
public static class Inv01Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(Inv01Document document) => JsonSerializer.Serialize(document, Options);
}
