import { auditShellRoutes } from '@bill-book/app-shell';
import { describe, expect, it } from 'vitest';
import { reportingRoutes } from './reporting.routes';

/** Every page in this lib declares its own access (TK-44). */
describe('reportingRoutes', () => {
  it('declare data.access on every page', () => {
    expect(auditShellRoutes(reportingRoutes)).toEqual([]);
  });
});
