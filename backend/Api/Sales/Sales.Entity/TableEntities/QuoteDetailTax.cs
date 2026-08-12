using Shared.Kernel.Documents;

namespace Sales.Entity.TableEntities;

/// <summary>One tax component on one quote line. Grain is (line, component).</summary>
public class QuoteDetailTax : DocumentLineTaxBase
{
    public long QuoteDetailTaxId { get; set; }

    public long QuoteDetailId { get; set; }
}
