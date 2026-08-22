namespace Shared.Kernel.Documents;

/// <summary>Why a lifecycle move was refused. Every value is something a user can act on.</summary>
public enum DocumentTransitionOutcome
{
    Ok = 0,

    /// <summary>A posted document is never edited. It is corrected by a credit or debit note.</summary>
    AlreadyPosted = 1,

    /// <summary>Nothing happens to a voided document. It is the terminal state.</summary>
    AlreadyVoid = 2,

    /// <summary>Approving something that is not a draft, or posting something already posted.</summary>
    WrongStatus = 3,

    /// <summary>Nothing to post: the document has no lines.</summary>
    NoLines = 4,

    /// <summary>A void with no reason given.</summary>
    ReasonRequired = 5,

    /// <summary>Something downstream points at this document — a payment, an allocated note, a delivery.</summary>
    HasDownstream = 6,

    /// <summary>A document row is never deleted. See <see cref="DocumentLifecycle"/>.</summary>
    NeverDeleted = 7,
}

/// <summary>The answer, and what to tell the user when it is no.</summary>
public sealed record DocumentTransition(DocumentTransitionOutcome Outcome, string? Detail = null)
{
    public bool IsAllowed => Outcome == DocumentTransitionOutcome.Ok;

    public static readonly DocumentTransition Allowed = new(DocumentTransitionOutcome.Ok);
}

/// <summary>
/// What each status permits — one table, for every trading document in the
/// product.
///
/// <b>Why this is shared rather than written per service.</b> Nine sales and
/// purchase documents plus the money documents have one lifecycle between them,
/// and the interesting cases are the ones nobody thinks about until they happen:
/// whether a <c>ReadyToPost</c> document can still be edited, whether a void
/// needs a reason, whether an abandoned draft is deleted. Answered nine times
/// they would be answered nine slightly different ways, and the copy that
/// disagreed would be found by a customer.
///
/// <b>Pure.</b> No database and no clock — whether anything downstream points at
/// the document is a query the calling service owns, and it is passed in. That
/// keeps the rules testable without a server, which matters because a lifecycle
/// bug looks like nothing until an invoice is edited after it was filed.
///
/// <b>A document row is never deleted.</b> Its number was issued when it was
/// created, so it has been spent; abandoning it is a <see cref="DocumentStatus.Void"/>
/// with a reason, which turns an unexplained hole in the series into an
/// answerable "INV-0042 was cancelled on the 3rd, by this user, because…".
/// Consecutive numbering on an Indian invoice is statutory rather than tidiness,
/// so <see cref="CanDelete"/> exists only to say no in one place.
/// </summary>
public static class DocumentLifecycle
{
    /// <summary>
    /// Whether the document's own fields and lines may still be changed.
    ///
    /// <b><see cref="DocumentStatus.ReadyToPost"/> is editable.</b> It marks a
    /// document as finished and waiting for whoever posts it, not as frozen —
    /// the person reviewing it is often the one who spots the error, and making
    /// them send it backwards to fix a typo is how the review step gets skipped.
    /// </summary>
    public static DocumentTransition CanEdit(DocumentStatus status) => status switch
    {
        DocumentStatus.Draft or DocumentStatus.ReadyToPost => DocumentTransition.Allowed,

        DocumentStatus.Posted => new(
            DocumentTransitionOutcome.AlreadyPosted,
            "A posted document is never edited. Correct it with a credit or debit note, or void "
                + "it and raise another — either way both stay on record."),

        _ => new(
            DocumentTransitionOutcome.AlreadyVoid,
            "This document has been voided. Its number stays on record; raise a new document."),
    };

    /// <summary>
    /// Draft → ReadyToPost. <b>This is what <c>sales.approve</c> and
    /// <c>purchase.approve</c> are for</b> — both have been seeded and granted
    /// since the beginning and nothing ever read one.
    ///
    /// A branch that does not want the step posts straight from
    /// <see cref="DocumentStatus.Draft"/>; nothing forces a document through it.
    /// </summary>
    public static DocumentTransition CanApprove(DocumentStatus status, int lineCount) => status switch
    {
        DocumentStatus.Draft when lineCount == 0 => new(
            DocumentTransitionOutcome.NoLines,
            "There is nothing to approve — the document has no lines."),

        DocumentStatus.Draft => DocumentTransition.Allowed,

        DocumentStatus.ReadyToPost => new(
            DocumentTransitionOutcome.WrongStatus,
            "This document is already waiting to be posted."),

        DocumentStatus.Posted => new(
            DocumentTransitionOutcome.AlreadyPosted, "This document has already been posted."),

        _ => new(DocumentTransitionOutcome.AlreadyVoid, "This document has been voided."),
    };

    /// <summary>
    /// Draft or ReadyToPost → Posted. The irreversible one: it writes to the
    /// ledger, moves stock, and freezes the document.
    /// </summary>
    public static DocumentTransition CanPost(DocumentStatus status, int lineCount) => status switch
    {
        (DocumentStatus.Draft or DocumentStatus.ReadyToPost) when lineCount == 0 => new(
            DocumentTransitionOutcome.NoLines,
            "There is nothing to post — the document has no lines."),

        DocumentStatus.Draft or DocumentStatus.ReadyToPost => DocumentTransition.Allowed,

        DocumentStatus.Posted => new(
            DocumentTransitionOutcome.AlreadyPosted, "This document has already been posted."),

        _ => new(
            DocumentTransitionOutcome.AlreadyVoid,
            "A voided document cannot be posted. Raise a new one."),
    };

    /// <summary>
    /// Anything → Void. Covers both an abandoned draft and a posted document
    /// taken back out; <c>PostedAt</c> being null is what keeps the two
    /// distinguishable afterwards, which is why there is no fifth status.
    /// </summary>
    /// <param name="hasDownstream">
    /// Whether another document points at this one — a payment against an
    /// invoice, an allocated credit note, a delivery against an order. The
    /// calling service establishes this; the rule only knows what to do about it.
    /// </param>
    /// <param name="reason">
    /// Required, always. A void with no reason is the row somebody has to
    /// reconstruct from memory a year later, and it is the whole difference
    /// between a gap that is explained and a gap that is a hole.
    /// </param>
    public static DocumentTransition CanVoid(
        DocumentStatus status, bool hasDownstream, string? reason)
    {
        if (status == DocumentStatus.Void)
        {
            return new(
                DocumentTransitionOutcome.AlreadyVoid, "This document is already voided.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return new(
                DocumentTransitionOutcome.ReasonRequired,
                "Say why this is being voided. The document and its number stay on record, and "
                    + "the reason is what makes the gap in the sequence answerable.");
        }

        // Checked after the reason on purpose: being told to give a reason and
        // then being refused anyway is worse than being refused first.
        if (hasDownstream)
        {
            return new(
                DocumentTransitionOutcome.HasDownstream,
                "Something else points at this document — a payment, an allocated note or a "
                    + "delivery. Undo that first; voiding underneath it would leave the other "
                    + "document referring to something that was withdrawn.");
        }

        return DocumentTransition.Allowed;
    }

    /// <summary>
    /// Never. Kept as a method rather than an absence so that a service reaching
    /// for a delete finds the reason instead of nothing.
    /// </summary>
    public static DocumentTransition CanDelete() => new(
        DocumentTransitionOutcome.NeverDeleted,
        "Documents are not deleted. This one was given its number when it was created, so that "
            + "number has been spent — void it instead and the sequence stays explicable.");
}
