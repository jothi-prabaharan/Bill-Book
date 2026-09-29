using Reporting.Repository.ReadModels;

namespace Reporting.Repository.Repositories;

/// <summary>One GSTR-3B figure: the taxable value and the tax by component, in base currency.</summary>
public sealed record GstFigure(decimal Taxable, decimal Igst, decimal Cgst, decimal Sgst, decimal Cess)
{
    public static readonly GstFigure Zero = new(0m, 0m, 0m, 0m, 0m);

    public decimal Tax => Igst + Cgst + Sgst + Cess;

    public GstFigure Plus(GstFigure other) =>
        new(Taxable + other.Taxable, Igst + other.Igst, Cgst + other.Cgst, Sgst + other.Sgst, Cess + other.Cess);

    public GstFigure Minus(GstFigure other) =>
        new(Taxable - other.Taxable, Igst - other.Igst, Cgst - other.Cgst, Sgst - other.Sgst, Cess - other.Cess);

    public GstFigure Rounded() =>
        new(Math.Round(Taxable, 2), Math.Round(Igst, 2), Math.Round(Cgst, 2), Math.Round(Sgst, 2), Math.Round(Cess, 2));
}

/// <summary>GSTR-3B for a period, in the return's own table numbers (TK-115).</summary>
public sealed record Gstr3bView(
    DateOnly From,
    DateOnly To,
    GstFigure OutwardTaxable31a,
    GstFigure OutwardZeroRated31b,
    GstFigure OutwardNilExempt31c,
    GstFigure InwardReverseCharge31d,
    GstFigure ItcReverseCharge4A3,
    GstFigure ItcAllOther4A5,
    GstFigure NetItc4C,
    GstFigure IneligibleBlocked4D1,
    GstFigure IneligibleOther4D2,
    GstFigure TaxPayable,
    GstFigure NetPayable);

/// <summary>
/// GSTR-3B from the two registers (TK-115). Pure: the controller reads the
/// period's rows and hands them in, so a test holds the whole rule.
///
/// <list type="bullet">
/// <item><b>3.1</b> is outward supplies from <c>sal.SalesRegister</c>, credit notes already negative there: (a) taxed, (b) exports, (c) nil-rated and exempt. (d) is inward supplies under reverse charge, from the purchase register.</item>
/// <item><b>4A</b> is credit that can be claimed: (3) on reverse-charge purchases, (5) everything else. Debit notes reduce it.</item>
/// <item><b>4D</b> is credit that cannot: (1) blocked by section 17(5), (2) ineligible otherwise. Shown so the accountant sees it, never claimed.</item>
/// <item><b>Payable</b> is 3.1(a), (b) and (d) tax; <b>net</b> is that less 4C, per component, without the cross-utilisation order the portal applies.</item>
/// </list>
/// Every figure is in base currency: each row's amounts times its snapshot rate.
/// </summary>
public static class Gstr3bRepository
{
    /// <summary>A place of supply outside India: GST's "other territory" code.</summary>
    public const string ForeignStateCode = "96";

    public static Gstr3bView Build(
        DateOnly from,
        DateOnly to,
        IEnumerable<SalesRegisterRead> sales,
        IEnumerable<PurchaseRegisterRead> purchases,
        IReadOnlyDictionary<int, string> stateCodes)
    {
        GstFigure taxed = GstFigure.Zero, exports = GstFigure.Zero, nil = GstFigure.Zero;

        foreach (SalesRegisterRead row in sales.Where(r => r.DocumentDate >= from && r.DocumentDate <= to && Counts(r.TransactionTypeCode)))
        {
            GstFigure figure = Of(row);

            if (stateCodes.TryGetValue(row.PlaceOfSupplyStateId, out string? code) && code == ForeignStateCode)
            {
                exports = exports.Plus(figure);
            }
            else if (figure.Tax == 0m && row.GstRate == 0m)
            {
                nil = nil.Plus(figure);
            }
            else
            {
                taxed = taxed.Plus(figure);
            }
        }

        GstFigure rcm = GstFigure.Zero, itcRcm = GstFigure.Zero, itcOther = GstFigure.Zero, blocked = GstFigure.Zero, ineligible = GstFigure.Zero;

        foreach (PurchaseRegisterRead row in purchases.Where(r => r.DocumentDate >= from && r.DocumentDate <= to))
        {
            GstFigure figure = Of(row);

            if (row.ReverseCharge)
            {
                rcm = rcm.Plus(figure);
            }

            switch (row.ItcEligibility)
            {
                case "Blocked":
                    blocked = blocked.Plus(figure);
                    break;
                case "Ineligible":
                    ineligible = ineligible.Plus(figure);
                    break;
                default:
                    itcRcm = row.ReverseCharge ? itcRcm.Plus(figure) : itcRcm;
                    itcOther = row.ReverseCharge ? itcOther : itcOther.Plus(figure);
                    break;
            }
        }

        GstFigure netItc = itcRcm.Plus(itcOther);
        GstFigure payable = taxed.Plus(exports).Plus(rcm);

        return new Gstr3bView(
            from, to,
            taxed.Rounded(), exports.Rounded(), nil.Rounded(), rcm.Rounded(),
            itcRcm.Rounded(), itcOther.Rounded(), netItc.Rounded(),
            blocked.Rounded(), ineligible.Rounded(),
            payable.Rounded(), payable.Minus(netItc).Rounded());
    }

    /// <summary>Invoices, POS sales and credit notes are supplies; nothing else in the register is.</summary>
    public static bool Counts(string transactionTypeCode) =>
        transactionTypeCode is "INV" or "POS" or "CRN";

    private static GstFigure Of(SalesRegisterRead r) => new(
        r.TaxableAmountBase,
        r.IgstAmount * r.ExchangeRate,
        r.CgstAmount * r.ExchangeRate,
        r.SgstAmount * r.ExchangeRate,
        r.CessAmount * r.ExchangeRate);

    /// <summary>A debit note's positive amounts count against the credit.</summary>
    private static GstFigure Of(PurchaseRegisterRead r)
    {
        decimal sign = r.TransactionTypeCode == "DBN" ? -1m : 1m;

        return new(
            sign * r.TaxableAmountBase,
            sign * r.IgstAmount * r.ExchangeRate,
            sign * r.CgstAmount * r.ExchangeRate,
            sign * r.SgstAmount * r.ExchangeRate,
            sign * r.CessAmount * r.ExchangeRate);
    }
}
