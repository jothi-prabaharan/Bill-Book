import { Routes } from '@angular/router';
import { ClaimCategoriesPage } from './categories/claim-categories.page';
import { ClaimsListPage } from './claims/claims.list';
import { ClaimDetailPage } from './claims/claim.page';
import { MyClaimsPage } from './my-claims/my-claims.page';

export const claimsRoutes: Routes = [
  {
    path: 'claims',
    children: [
      { path: '', component: ClaimsListPage, data: { access: { permission: 'claims.view' } } },
      { path: 'categories', component: ClaimCategoriesPage, data: { access: { permission: 'claims.edit' } } },
      { path: ':id', component: ClaimDetailPage, data: { access: { permission: 'claims.view' } } },
    ],
  },
  {
    path: 'me/claims',
    component: MyClaimsPage,
    data: { access: { signedIn: true } },
  },
];
