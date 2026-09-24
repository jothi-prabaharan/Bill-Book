import { Component } from '@angular/core';
import { Routes } from '@angular/router';
import { pageGuard } from '@bill-book/auth';
import { describe, expect, it } from 'vitest';
import { auditShellRoutes, shellRoutes } from './shell-routes';

@Component({ standalone: true, template: '' })
class PageStub {}

/** `shellRoutes` attaches the page guard, and `auditShellRoutes` finds a page that declares nothing (TK-44). */
describe('shellRoutes', () => {
  const pages: Routes = [
    { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
    { path: 'dashboard', component: PageStub, data: { access: { signedIn: true } } },
    { path: 'payroll/runs', component: PageStub, data: { access: { permission: 'payroll.view' } } },
    {
      path: 'employees',
      loadChildren: () => Promise.resolve([]),
      data: { access: { permission: 'employee.view' } },
    },
  ];

  it('attaches the page guard to every page, so no app lists a guard', () => {
    const shell = shellRoutes({ app: 'Payroll', children: pages });

    expect(shell.canActivateChild).toContain(pageGuard);
    expect(shell.data?.['app']).toBe('Payroll');
    expect(shell.children?.some((r) => r.path === 'no-access')).toBe(true);
  });

  it('passes when every page declares access', () => {
    expect(auditShellRoutes([shellRoutes({ app: 'Payroll', children: pages })])).toEqual([]);
  });

  it('fails when one page loses its data.access', () => {
    const broken = pages.map((r) => (r.path === 'payroll/runs' ? { ...r, data: {} } : r));

    expect(auditShellRoutes([shellRoutes({ app: 'Payroll', children: broken })])).toEqual(['payroll/runs']);
  });

  it("lets a child inherit its lazy parent's access, but not the shell's", () => {
    const nested: Routes = [
      {
        path: 'sales',
        data: { access: { permission: 'sales.view' } },
        children: [{ path: 'invoices', component: PageStub }],
      },
      { path: 'orphan', component: PageStub },
    ];

    expect(auditShellRoutes([shellRoutes({ app: 'RetailErp', children: nested })])).toEqual(['orphan']);
  });

  it('audits a lib route array on its own', () => {
    expect(auditShellRoutes([{ path: 'x', component: PageStub }])).toEqual(['x']);
  });
});
