import { auditShellRoutes } from '@bill-book/app-shell';
import { describe, expect, it } from 'vitest';
import { studentAttendanceRoutes } from './student-attendance.routes';

describe('studentAttendanceRoutes (TK-63)', () => {
  it('declare data.access on every page', () => {
    expect(auditShellRoutes(studentAttendanceRoutes)).toEqual([]);
  });

  it('are School pages, apart from HRMS staff attendance', () => {
    for (const route of studentAttendanceRoutes) {
      expect(route.data?.['access']?.apps).toEqual(['School']);
    }
  });
});
