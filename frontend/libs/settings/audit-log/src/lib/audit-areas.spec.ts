import { describe, expect, it } from 'vitest';
import type { AppId } from '@bill-book/auth';
import { AUDIT_AREAS, auditAreasFor } from './audit-areas';

describe('audit areas', () => {
  it('gives every app at least one area', () => {
    for (const app of ['RetailErp', 'School', 'Hrms', 'Payroll'] as AppId[]) {
      expect(auditAreasFor(app).length).toBeGreaterThan(0);
    }
  });

  it('names each service once per app', () => {
    for (const areas of Object.values(AUDIT_AREAS)) {
      const services = areas.map((a) => a.service);
      expect(new Set(services).size).toBe(services.length);
    }
  });

  it('offers RetailErp its documents and HRMS its people', () => {
    expect(auditAreasFor('RetailErp').map((a) => a.service)).toContain('sales');
    expect(auditAreasFor('Hrms').map((a) => a.service)).toContain('hrm');
    expect(auditAreasFor('School').map((a) => a.service)).toContain('sis');
  });
});
