import { Routes } from '@angular/router';
import { DocViewerComponent } from './doc-viewer.component';

export const appRoutes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'index' },
  { path: ':slug', component: DocViewerComponent },
];
