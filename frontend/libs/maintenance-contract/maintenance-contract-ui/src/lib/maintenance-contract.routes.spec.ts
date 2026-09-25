import { auditShellRoutes } from '@bill-book/app-shell';
import { describe, expect, it } from 'vitest';
import { maintenanceContractRoutes } from './maintenance-contract.routes';

describe('maintenanceContractRoutes (TK-68)', () => {
  it('declare data.access on every page', () => {
    expect(auditShellRoutes(maintenanceContractRoutes)).toEqual([]);
  });

  it('are School pages', () => {
    for (const route of maintenanceContractRoutes) {
      expect(route.data?.['access']?.apps).toEqual(['School']);
    }
  });
});
