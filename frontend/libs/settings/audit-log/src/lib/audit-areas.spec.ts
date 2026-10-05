import { describe, expect, it } from 'vitest';
import { AUDIT_AREAS, auditAreasFor } from './audit-areas';

const services = (areas: { service: string }[]) => areas.map((a) => a.service);

describe('audit areas', () => {
  it('names each service once per group', () => {
    for (const areas of Object.values(AUDIT_AREAS)) {
      expect(new Set(services(areas)).size).toBe(areas.length);
    }
  });

  it('offers a trading branch its documents, whatever its trade', () => {
    for (const type of ['Retail', 'Pharma', 'Jewellery', 'Service', 'Restaurant', 'General']) {
      expect(services(auditAreasFor(type, 'None'))).toContain('sales');
    }
  });

  it('offers a school its students and no sales', () => {
    const school = services(auditAreasFor('School', null));
    expect(school).toContain('sis');
    expect(school).not.toContain('sales');
  });

  it('adds HRMS and Payroll by add-on, each service once', () => {
    const areas = services(auditAreasFor('Retail', 'Hrms, Payroll'));
    expect(areas).toContain('sales');
    expect(areas).toContain('recruitment');
    expect(areas).toContain('payroll');
    expect(new Set(areas).size).toBe(areas.length);
  });

  it('gives a payroll-only customer payroll and employees, and nothing to do with trading', () => {
    expect(services(auditAreasFor(null, 'Payroll'))).toEqual(['payroll', 'hrm']);
  });

  it('falls back to the trading areas when nothing is known', () => {
    expect(services(auditAreasFor(null, null))).toContain('sales');
  });
});
