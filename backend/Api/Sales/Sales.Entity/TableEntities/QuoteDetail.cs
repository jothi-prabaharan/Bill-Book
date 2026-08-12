using Shared.Kernel.Documents;

namespace Sales.Entity.TableEntities;

/// <summary>
/// One line of a quote. Adds nothing to <see cref="DocumentLineBase"/> — a quote
/// promises nothing beyond a price, so it has no delivered, reserved or returned
/// quantity to track.
/// </summary>
public class QuoteDetail : DocumentLineBase
{
    public long QuoteDetailId { get; set; }

    public long QuoteId { get; set; }
}
