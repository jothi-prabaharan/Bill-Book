using Shared.Kernel.Documents;

namespace Purchase.Entity.TableEntities;

/// <summary>
/// One tax component on one bill line.
///
/// Identical in shape to the sales side and opposite in meaning: this is
/// <b>Input</b> GST, an asset and reclaimable, where a sales invoice's is Output
/// GST and a liability. A vendor who is unregistered or on the composition
/// scheme charges none, and the bill must claim none — that is a property of the
/// contact, read when the bill is raised.
/// </summary>
public class BillDetailTax : DocumentLineTaxBase
{
    public long BillDetailTaxId { get; set; }

    public long BillDetailId { get; set; }
}
