import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, ParamMap, Router, convertToParamMap } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { onCreateRequest } from './create-request';

describe('onCreateRequest (shared/ui-components, TK-189)', () => {
  let params$: BehaviorSubject<ParamMap>;
  let router: { navigate: ReturnType<typeof vi.fn>; currentNavigation: ReturnType<typeof signal<unknown>> };
  let route: { queryParamMap: BehaviorSubject<ParamMap> };

  const listen = (open: () => void): void => TestBed.runInInjectionContext(() => onCreateRequest(open));
  const listen2 = (open: () => void, ready: () => boolean): void =>
    TestBed.runInInjectionContext(() => onCreateRequest(open, ready));

  beforeEach(() => {
    vi.useFakeTimers();
    params$ = new BehaviorSubject<ParamMap>(convertToParamMap({}));
    route = { queryParamMap: params$ };
    router = { navigate: vi.fn().mockResolvedValue(true), currentNavigation: signal<unknown>(null) };
    TestBed.configureTestingModule({
      providers: [
        { provide: ActivatedRoute, useValue: route },
        { provide: Router, useValue: router },
      ],
    });
  });

  afterEach(() => {
    vi.useRealTimers();
    TestBed.resetTestingModule();
  });

  it('CREATE-01: A page opened with ?action=create opens its form and takes the request off the URL', () => {
    params$.next(convertToParamMap({ action: 'create', status: 'draft' }));
    const open = vi.fn();
    listen(open);

    expect(open).not.toHaveBeenCalled();
    vi.runAllTimers();
    TestBed.tick();

    expect(open).toHaveBeenCalledTimes(1);
    expect(router.navigate).toHaveBeenCalledWith([], {
      relativeTo: route,
      queryParams: { action: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  });

  it('CREATE-02: The + on the list already on screen changes only the query, and still opens the form', () => {
    const open = vi.fn();
    listen(open);
    vi.runAllTimers();
    expect(open).not.toHaveBeenCalled();

    params$.next(convertToParamMap({ action: 'create' }));
    vi.runAllTimers();
    TestBed.tick();
    expect(open).toHaveBeenCalledTimes(1);
  });

  it('CREATE-03: Any other query is left alone', () => {
    const open = vi.fn();
    listen(open);

    params$.next(convertToParamMap({ action: 'edit', id: '4' }));
    vi.runAllTimers();

    expect(open).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('CREATE-04: A page already on its way elsewhere — passing the request on itself — is left alone', () => {
    params$.next(convertToParamMap({ action: 'create' }));
    const open = vi.fn();
    listen(open);

    router.currentNavigation.set({ id: 2 });
    vi.runAllTimers();

    expect(open).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('CREATE-05: Nothing opens once the page has gone', () => {
    params$.next(convertToParamMap({ action: 'create' }));
    const open = vi.fn();
    listen(open);

    TestBed.resetTestingModule();
    vi.runAllTimers();

    expect(open).not.toHaveBeenCalled();
  });

  it('CREATE-06: A page still loading opens its form once it is ready, not before', () => {
    params$.next(convertToParamMap({ action: 'create' }));
    const busy = signal(true);
    const open = vi.fn();
    listen2(open, () => !busy());

    vi.runAllTimers();
    TestBed.tick();
    expect(open).not.toHaveBeenCalled();

    busy.set(false);
    TestBed.tick();
    expect(open).toHaveBeenCalledTimes(1);

    // Once handled it does not open again when the page is busy and free again.
    busy.set(true);
    TestBed.tick();
    busy.set(false);
    TestBed.tick();
    expect(open).toHaveBeenCalledTimes(1);
  });
});

