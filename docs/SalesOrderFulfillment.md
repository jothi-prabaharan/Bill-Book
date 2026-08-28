# Sales Order Fulfillment

## Endpoint

`POST /api/sales/sales-orders/{SalesOrderId}/fulfill`

Permission: `sales.approve`.

The endpoint creates and posts an invoice through the existing `InvoiceService` pipeline.
That means tax calculation, stock issue/reservation release, ledger posting and Sales Register
recording remain owned by the existing invoice implementation.

## Full fulfillment

Send an empty `lines` array (or omit it):

```json
{
  "dueDate": "2026-08-31",
  "lines": []
}
```

The server invoices every remaining quantity that has not already been invoiced by a non-void invoice.

## Partial fulfillment

Send only the order lines and quantities to fulfill now:

```json
{
  "dueDate": "2026-08-31",
  "lines": [
    {
      "salesOrderDetailId": 101,
      "quantity": 40
    },
    {
      "salesOrderDetailId": 102,
      "quantity": 10
    }
  ]
}
```

The server validates that each line belongs to the order and that the requested quantity does not exceed:

`Ordered Quantity - Existing Non-Void Invoiced Quantity`

## Fulfillment accounting

For stock lines, posting the generated invoice issues stock and releases the corresponding reservation.
The Sales Order line then reconciles `DeliveredQuantity` and `ReservedQuantity` so the order can move from:

`Open → PartlyDelivered → Closed`

Delivery Challans continue to use their existing path and update the same Sales Order fulfillment quantities.

## Concurrency

The fulfillment operation uses a serializable database transaction while calculating remaining quantities,
creating/posting the invoice, and updating the Sales Order. This prevents two concurrent fulfillment requests
from intentionally invoicing the same remaining quantity based on the same stale read.

## Important existing behavior

`InvoiceService.CreateFromSalesOrderAsync` remains the legacy all-or-nothing conversion path. The new
`/fulfill` endpoint is the partial/full fulfillment path and does not use the legacy single-invoice guard.
