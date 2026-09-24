import { auditShellRoutes } from '@bill-book/app-shell';
import { describe, expect, it } from 'vitest';
import { purchaseRoutes } from './purchase.routes';

/** Every page in this lib declares its own access (TK-44). */
describe('purchaseRoutes', () => {
  it('declare data.access on every page', () => {
    expect(auditShellRoutes(purchaseRoutes)).toEqual([]);
  });
});
