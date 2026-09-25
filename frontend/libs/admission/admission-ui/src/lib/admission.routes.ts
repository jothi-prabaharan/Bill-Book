import { Routes } from '@angular/router';

/** The Admission pages (S2, TK-62), mounted under the shell by `apps/school`. */
export const admissionRoutes: Routes = [
  {
    path: 'admission/enquiries',
    loadComponent: () => import('./enquiries/enquiries.page').then((m) => m.EnquiriesPage),
    data: { access: { permission: 'admission.view', apps: ['School'] } },
  },
  {
    path: 'admission/applications',
    loadComponent: () => import('./applications/applications.list').then((m) => m.ApplicationsList),
    data: { access: { permission: 'admission.view', apps: ['School'] } },
  },
  {
    path: 'admission/applications/new',
    loadComponent: () => import('./applications/application.page').then((m) => m.ApplicationPage),
    data: { access: { permission: 'admission.create', apps: ['School'] } },
  },
  {
    path: 'admission/applications/:id',
    loadComponent: () => import('./applications/application.page').then((m) => m.ApplicationPage),
    data: { access: { permission: 'admission.view', apps: ['School'] } },
  },
];
