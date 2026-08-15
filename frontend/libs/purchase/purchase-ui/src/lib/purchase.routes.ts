import { Routes } from '@angular/router';

/**
 * Purchase's screens.
 *
 * The goods receipt (T4.4), the bill (T4.5) and the debit note (T5.3) mount
 * here as they land. Permissions are declared per route so the frontend guard
 * refuses a page the API would refuse anyway — the two read the same
 * `{module}.{action}` strings.
 */
export const purchaseRoutes: Routes = [
  {
    path: 'purchase-orders',
    loadComponent: () =>
      import('./purchase-order-list/purchase-order-list.page').then(
        (m) => m.PurchaseOrderListPage,
      ),
    data: { permission: 'purchase.view' },
  },
  {
    path: 'purchase-orders/new',
    loadComponent: () =>
      import('./purchase-order-form/purchase-order-form.page').then(
        (m) => m.PurchaseOrderFormPage,
      ),
    data: { permission: 'purchase.edit' },
  },
  {
    path: 'purchase-orders/:id',
    loadComponent: () =>
      import('./purchase-order-form/purchase-order-form.page').then(
        (m) => m.PurchaseOrderFormPage,
      ),
    data: { permission: 'purchase.view' },
  },
];
