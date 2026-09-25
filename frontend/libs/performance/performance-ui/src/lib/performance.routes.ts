import { Routes } from '@angular/router';
import { CalibrationPage } from './calibration/calibration.page';
import { CyclesPage } from './cycles/cycles.page';
import { GoalsPage } from './goals/goals.page';
import { ReviewsPage } from './reviews/reviews.page';
import { SelfEvaluationPage } from './self-evaluation/self-evaluation.page';

export const performanceRoutes: Routes = [
  {
    path: 'performance',
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'reviews' },
      { path: 'cycles', component: CyclesPage, data: { access: { permission: 'performance.view' } } },
      { path: 'goals', component: GoalsPage, data: { access: { permission: 'performance.view' } } },
      { path: 'reviews', component: ReviewsPage, data: { access: { permission: 'performance.view' } } },
      { path: 'calibration', component: CalibrationPage, data: { access: { permission: 'performance.view' } } },
      { path: 'self-evaluation', component: SelfEvaluationPage, data: { access: { signedIn: true } } },
    ],
  },
];
