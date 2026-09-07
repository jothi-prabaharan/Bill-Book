import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { ReconciliationPageComponent } from './reconciliation.page';

/**
 * The page under its own class, not rendered — this workspace's Vitest has no
 * Angular Vite plugin and cannot compile a `templateUrl`.
 *
 * **`flush` is not the end of the work.** The page's methods are `async`, so
 * everything after the `await` — clearing `busy`, setting `lines`, issuing the
 * next request — runs in a microtask. A synchronous assertion straight after
 * `flush` reads the state as it was *before* the continuation, which is what
 * this spec used to do: it asserted `busy()` was false one statement after the
 * flush and found it still true. `settle()` lets the microtask queue drain
 * first.
 */
const settle = (): Promise<void> => Promise.resolve();

interface ReconciliationHarness {
  currentStatementId: { set(value: number | null): void };
  busy: () => boolean;
  error: () => string | null;
  lines: () => { bankStatementLineId: number; amount: number; isReconciled?: boolean }[];
  ngOnInit: () => void;
  reconcile: (line: unknown, match: unknown) => Promise<void>;
}

describe('ReconciliationPageComponent', () => {
  let httpTestingController: HttpTestingController;
  let component: ReconciliationHarness;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
      ]
    });
    httpTestingController = TestBed.inject(HttpTestingController);
    component = TestBed.runInInjectionContext(
      () => new ReconciliationPageComponent(),
    ) as unknown as ReconciliationHarness;
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('REC-01: should load suggestions if statement id is set', async () => {
    component.currentStatementId.set(42);
    component.ngOnInit();

    const req = httpTestingController.expectOne('/api/accounting/reconciliation/42/suggestions');
    expect(req.request.method).toBe('GET');

    expect(component.busy()).toBe(true);

    req.flush([
      {
        bankStatementLineId: 101,
        transactionDate: '2026-09-01',
        description: 'Vendor payment',
        referenceNo: 'REF-1',
        amount: 500,
        suggestedMatches: []
      }
    ]);
    await settle();

    expect(component.busy()).toBe(false);
    expect(component.lines().length).toBe(1);
    expect(component.lines()[0].amount).toBe(500);
  });

  it('REC-02: should handle errors correctly', async () => {
    component.currentStatementId.set(42);
    component.ngOnInit();

    const req = httpTestingController.expectOne('/api/accounting/reconciliation/42/suggestions');
    req.flush('Error', { status: 500, statusText: 'Internal Server Error' });
    await settle();

    expect(component.busy()).toBe(false);
    expect(component.error()).toContain('could not be read');
  });

  it('REC-03: should reconcile and mark line as reconciled', async () => {
    component.currentStatementId.set(42);
    component.ngOnInit();

    const req = httpTestingController.expectOne('/api/accounting/reconciliation/42/suggestions');
    req.flush([
      { bankStatementLineId: 101, transactionDate: '2026-09-01', description: 'Test', referenceNo: 'REF-1', amount: 500, suggestedMatches: [] }
    ]);
    await settle();

    const line = component.lines()[0];
    const match = { journalLedgerId: 201, ledgerDate: '2026-09-01', transactionTypeCode: 'PAY', amount: 500, description: 'Payment', score: 100 };

    const promise = component.reconcile(line, match);
    await settle();

    const postReq = httpTestingController.expectOne('/api/accounting/reconciliation/reconcile');
    expect(postReq.request.method).toBe('POST');
    expect(postReq.request.body).toEqual({ bankStatementLineId: 101, journalLedgerId: 201 });

    postReq.flush({});
    await promise;

    expect(component.lines()[0].isReconciled).toBe(true);
  });
});
