namespace Shared.Kernel.Documents;

/// <summary>
/// The life of a trading document — one lifecycle for all nine sales and
/// purchase types, which is half the reason the base classes below exist.
///
/// <b>Four statuses, and no <c>Cancelled</c>.</b> A void document that never
/// posted and a void document that did are told apart by <c>PostedAt</c> being
/// null, which is a fact already on the row; a fifth status would be a second
/// way of saying it, and the two would eventually disagree.
/// </summary>
public enum DocumentStatus
{
    /// <summary>
    /// Being keyed. It already carries its number — see
    /// <see cref="DocumentHeaderBase.DocumentNo"/> for why — and touches no
    /// ledger and no stock.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Complete and checked, waiting to post. The step exists because posting is
    /// the irreversible act and a document is often finished by one person and
    /// released by another.
    /// </summary>
    ReadyToPost = 1,

    /// <summary>In the books. Immutable from here — corrected by a credit note, never edited.</summary>
    Posted = 2,

    /// <summary>
    /// Withdrawn. The row stays and keeps its number, always: a gap in a
    /// document series is what an auditor asks about, and abandoning a draft is
    /// a void rather than a delete for exactly that reason.
    /// </summary>
    Void = 3,
}

/// <summary>
/// How a line is treated for GST, snapshotted from the item's tax preference at
/// the moment the document was raised.
///
/// <b>These are five different things, not one thing at five rates.</b> Exempt is
/// not zero-rated and neither is nil-rated; GSTR-1 reports them in different
/// tables, and a line that lost the distinction would be filed in the wrong one.
/// </summary>
public enum TaxTreatment
{
    /// <summary>Ordinary supply at a rate. The only value that carries tax rows with a rate above zero.</summary>
    Taxable = 0,

    /// <summary>
    /// Export or SEZ. Carries tax rows <b>at rate zero</b> rather than none —
    /// which is the whole reason tax is held as rows: at 0% every amount is zero
    /// and only the presence of the rows says which side of the return it goes.
    /// </summary>
    ZeroRated = 1,

    /// <summary>Nil-rated supply. No tax rows.</summary>
    NilRated = 2,

    /// <summary>Exempt supply. No tax rows.</summary>
    Exempt = 3,

    /// <summary>Outside GST altogether — alcohol, petroleum. No tax rows.</summary>
    NonGst = 4,
}

/// <summary>
/// What a line is buying or selling, which decides where it posts.
///
/// On the base rather than only on a bill, deliberately: a <see cref="Capital"/>
/// line on a sales invoice is a fixed-asset <i>disposal</i>, and it would
/// otherwise have nowhere to be said.
/// </summary>
public enum DocumentLineType
{
    /// <summary>Goods that move. Needs an item, and is the only type that touches stock.</summary>
    Stock = 0,

    /// <summary>A service, a charge, a one-off. Posts to a named account and moves nothing.</summary>
    Expense = 1,

    /// <summary>An asset. Acquired on a bill, disposed of on an invoice; needs a fixed-asset category.</summary>
    Capital = 2,
}

/// <summary>
/// Which GST component a tax row is.
///
/// <b>Deliberately not <c>Accounting.Entity.Enums.TaxComponent</c>.</b> That one
/// discriminates a sub-account and therefore needs a <c>None</c> — a contact's
/// receivable sub-account is not a tax of any kind — and has no <c>Cess</c>. This
/// one names a tax row, which is always a real component and may be cess.
/// Forcing one enum to be both would give each a member the other must never
/// hold, and the check that stops it being used would live nowhere.
/// </summary>
public enum TaxComponent
{
    /// <summary>Central GST. Intra-state only, always beside <see cref="Sgst"/>.</summary>
    Cgst = 0,

    /// <summary>State GST. Intra-state only, always beside <see cref="Cgst"/>.</summary>
    Sgst = 1,

    /// <summary>Integrated GST. Inter-state only, and never beside CGST or SGST.</summary>
    Igst = 2,

    /// <summary>Compensation cess. Sits beside either arrangement.</summary>
    Cess = 3,
}
