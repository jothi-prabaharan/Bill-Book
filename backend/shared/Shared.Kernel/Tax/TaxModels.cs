using Shared.Kernel.Documents;

namespace Shared.Kernel.Tax;

/// <summary>
/// The rate in force for one tax group on one date, as Accounting serves it.
///
/// Both splits are carried — CGST/SGST and IGST — and the calculator picks
/// whichever the supply calls for. The alternative is asking Accounting which
/// one applies, which would put the intra/inter decision in two services.
/// </summary>
public sealed record TaxRate(
    long TaxMasterId,
    long TaxGroupId,
    string? TaxSystemName,
    decimal TotalRate,
    decimal CgstRate,
    decimal SgstRate,
    decimal IgstRate,
    decimal CessRate);

/// <summary>
/// Everything the calculator needs about the document a line sits on. No
/// database, no clock, no tenant — the three settings come from the branch and
/// the two flags from the header.
/// </summary>
/// <param name="IsInterState">
/// Decided once on the header and passed in, never re-derived here. See
/// <see cref="PlaceOfSupply"/> for where it is decided.
/// </param>
/// <param name="DiscountBeforeTax">
/// Whether a discount reduces the taxable value. A trade discount does; a
/// settlement discount does not and must still be shown on the invoice.
/// </param>
public sealed record TaxContext(
    bool IsInterState,
    bool DiscountBeforeTax,

    /// <summary>
    /// Whether an intra-state supply is inside a Union Territory with no
    /// legislature, which makes the state half UTGST rather than SGST.
    ///
    /// Optional, and false by default, so the twenty-eight states — which is
    /// almost every branch — need say nothing.
    /// </summary>
    bool IsUnionTerritory = false);

/// <summary>One line, as the calculator takes it. Prices and quantities as keyed.</summary>
public sealed record TaxLineInput
{
    public required decimal Quantity { get; init; }

    public required decimal UnitPrice { get; init; }

    /// <summary>Percent, or null when the discount was keyed as an amount.</summary>
    public decimal? DiscountPercent { get; init; }

    /// <summary>The amount as keyed. Ignored when <see cref="DiscountPercent"/> is set.</summary>
    public decimal DiscountAmount { get; init; }

    /// <summary>An MRP: the price already contains its tax, and the taxable value is backed out.</summary>
    public bool IsPriceInclusive { get; init; }

    public TaxTreatment TaxTreatment { get; init; } = TaxTreatment.Taxable;

    /// <summary>
    /// The rate in force on the document date. Null on a line that carries no
    /// tax — exempt, nil-rated, outside GST — and required on one that does.
    /// </summary>
    public TaxRate? Rate { get; init; }

    public decimal ConversionFactor { get; init; } = 1m;
}

/// <summary>One computed tax component of one line.</summary>
public sealed record TaxComponentAmount(
    TaxComponent Component,
    decimal Rate,
    decimal TaxableAmount,
    decimal Amount);

/// <summary>What a line comes to.</summary>
public sealed record TaxLineResult
{
    public required decimal GrossAmount { get; init; }

    public required decimal DiscountAmount { get; init; }

    public required decimal TaxableAmount { get; init; }

    public required decimal TaxAmount { get; init; }

    public required decimal LineTotal { get; init; }

    public required decimal BaseQuantity { get; init; }

    /// <summary>
    /// Empty on an exempt, nil-rated or non-GST line. <b>Present at rate zero on
    /// a zero-rated one</b> — that is the whole reason tax is rows rather than
    /// columns, because at 0% every amount is zero and only the presence of the
    /// rows says which side of the return it belongs on.
    /// </summary>
    public required IReadOnlyList<TaxComponentAmount> Components { get; init; }
}

/// <summary>What a document's lines come to, summed from already-rounded lines.</summary>
public sealed record TaxDocumentTotals
{
    public required decimal SubTotal { get; init; }

    public required decimal DiscountAmount { get; init; }

    public required decimal TaxableAmount { get; init; }

    public required decimal CgstAmount { get; init; }

    public required decimal SgstAmount { get; init; }

    public required decimal IgstAmount { get; init; }

    public required decimal CessAmount { get; init; }

    /// <summary>Before rounding to the rupee. The round-off is the document's, not the calculator's.</summary>
    public required decimal TotalAmount { get; init; }
}
