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
    path: 'transactions',
    loadComponent: () => import('./purchase-list/purchase-list.page').then(m => m.PurchaseListPage),
    data: { access: { permission: 'purchase.view' } }
  },
  {
    path: 'gstr2b',
    loadComponent: () => import('./gstr2b/gstr2b.page').then((m) => m.Gstr2bPage),
    data: { access: { permission: 'purchase.view' } },
  },
  // Recurring bills (TK-139): a free add-on, the same page as Sales' recurring invoices.
  {
    path: 'recurring-bills',
    loadComponent: () => import('@bill-book/ui-components').then((m) => m.RecurringSchedulesPage),
    data: { recurringKind: 'bill', access: { permission: 'purchase.view', addOns: ['RecurringBilling'] } },
  },
  {
    path: '',
    redirectTo: 'transactions',
    pathMatch: 'full'
  },
  // Keep form routes intact for navigating to them from the transaction list
  {
    path: 'purchase-orders/new',
    loadComponent: () =>
      import('./purchase-order-form/purchase-order-form.page').then(
        (m) => m.PurchaseOrderFormPage,
      ),
    data: { access: { permission: 'purchase.edit' } },
  },
  {
    path: 'purchase-orders/:id',
    loadComponent: () =>
      import('./purchase-order-form/purchase-order-form.page').then(
        (m) => m.PurchaseOrderFormPage,
      ),
    data: { access: { permission: 'purchase.view' } },
  },
  {
    path: 'bills/new',
    loadComponent: () =>
      import('./bill-form/bill-form.page').then((m) => m.BillFormPage),
    data: { access: { permission: 'purchase.edit' } },
  },
  {
    path: 'bills/:id',
    loadComponent: () =>
      import('./bill-form/bill-form.page').then((m) => m.BillFormPage),
    data: { access: { permission: 'purchase.view' } },
  },
  {
    path: 'debit-notes/new',
    loadComponent: () =>
      import('./debit-note-form/debit-note-form.page').then(
        (m) => m.DebitNoteFormPage,
      ),
    data: { access: { permission: 'purchase.edit' } },
  },
  {
    path: 'debit-notes/:id',
    loadComponent: () =>
      import('./debit-note-form/debit-note-form.page').then(
        (m) => m.DebitNoteFormPage,
      ),
    data: { access: { permission: 'purchase.view' } },
  },
  {
    path: 'goods-receipts/new',
    loadComponent: () =>
      import('./goods-receipt-form/goods-receipt-form.page').then(
        (m) => m.GoodsReceiptFormPage,
      ),
    data: { access: { permission: 'purchase.edit' } },
  },
  {
    path: 'goods-receipts/:id',
    loadComponent: () =>
      import('./goods-receipt-form/goods-receipt-form.page').then(
        (m) => m.GoodsReceiptFormPage,
      ),
    data: { access: { permission: 'purchase.view' } },
  },
];
