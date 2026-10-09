import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Route, RouterStateSnapshot } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';
import { AppUpdateService } from './app-update.service';
import { updateGate, withUpdateGate } from './update-gate';

describe('updateGate', () => {
  let forced = false;

  beforeEach(() => {
    forced = false;
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [{ provide: AppUpdateService, useValue: { isForcedNow: () => forced } }],
    });
  });

  const run = (): unknown =>
    TestBed.runInInjectionContext(() => updateGate({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot));

  it('lets navigation through while no update is forced', () => {
    expect(run()).toBe(true);
  });

  it('refuses every navigation while a forced update is pending', () => {
    forced = true;

    expect(run()).toBe(false);
  });
});

describe('withUpdateGate', () => {
  const other = (): boolean => true;

  it('puts the gate first on every top-level route, keeping the guards already there', () => {
    const [login, expired] = withUpdateGate([{ path: 'login' }, { path: 'expired', canActivate: [other] }]);

    expect(login.canActivate).toEqual([updateGate]);
    expect(expired.canActivate).toEqual([updateGate, other]);
  });

  it('guards the pages beneath a parent route and a lazy one', () => {
    const [shell, portal]: Route[] = withUpdateGate([
      { path: '', children: [{ path: 'dashboard' }], canActivateChild: [other] },
      { path: 'portal', loadChildren: () => Promise.resolve([]) },
    ]);

    expect(shell.canActivateChild).toEqual([updateGate, other]);
    expect(portal.canActivateChild).toEqual([updateGate]);
  });

  it('adds no child guard to a route without children', () => {
    expect(withUpdateGate([{ path: 'login' }])[0].canActivateChild).toBeUndefined();
  });

  it('leaves a redirect alone, since a redirect cannot carry a guard', () => {
    const redirect: Route = { path: '', pathMatch: 'full', redirectTo: 'dashboard' };

    expect(withUpdateGate([redirect])[0]).toBe(redirect);
  });

  it('keeps the shell marker the route audit looks for', () => {
    expect(withUpdateGate([{ path: '', data: { shellRoot: true }, children: [] }])[0].data).toEqual({ shellRoot: true });
  });
});
