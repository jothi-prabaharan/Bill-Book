import { auditShellRoutes } from '@bill-book/app-shell';
import { describe, expect, it } from 'vitest';
import { sisRoutes } from './sis.routes';

/** Every Sis page declares its access and belongs to School (TK-61). */
describe('sisRoutes', () => {
  it('declare data.access on every page', () => {
    expect(auditShellRoutes(sisRoutes)).toEqual([]);
  });

  it('are School pages', () => {
    for (const route of sisRoutes) {
      expect(route.data?.['access']?.apps).toEqual(['School']);
    }
  });

  it('list new before :id, so the id route does not swallow it', () => {
    const paths = sisRoutes.map((r) => r.path);
    expect(paths.indexOf('sis/students/new')).toBeLessThan(paths.indexOf('sis/students/:id'));
  });
});
