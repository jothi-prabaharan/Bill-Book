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
    data: { title: 'All transactions', newRoute: '/purchase/bills/new', newTitle: 'New bill', access: { permission: 'purchase.view' } }
  },
  {
    path: 'bills',
    loadComponent: () => import('./purchase-list/purchase-list.page').then(m => m.PurchaseListPage),
    data: { type: 'Bill', title: 'Bills', newRoute: '/purchase/bills/new', newTitle: 'New bill', access: { permission: 'purchase.view' } }
  },
  {
    path: 'purchase-orders',
    loadComponent: () => import('./purchase-list/purchase-list.page').then(m => m.PurchaseListPage),
    data: { type: 'PurchaseOrder', title: 'Purchase orders', newRoute: '/purchase/purchase-orders/new', newTitle: 'New purchase order', access: { permission: 'purchase.view' } }
  },
  {
    path: 'goods-receipts',
    loadComponent: () => import('./purchase-list/purchase-list.page').then(m => m.PurchaseListPage),
    data: { type: 'GoodsReceipt', title: 'Goods receipts', newRoute: '/purchase/goods-receipts/new', newTitle: 'New goods receipt', access: { permission: 'purchase.view' } }
  },
  {
    path: 'debit-notes',
    loadComponent: () => import('./purchase-list/purchase-list.page').then(m => m.PurchaseListPage),
    data: { type: 'DebitNote', title: 'Debit notes', newRoute: '/purchase/debit-notes/new', newTitle: 'New debit note', access: { permission: 'purchase.view' } }
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
  // Printing a document, one page for all four (the :id routes below are two segments, so these never clash).
  {
    path: 'purchase-orders/:id/print',
    loadComponent: () => import('./purchase-print/purchase-print.page').then((m) => m.PurchasePrintPage),
    data: { printKind: 'purchase-orders', access: { permission: 'purchase.print' } },
  },
  {
    path: 'bills/:id/print',
    loadComponent: () => import('./purchase-print/purchase-print.page').then((m) => m.PurchasePrintPage),
    data: { printKind: 'bills', access: { permission: 'purchase.print' } },
  },
  {
    path: 'goods-receipts/:id/print',
    loadComponent: () => import('./purchase-print/purchase-print.page').then((m) => m.PurchasePrintPage),
    data: { printKind: 'goods-receipts', access: { permission: 'purchase.print' } },
  },
  {
    path: 'debit-notes/:id/print',
    loadComponent: () => import('./purchase-print/purchase-print.page').then((m) => m.PurchasePrintPage),
    data: { printKind: 'debit-notes', access: { permission: 'purchase.print' } },
  },
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
