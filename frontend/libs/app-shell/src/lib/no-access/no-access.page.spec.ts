import { describe, expect, it } from 'vitest';
import { permissionLabel } from './no-access.page';

/** The no-access page names a permission as a person reads it (TK-44). */
describe('permissionLabel', () => {
  it('turns a code into Module: action', () => {
    expect(permissionLabel('payroll.view')).toBe('Payroll: view');
    expect(permissionLabel('settings.edit')).toBe('Settings: edit');
  });

  it('leaves an odd code as it is and nothing as nothing', () => {
    expect(permissionLabel('odd')).toBe('odd');
    expect(permissionLabel(null)).toBeNull();
  });
});
