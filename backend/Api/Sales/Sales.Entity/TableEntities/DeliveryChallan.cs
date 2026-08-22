using System.ComponentModel.DataAnnotations;
using Sales.Entity.Enums;
using Shared.Kernel.Documents;

namespace Sales.Entity.TableEntities;

/// <summary>
/// A delivery challan — <c>DLC</c>. Goods leaving the premises, with or without a
/// sale behind them.
///
/// <b>It issues stock; whether it posts depends on why the goods are going.</b>
/// Only <see cref="ChallanType.Sale"/> is a supply. Job work, approval, branch
/// transfer and a sample move goods without transferring ownership, so they reach
/// no ledger at all — and booking revenue on a sample is exactly the mistake the
/// type column exists to prevent.
///
/// <b>What a sale challan posts is still open</b> — see <c>SALES.md</c> §9.
/// Issuing as <c>Dr COGS</c> at dispatch books a cost with no revenue against it,
/// so the recommendation is a <i>Goods Delivered Not Invoiced</i> control
/// account. Nothing here depends on the answer; the table can be built before it
/// is settled, and T3.6 is where it has to be.
/// </summary>
public class DeliveryChallan : DocumentHeaderBase
{
    public long DeliveryChallanId { get; set; }

    /// <summary>The order being delivered against, when there is one. A real foreign key.</summary>
    public long? SalesOrderId { get; set; }

    /// <summary>Why the goods are leaving. Decides whether this reaches the ledger at all.</summary>
    public ChallanType ChallanType { get; set; } = ChallanType.Sale;

    public DateOnly DispatchDate { get; set; }

    [MaxLength(20, ErrorMessage = "Vehicle number cannot exceed 20 characters.")]
    public string? VehicleNo { get; set; }

    [MaxLength(100, ErrorMessage = "Transporter name cannot exceed 100 characters.")]
    public string? TransporterName { get; set; }

    /// <summary>The e-way bill, when one was raised. Compliance is Phase 3; the columns are here now.</summary>
    [MaxLength(20, ErrorMessage = "E-way bill number cannot exceed 20 characters.")]
    public string? EwayBillNo { get; set; }

    public DateOnly? EwayBillDate { get; set; }
    public List<DeliveryChallanDetail> Lines { get; set; } = [];
}

