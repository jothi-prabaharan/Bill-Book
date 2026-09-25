import { auditShellRoutes } from '@bill-book/app-shell';
import { describe, expect, it } from 'vitest';
import { admissionRoutes } from './admission.routes';

/** Every Admission page declares its access and belongs to School (TK-62). */
describe('admissionRoutes', () => {
  it('declare data.access on every page', () => {
    expect(auditShellRoutes(admissionRoutes)).toEqual([]);
  });

  it('are School pages', () => {
    for (const route of admissionRoutes) {
      expect(route.data?.['access']?.apps).toEqual(['School']);
    }
  });

  it('list new before :id', () => {
    const paths = admissionRoutes.map((r) => r.path);
    expect(paths.indexOf('admission/applications/new')).toBeLessThan(paths.indexOf('admission/applications/:id'));
  });
});
