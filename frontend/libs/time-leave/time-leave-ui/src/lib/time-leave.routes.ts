import { Routes } from '@angular/router';

export const timeLeaveRoutes: Routes = [
  {
    path: 'leave',
    loadComponent: () =>
      import('./leave/leave-applications.page').then((m) => m.LeaveApplicationsPage),
    data: { access: { permission: 'leave.view' } },
  },
  {
    path: 'attendance',
    loadComponent: () =>
      import('./attendance/attendance-dashboard.page').then((m) => m.AttendanceDashboardPage),
    data: { access: { permission: 'attendance.view' } },
  },
];
