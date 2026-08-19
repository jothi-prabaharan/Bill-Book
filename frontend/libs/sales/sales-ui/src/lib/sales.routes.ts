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
  {
    path: 'sales-orders/new',
    loadComponent: () => import('./sales-order-form/sales-order-form.component').then(m => m.SalesOrderFormComponent)
  },
  {
    path: 'sales-orders/:id',
    loadComponent: () => import('./sales-order-form/sales-order-form.component').then(m => m.SalesOrderFormComponent)
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

