import { Routes } from '@angular/router';

export const salesRoutes: Routes = [
  {
    path: 'quotes',
    loadComponent: () => import('./quote-list/quote-list.component').then(m => m.QuoteListComponent)
  },
  {
    path: 'quotes/new',
    loadComponent: () => import('./quote-form/quote-form.component').then(m => m.QuoteFormComponent)
  },
  {
    path: 'quotes/:id',
    loadComponent: () => import('./quote-form/quote-form.component').then(m => m.QuoteFormComponent)
  },
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
  {
    path: 'invoices',
    loadComponent: () => import('./invoice-list/invoice-list.component').then(m => m.InvoiceListComponent)
  },
  {
    path: 'invoices/new',
    loadComponent: () => import('./invoice-form/invoice-form.component').then(m => m.InvoiceFormComponent)
  },
  {
    path: 'invoices/:id',
    loadComponent: () => import('./invoice-form/invoice-form.component').then(m => m.InvoiceFormComponent)
  },

  {
    path: 'credit-notes',
    loadComponent: () => import('./credit-note-list/credit-note-list.component').then(m => m.CreditNoteListComponent)
  },
  {
    path: 'credit-notes/new',
    loadComponent: () => import('./credit-note-form/credit-note-form.component').then(m => m.CreditNoteFormComponent)
  },
  {
    path: 'credit-notes/:id',
    loadComponent: () => import('./credit-note-form/credit-note-form.component').then(m => m.CreditNoteFormComponent)
  },

  // The delivery challan. Its form existed for a while with no route and no
  // export, so it was in the repository and unreachable from the application —
  // built and invisible, which is the same as not built.
  {
    path: 'delivery-challans',
    loadComponent: () => import('./delivery-challan-list/delivery-challan-list.component').then(m => m.DeliveryChallanListComponent)
  },
  {
    path: 'delivery-challans/new',
    loadComponent: () => import('./delivery-challan-form/delivery-challan-form.component').then(m => m.DeliveryChallanFormComponent)
  },
  {
    path: 'delivery-challans/:id',
    loadComponent: () => import('./delivery-challan-form/delivery-challan-form.component').then(m => m.DeliveryChallanFormComponent)
  }
];

