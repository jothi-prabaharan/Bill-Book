import { describe, expect, it } from 'vitest';
import { buildCrumbs } from './shell-breadcrumb.component';

/** The one crumb builder the shell and the strip share (TK-188). */
describe('buildCrumbs', () => {
  it('CRUMB-01: one crumb per segment, only the last not a link', () => {
    const crumbs = buildCrumbs('/purchase/purchase-orders');
    expect(crumbs.map((c) => c.label)).toEqual(['Purchase', 'Purchase orders']);
    expect(crumbs.map((c) => c.isLink)).toEqual([true, false]);
  });

  it('CRUMB-02: a new record in a role names the role, as the form heading does', () => {
    expect(buildCrumbs('/contacts/new?role=customer').map((c) => c.label)).toEqual(['Contacts', 'New customer']);
    expect(buildCrumbs('/contacts/new?role=jobworker').at(-1)?.label).toBe('New job worker');
  });

  it('CRUMB-03: without a role, a new record is just New, and the query never shows', () => {
    expect(buildCrumbs('/sales/invoices/new').at(-1)?.label).toBe('New');
    expect(buildCrumbs('/contacts?includeInactive=true').map((c) => c.label)).toEqual(['Contacts']);
  });

  it('CRUMB-04: home has none', () => {
    expect(buildCrumbs('/')).toEqual([]);
    expect(buildCrumbs('/dashboard')).toEqual([]);
  });
});
