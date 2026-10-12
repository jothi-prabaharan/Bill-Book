import { Routes } from '@angular/router';
import { DocViewerComponent } from './doc-viewer/doc-viewer.component';

export const appRoutes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'overview' },
  { path: ':slug', component: DocViewerComponent },
];
