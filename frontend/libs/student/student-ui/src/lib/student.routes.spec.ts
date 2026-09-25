import { auditShellRoutes } from '@bill-book/app-shell';
import { describe, expect, it } from 'vitest';
import { studentRoutes } from './student.routes';

/** Every Student page declares its access and belongs to School (TK-61). */
describe('studentRoutes', () => {
  it('declare data.access on every page', () => {
    expect(auditShellRoutes(studentRoutes)).toEqual([]);
  });

  it('are School pages', () => {
    for (const route of studentRoutes) {
      expect(route.data?.['access']?.apps).toEqual(['School']);
    }
  });

  it('list new before :id, so the id route does not swallow it', () => {
    const paths = studentRoutes.map((r) => r.path);
    expect(paths.indexOf('sis/students/new')).toBeLessThan(paths.indexOf('sis/students/:id'));
  });
});
