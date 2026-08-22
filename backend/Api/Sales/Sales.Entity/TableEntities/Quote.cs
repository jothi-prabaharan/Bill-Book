using Shared.Kernel.Documents;

namespace Sales.Entity.TableEntities;

/// <summary>
/// A quote — <c>QTE</c>. A price offered, valid until a date.
///
/// <b>Posts nothing, reserves nothing, moves nothing.</b> It is the cheapest
/// document in the chain, which is why it is built first: the whole machinery
/// gets exercised with no accounting risk at all.
///
/// It carries its number from creation like every document here, so a draft can
/// be sent to a customer and quoted back. It is never deleted; a quote that
/// comes to nothing is voided, or lapses.
/// </summary>
public class Quote : DocumentHeaderBase
{
    public long QuoteId { get; set; }

    /// <summary>
    /// The date the offer lapses. <b>Required</b> — a price with no end to it is
    /// a price offered forever, and this is the only header column the quote adds.
    ///
    /// Lapsing is read from this date rather than written into
    /// <see cref="DocumentHeaderBase.Status"/> by a sweeper: a job flipping rows
    /// at midnight needs a worker and a schedule and is still wrong between
    /// ticks, and a lapsed quote is not a withdrawn one.
    /// </summary>
    public DateOnly ValidUntil { get; set; }

    public List<QuoteDetail> Lines { get; set; } = [];
}
