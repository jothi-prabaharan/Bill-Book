import { Routes } from '@angular/router';

export const salesRoutes: Routes = [
  {
    path: 'transactions',
    loadComponent: () => import('./sales-list/sales-list.component').then(m => m.SalesListComponent)
  },
  {
    path: '',
    redirectTo: 'transactions',
    pathMatch: 'full'
  },
  // Keep form routes intact for navigating to them from the transaction list
  {
    path: 'quotes/new',
    loadComponent: () => import('./quote-form/quote-form.component').then(m => m.QuoteFormComponent)
  },
  {
    path: 'quotes/:id',
    loadComponent: () => import('./quote-form/quote-form.component').then(m => m.QuoteFormComponent)
  },
  // Its own list, beside the mixed transaction list. The mixed one pages over
  // five document types at once and cannot filter by fulfilment or search a
  // sales order number, which is what somebody chasing an order actually wants.
  {
    path: 'sales-orders',
    loadComponent: () => import('./sales-order-list/sales-order-list.component').then(m => m.SalesOrderListComponent)
  },
  {
    path: 'sales-orders/new',
    loadComponent: () => import('./sales-order-form/sales-order-form.component').then(m => m.SalesOrderFormComponent)
  },
  {
    path: 'sales-orders/:id',
    loadComponent: () => import('./sales-order-form/sales-order-form.component').then(m => m.SalesOrderFormComponent)
  },
  // Its own list, beside the mixed transaction list — the mixed one cannot
  // filter by overdue or search an invoice number, which is what somebody
  // chasing a payment actually wants.
  {
    path: 'invoices',
    loadComponent: () => import('./invoice-list/invoice-list.component').then(m => m.InvoiceListComponent)
  },
  {
    path: 'invoices/new',
    loadComponent: () => import('./invoice-form/invoice-form.component').then(m => m.InvoiceFormComponent)
  },
  // Before 'invoices/:id', or the :id route swallows it.
  {
    path: 'invoices/:id/print',
    loadComponent: () => import('./invoice-print/invoice-print.page').then(m => m.InvoicePrintPage)
  },
  {
    path: 'invoices/:id',
    loadComponent: () => import('./invoice-form/invoice-form.component').then(m => m.InvoiceFormComponent)
  },
  {
    path: 'delivery-challans/new',
    loadComponent: () => import('./delivery-challan-form/delivery-challan-form.component').then(m => m.DeliveryChallanFormComponent)
  },
  {
    path: 'delivery-challans/:id',
    loadComponent: () => import('./delivery-challan-form/delivery-challan-form.component').then(m => m.DeliveryChallanFormComponent)
  },
  {
    path: 'credit-notes/new',
    loadComponent: () => import('./credit-note-form/credit-note-form.component').then(m => m.CreditNoteFormComponent)
  },
  {
    path: 'credit-notes/:id',
    loadComponent: () => import('./credit-note-form/credit-note-form.component').then(m => m.CreditNoteFormComponent)
  }
];

