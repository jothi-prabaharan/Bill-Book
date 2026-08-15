import { Routes } from '@angular/router';

/**
 * Purchase's screens.
 *
 * Every document in the purchase chain has a screen.
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
    path: 'debit-notes',
    loadComponent: () =>
      import('./debit-note-list/debit-note-list.page').then(
        (m) => m.DebitNoteListPage,
      ),
    data: { permission: 'purchase.view' },
  },
  {
    path: 'debit-notes/new',
    loadComponent: () =>
      import('./debit-note-form/debit-note-form.page').then(
        (m) => m.DebitNoteFormPage,
      ),
    data: { permission: 'purchase.edit' },
  },
  {
    path: 'debit-notes/:id',
    loadComponent: () =>
      import('./debit-note-form/debit-note-form.page').then(
        (m) => m.DebitNoteFormPage,
      ),
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
