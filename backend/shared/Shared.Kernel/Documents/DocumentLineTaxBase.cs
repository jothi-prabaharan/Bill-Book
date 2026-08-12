using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Documents;

/// <summary>
/// One tax component on one document line. Grain is <b>(line, component)</b>.
///
/// <b>Why rows and not four columns on the line.</b> Intra-state is two
/// components, inter-state is one, and cess is a third — a fixed set of columns
/// only ever half-applies, and the half that does not is four zeroes that mean
/// "not applicable" and read as "nothing".
///
/// The sharper reason is that <b>rows make a zero-rated supply legible</b>. At 0%
/// every amount is zero, so columns cannot tell an export from an exempt supply
/// from a nil-rated one — and GSTR-1 files those in three different tables. A
/// zero-rated line carries tax rows at rate zero; an exempt line carries none.
/// The presence of the rows is the fact, not their value.
///
/// Unique on (line, component), and CGST/SGST may never sit on the same line as
/// IGST — a determination that got that wrong still balances, still prints and
/// still posts, and the return is where it would otherwise surface, months later.
/// </summary>
public abstract class DocumentLineTaxBase : OrgScopedEntity
{
    public TaxComponent TaxComponent { get; set; }

    /// <summary>
    /// The GST sub-account this component posts against, resolved when the
    /// document was raised. <b>What the <c>TAX</c> ledger leg keys on</b>, which
    /// is why it is resolved here rather than looked up at post: the rate in
    /// force moves, and the leg has to land where the document said it would.
    /// </summary>
    public long SubAccountId { get; set; }

    /// <summary>Snapshot at the document date. Never today's rate.</summary>
    public decimal Rate { get; set; }

    /// <summary>
    /// What this component was charged on. Carried per row rather than read from
    /// the line, because cess is sometimes charged on a different value from GST.
    /// </summary>
    public decimal TaxableAmount { get; set; }

    public decimal Amount { get; set; }

    /// <summary>In the branch's base currency, at the header's rate. What the ledger posts.</summary>
    public decimal AmountBase { get; set; }
}
