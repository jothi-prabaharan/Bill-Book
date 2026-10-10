import { Routes } from '@angular/router';

export const salesRoutes: Routes = [
  {
    path: 'transactions',
    redirectTo: 'quotes',
    pathMatch: 'full'
  },
  {
    path: 'quotes',
    loadComponent: () => import('./sales-list/sales-list.component').then(m => m.SalesListComponent),
    data: { motionIndex: 4, depth: 0, type: 'Quote', title: 'Quotes', newRoute: '/sales/quotes/new', newTitle: 'New quote', access: { permission: 'sales.view' } }
  },
  {
    path: 'delivery-challans',
    loadComponent: () => import('./sales-list/sales-list.component').then(m => m.SalesListComponent),
    data: { motionIndex: 4, depth: 0, type: 'DeliveryChallan', title: 'Delivery challans', newRoute: '/sales/delivery-challans/new', newTitle: 'New delivery challan', access: { permission: 'sales.view' } }
  },
  {
    path: 'credit-notes',
    loadComponent: () => import('./sales-list/sales-list.component').then(m => m.SalesListComponent),
    data: { motionIndex: 4, depth: 0, type: 'CreditNote', title: 'Credit notes', newRoute: '/sales/credit-notes/new', newTitle: 'New credit note', access: { permission: 'sales.view' } }
  },
  {
    path: '',
    redirectTo: 'quotes',
    pathMatch: 'full'
  },
  // Keep form routes intact for navigating to them from the transaction list
  {
    path: 'quotes/new',
    loadComponent: () => import('./quote-form/quote-form.component').then(m => m.QuoteFormComponent), data: { motionIndex: 4, depth: 2 }
  },
  {
    path: 'quotes/:id',
    loadComponent: () => import('./quote-form/quote-form.component').then(m => m.QuoteFormComponent), data: { motionIndex: 4, depth: 2 }
  },
  // Its own list, beside the mixed transaction list. The mixed one pages over
  // five document types at once and cannot filter by fulfilment or search a
  // sales order number, which is what somebody chasing an order actually wants.
  {
    path: 'sales-orders',
    loadComponent: () => import('./sales-order-list/sales-order-list.component').then(m => m.SalesOrderListComponent), data: { motionIndex: 4, depth: 0 }
  },
  {
    path: 'sales-orders/new',
    loadComponent: () => import('./sales-order-form/sales-order-form.component').then(m => m.SalesOrderFormComponent), data: { motionIndex: 4, depth: 2 }
  },
  {
    path: 'sales-orders/:id',
    loadComponent: () => import('./sales-order-form/sales-order-form.component').then(m => m.SalesOrderFormComponent), data: { motionIndex: 4, depth: 2 }
  },
  // Its own list, beside the mixed transaction list â€” the mixed one cannot
  // filter by overdue or search an invoice number, which is what somebody
  // chasing a payment actually wants.
  {
    path: 'invoices',
    loadComponent: () => import('./invoice-list/invoice-list.component').then(m => m.InvoiceListComponent), data: { motionIndex: 4, depth: 0 }
  },
  {
    path: 'invoices/new',
    loadComponent: () => import('./invoice-form/invoice-form.component').then(m => m.InvoiceFormComponent), data: { motionIndex: 4, depth: 2 }
  },
  // Before 'invoices/:id', or the :id route swallows it.
  {
    path: 'invoices/:id/print',
    loadComponent: () => import('./invoice-print/invoice-print.page').then(m => m.InvoicePrintPage), data: { motionIndex: 4, depth: 2 }
  },
  {
    path: 'invoices/:id',
    loadComponent: () => import('./invoice-form/invoice-form.component').then(m => m.InvoiceFormComponent), data: { motionIndex: 4, depth: 2 }
  },
  // Recurring invoices (TK-139): a free add-on, shared with Purchase's recurring bills.
  {
    path: 'recurring-invoices',
    loadComponent: () => import('@bill-book/ui-components').then(m => m.RecurringSchedulesPage),
    data: { motionIndex: 4, depth: 1, recurringKind: 'invoice', access: { permission: 'sales.view', addOns: ['RecurringBilling'] } },
  },
  // Jewellery savings schemes (TK-141): a paid add-on for Jewellery branches.
  {
    path: 'savings-schemes',
    loadComponent: () => import('./savings-schemes/savings-schemes.page').then(m => m.SavingsSchemesPage),
    data: { motionIndex: 4, depth: 1, access: { permission: 'sales.view', businessTypes: ['Jewellery'], addOns: ['SavingsScheme'] } },
  },
  {
    path: 'delivery-challans/new',
    loadComponent: () => import('./delivery-challan-form/delivery-challan-form.component').then(m => m.DeliveryChallanFormComponent), data: { motionIndex: 4, depth: 2 }
  },
  {
    path: 'delivery-challans/:id',
    loadComponent: () => import('./delivery-challan-form/delivery-challan-form.component').then(m => m.DeliveryChallanFormComponent), data: { motionIndex: 4, depth: 2 }
  },
  {
    path: 'credit-notes/new',
    loadComponent: () => import('./credit-note-form/credit-note-form.component').then(m => m.CreditNoteFormComponent), data: { motionIndex: 4, depth: 2 }
  },
  {
    path: 'credit-notes/:id',
    loadComponent: () => import('./credit-note-form/credit-note-form.component').then(m => m.CreditNoteFormComponent), data: { motionIndex: 4, depth: 2 }
  }
];


