import { Routes } from '@angular/router';

/** The student attendance pages (S3, TK-63), mounted under the shell in `apps/web` for a School branch. */
export const studentAttendanceRoutes: Routes = [
  {
    path: 'attendance/register',
    loadComponent: () => import('./register/register.page').then((m) => m.RegisterPage),
    data: { access: { permission: 'attendance.view', businessTypes: ['School'] } },
  },
];
