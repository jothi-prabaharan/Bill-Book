using Sales.Entity.Enums;
using Sales.Entity.TableEntities;

namespace Sales.Api.Services;

/// <summary>
/// Where a sales order stands once goods have gone out or been billed against it.
///
/// One place, because three documents move an order's quantities — the challan
/// that delivers, the invoice that bills (and delivers, when nothing went out
/// before it), and the order's own fulfilment action — and three copies of the
/// same status rule are three chances for them to disagree about one order.
/// </summary>
public static class SalesOrderFulfilment
{
    /// <summary>
    /// Sets the fulfilment status from what has been delivered: Closed when every
    /// line is out in full, Partly delivered when anything is, Open otherwise.
    ///
    /// A cancelled order stays cancelled, and one closed short by agreement stays
    /// closed — its status records a decision, not arithmetic, and a delivery
    /// arriving afterwards must not reopen it.
    /// </summary>
    public static void Refresh(SalesOrder order)
    {
        if (order.FulfilmentStatus == FulfilmentStatus.Cancelled || order.ShortCloseReason is not null)
        {
            return;
        }

        bool allDelivered = order.Lines.All(l => l.DeliveredQuantity >= l.Quantity);
        bool someDelivered = order.Lines.Any(l => l.DeliveredQuantity > 0m);

        order.FulfilmentStatus = allDelivered
            ? FulfilmentStatus.Closed
            : someDelivered ? FulfilmentStatus.PartlyDelivered : FulfilmentStatus.Open;
    }

    /// <summary>
    /// How much of an order line has gone out but not been billed — what an
    /// invoice against the line covers before it issues any stock of its own.
    /// </summary>
    public static decimal DeliveredNotInvoiced(SalesOrderDetail line) =>
        Math.Max(0m, line.DeliveredQuantity - line.InvoicedQuantity);
}
