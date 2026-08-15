import { Routes } from '@angular/router';

/**
 * Purchase's screens.
 *
 * The debit note (T5.3) mounts here as it lands.
 * Permissions are declared per route so the frontend guard refuses a page the
 * API would refuse anyway — the two read the same `{module}.{action}` strings.
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
  {
    path: 'bills',
    loadComponent: () =>
      import('./bill-list/bill-list.page').then((m) => m.BillListPage),
    data: { permission: 'purchase.view' },
  },
  {
    path: 'bills/new',
    loadComponent: () =>
      import('./bill-form/bill-form.page').then((m) => m.BillFormPage),
    data: { permission: 'purchase.edit' },
  },
  {
    path: 'bills/:id',
    loadComponent: () =>
      import('./bill-form/bill-form.page').then((m) => m.BillFormPage),
    data: { permission: 'purchase.view' },
  },
  {
    path: 'goods-receipts',
    loadComponent: () =>
      import('./goods-receipt-list/goods-receipt-list.page').then(
        (m) => m.GoodsReceiptListPage,
      ),
    data: { permission: 'purchase.view' },
  },
  {
    path: 'goods-receipts/new',
    loadComponent: () =>
      import('./goods-receipt-form/goods-receipt-form.page').then(
        (m) => m.GoodsReceiptFormPage,
      ),
    data: { permission: 'purchase.edit' },
  },
  {
    path: 'goods-receipts/:id',
    loadComponent: () =>
      import('./goods-receipt-form/goods-receipt-form.page').then(
        (m) => m.GoodsReceiptFormPage,
      ),
    data: { permission: 'purchase.view' },
  },
];
