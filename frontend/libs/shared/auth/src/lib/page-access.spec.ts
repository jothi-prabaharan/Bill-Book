import { describe, expect, it } from 'vitest';
import { decideAccess } from './page-access';
import { SessionContext } from './session-context.service';

/** The shell's five checks, in order (H0.3, TK-44). */
describe('decideAccess', () => {
  const context = (overrides: Partial<SessionContext> = {}): SessionContext => ({
    displayName: 'Priya',
    email: 'priya@example.com',
    branchName: 'Chennai',
    branchCode: 'CHN',
    app: 'Payroll',
    licenseStatus: 'Active',
    licenseExpiry: null,
    expiryIsBranchLevel: false,
    permissions: ['payroll.view', 'settings.view'],
    apps: [],
    ...overrides,
  });

  it('1. sends a signed-out user to sign in', () => {
    expect(decideAccess(false, null, 'Payroll', { signedIn: true })).toEqual({ kind: 'signIn' });
  });

  it('2. closes the app when its licence is not open, before anything else', () => {
    expect(decideAccess(true, context({ licenseStatus: 'Expired' }), 'Payroll', undefined)).toEqual({ kind: 'licence' });
    expect(decideAccess(true, context({ licenseStatus: 'NotLicensed' }), 'Payroll', { signedIn: true })).toEqual({
      kind: 'licence',
    });
  });

  it("3. refuses a shared page this app may not mount", () => {
    expect(
      decideAccess(true, context(), 'Payroll', { permission: 'contacts.view', apps: ['RetailErp', 'School'] }),
    ).toEqual({ kind: 'app' });
  });

  it('4. refuses a page that declares no access', () => {
    expect(decideAccess(true, context(), 'Payroll', undefined)).toEqual({ kind: 'undeclared' });
  });

  it('5. refuses a page whose permission the user lacks in this app', () => {
    expect(decideAccess(true, context(), 'Payroll', { permission: 'payroll.post' })).toEqual({
      kind: 'permission',
      permission: 'payroll.post',
    });
  });

  it('lets a declared, permitted page through', () => {
    expect(decideAccess(true, context(), 'Payroll', { permission: 'payroll.view' })).toBeNull();
    expect(decideAccess(true, context({ licenseStatus: 'Trial' }), 'Payroll', { signedIn: true })).toBeNull();
  });
});
