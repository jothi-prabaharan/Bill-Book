using Shared.Kernel.Documents;

namespace Shared.Kernel.Tax;

/// <summary>
/// GST arithmetic, shared by Sales and Purchase. <b>One component, as
/// <c>CLAUDE.md</c> requires</b> — the same sale computed two ways is two
/// answers, and only one of them goes on the return.
///
/// <b>Pure: no database, no clock, no tenant, no HTTP.</b> Rates come in as
/// <see cref="TaxRate"/>, already resolved for the document's date by whoever
/// called. That is deliberate and is the point of the split: this is the piece
/// that fails silently. A wrong split still balances, still prints and still
/// posts — a GSTR-1 that a human has to reconcile, months later, is where it
/// surfaces. Something that fails silently has to be testable without a server.
///
/// <b>The order of operations is fixed here and mirrors
/// <c>line-math.ts</c> exactly.</b> The TypeScript works in integer paise
/// because JavaScript has no decimal; this works in <c>decimal</c> because C#
/// does. What matters is that both round at the same points, in the same
/// sequence — two implementations of one sum diverge by a paisa without either
/// looking wrong. `tax-fixture.json` is what proves they have not.
///
/// <code>
///   gross    = qty × unitPrice                         → 2dp
///   discount = percent → amount, or the amount as keyed → 2dp, capped at gross
///   net      = gross − discount        (when DiscountBeforeTax)
///   taxable  = inclusive ? net × 100 ÷ (100 + rate) : net → 2dp
///   tax      = taxable × componentRate ÷ 100          → 2dp, per component
///   line     = taxable + Σ components
/// </code>
///
/// <b>The three sub-decisions T0.2 asks to settle</b>, settled here and nowhere
/// else:
/// <list type="number">
/// <item><b>Inclusive and exclusive are both supported</b>, per line, because
/// MRP pricing is the Indian retail default rather than an edge case. Inclusive
/// back-computes the taxable value.</item>
/// <item><b>A discount reduces the taxable value</b> when the branch says so —
/// `DiscountBeforeTax`. A trade discount does; a settlement discount does not,
/// and must still appear on the invoice.</item>
/// <item><b>Tax rounds per component, then sums.</b> Not sum-then-round: GSTR-1
/// reconciles per line and per rate, so the component is the rounded unit, and
/// the tax printed against a line is the tax that was filed.</item>
/// </list>
/// </summary>
public static class GstCalculator
{
    /// <summary>
    /// Two decimals, half away from zero — matching
    /// <c>MidpointRounding.AwayFromZero</c> in <c>LedgerPostingService</c> and
    /// <c>roundHalfAwayFromZero</c> in <c>line-math.ts</c>.
    ///
    /// Not banker's rounding, which is .NET's default: half-to-even would send
    /// 2.125 and 2.135 in opposite directions, and a customer adding up a column
    /// of invoice lines by hand does not round to even.
    /// </summary>
    private static decimal Money(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Whether this treatment produces tax rows at all. Zero-rated does, at rate zero.</summary>
    public static bool ChargesTax(TaxTreatment treatment) =>
        treatment is TaxTreatment.Taxable or TaxTreatment.ZeroRated;

    /// <summary>
    /// The components a line carries, given where the supply is going.
    ///
    /// Intra-state splits across CGST and SGST; inter-state puts the whole rate
    /// on IGST. Cess rides on top of either and is added by
    /// <see cref="Compute"/> when the rate carries one.
    /// </summary>
    public static IReadOnlyList<TaxComponent> ComponentsFor(bool isInterState) =>
        isInterState ? [TaxComponent.Igst] : [TaxComponent.Cgst, TaxComponent.Sgst];

    /// <summary>What one line comes to.</summary>
    public static TaxLineResult Compute(TaxLineInput line, TaxContext context)
    {
        decimal gross = Money(line.Quantity * line.UnitPrice);
        decimal discount = DiscountOf(line, gross);
        decimal net = context.DiscountBeforeTax ? gross - discount : gross;

        bool charges = ChargesTax(line.TaxTreatment);
        decimal totalRate = charges ? TotalRateOf(line, context) : 0m;

        // An inclusive price already contains its tax, so the taxable value is
        // backed out of it: taxable = inclusive ÷ (1 + rate/100). At a total
        // rate of nothing the division is by one and the net stands, which is
        // what makes a zero-rated inclusive line behave.
        decimal taxable = line.IsPriceInclusive && charges && totalRate > 0m
            ? Money(net * 100m / (100m + totalRate))
            : net;

        List<TaxComponentAmount> components = charges
            ? BuildComponents(line, context, taxable)
            : [];

        decimal tax = components.Sum(c => c.Amount);

        return new TaxLineResult
        {
            GrossAmount = gross,
            DiscountAmount = discount,
            TaxableAmount = taxable,
            TaxAmount = tax,
            LineTotal = taxable + tax,
            BaseQuantity = Math.Round(
                line.Quantity * line.ConversionFactor, 6, MidpointRounding.AwayFromZero),
            Components = components,
        };
    }

    /// <summary>
    /// What a set of already-computed lines comes to.
    ///
    /// <b>Sums rounded lines rather than re-deriving from raw inputs.</b> The
    /// figure on the document has to be the sum of the figures printed above it,
    /// because somebody always adds them up.
    /// </summary>
    public static TaxDocumentTotals Totals(IReadOnlyList<TaxLineResult> lines)
    {
        decimal ByComponent(TaxComponent component) =>
            lines.SelectMany(l => l.Components)
                .Where(c => c.Component == component)
                .Sum(c => c.Amount);

        decimal taxable = lines.Sum(l => l.TaxableAmount);
        decimal cgst = ByComponent(TaxComponent.Cgst);
        decimal sgst = ByComponent(TaxComponent.Sgst);
        decimal igst = ByComponent(TaxComponent.Igst);
        decimal cess = ByComponent(TaxComponent.Cess);

        return new TaxDocumentTotals
        {
            SubTotal = lines.Sum(l => l.GrossAmount),
            DiscountAmount = lines.Sum(l => l.DiscountAmount),
            TaxableAmount = taxable,
            CgstAmount = cgst,
            SgstAmount = sgst,
            IgstAmount = igst,
            CessAmount = cess,
            TotalAmount = taxable + cgst + sgst + igst + cess,
        };
    }

    /// <summary>
    /// The discount actually applied, whether it was keyed as a percentage or an
    /// amount. Capped at the gross either way — a discount larger than the line
    /// would make the taxable value negative, and a negative taxable value
    /// posts as a credit nobody raised.
    /// </summary>
    private static decimal DiscountOf(TaxLineInput line, decimal gross)
    {
        decimal keyed = line.DiscountPercent is decimal percent
            ? Money(gross * percent / 100m)
            : line.DiscountAmount;

        return Math.Max(0m, Math.Min(keyed, gross));
    }

    private static decimal TotalRateOf(TaxLineInput line, TaxContext context)
    {
        if (line.Rate is not TaxRate rate)
        {
            return 0m;
        }

        decimal gst = context.IsInterState
            ? rate.IgstRate
            : rate.CgstRate + rate.SgstRate;

        return gst + rate.CessRate;
    }

    private static List<TaxComponentAmount> BuildComponents(
        TaxLineInput line, TaxContext context, decimal taxable)
    {
        List<TaxComponentAmount> components = [];

        if (line.Rate is not TaxRate rate)
        {
            // Zero-rated with no rate row behind it still carries its components,
            // at zero — the rows are the fact, not their value.
            foreach (TaxComponent component in ComponentsFor(context.IsInterState))
            {
                components.Add(new TaxComponentAmount(component, 0m, taxable, 0m));
            }

            return components;
        }

        foreach (TaxComponent component in ComponentsFor(context.IsInterState))
        {
            decimal componentRate = component switch
            {
                TaxComponent.Cgst => rate.CgstRate,
                TaxComponent.Sgst => rate.SgstRate,
                TaxComponent.Igst => rate.IgstRate,
                _ => 0m,
            };

            components.Add(new TaxComponentAmount(
                component, componentRate, taxable, Money(taxable * componentRate / 100m)));
        }

        // Cess rides on top of whichever arrangement applies, and only when the
        // rate carries one — an empty cess row on every line would put a
        // meaningless zero into the GSTR-1 cess column.
        if (rate.CessRate > 0m)
        {
            components.Add(new TaxComponentAmount(
                TaxComponent.Cess, rate.CessRate, taxable, Money(taxable * rate.CessRate / 100m)));
        }

        return components;
    }
}
