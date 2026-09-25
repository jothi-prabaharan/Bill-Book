import { Routes } from '@angular/router';

/**
 * The Sis pages (S1, TK-61), mounted under the shell by `apps/school`.
 * School's own, so each names its app and the shell refuses it elsewhere.
 */
export const sisRoutes: Routes = [
  {
    path: 'sis/students',
    loadComponent: () => import('./students/students.list').then((m) => m.StudentsList),
    data: { access: { permission: 'sis.view', apps: ['School'] } },
  },
  {
    path: 'sis/students/new',
    loadComponent: () => import('./students/student.page').then((m) => m.StudentPage),
    data: { access: { permission: 'sis.create', apps: ['School'] } },
  },
  {
    path: 'sis/students/:id',
    loadComponent: () => import('./students/student.page').then((m) => m.StudentPage),
    data: { access: { permission: 'sis.view', apps: ['School'] } },
  },
  {
    path: 'sis/setup',
    loadComponent: () => import('./setup/academic-setup.page').then((m) => m.AcademicSetupPage),
    data: { access: { permission: 'sis.view', apps: ['School'] } },
  },
  {
    path: 'sis/exams',
    loadComponent: () => import('./exams/exams.page').then((m) => m.ExamsPage),
    data: { access: { permission: 'sis.view', apps: ['School'] } },
  },
];
