import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { ReconciliationPageComponent } from './reconciliation.page';

describe('ReconciliationPageComponent', () => {
  let httpTestingController: HttpTestingController;
  let component: any;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
      ]
    });
    httpTestingController = TestBed.inject(HttpTestingController);
    component = TestBed.runInInjectionContext(() => new ReconciliationPageComponent());
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('REC-01: should load suggestions if statement id is set', () => {
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

    expect(component.busy()).toBe(false);
    expect(component.lines().length).toBe(1);
    expect(component.lines()[0].amount).toBe(500);
  });

  it('REC-02: should handle errors correctly', () => {
    component.currentStatementId.set(42);
    component.ngOnInit();

    const req = httpTestingController.expectOne('/api/accounting/reconciliation/42/suggestions');
    req.flush('Error', { status: 500, statusText: 'Internal Server Error' });

    expect(component.busy()).toBe(false);
    expect(component.error()).toContain('could not be read');
  });

  it('REC-03: should reconcile and mark line as reconciled', async () => {
    component.currentStatementId.set(42);
    component.ngOnInit();

    let req = httpTestingController.expectOne('/api/accounting/reconciliation/42/suggestions');
    req.flush([
      { bankStatementLineId: 101, transactionDate: '2026-09-01', description: 'Test', referenceNo: 'REF-1', amount: 500, suggestedMatches: [] }
    ]);

    const line = component.lines()[0];
    const match = { journalLedgerId: 201, ledgerDate: '2026-09-01', transactionTypeCode: 'PAY', amount: 500, description: 'Payment', score: 100 };

    const promise = component.reconcile(line, match);
    const postReq = httpTestingController.expectOne('/api/accounting/reconciliation/reconcile');
    expect(postReq.request.method).toBe('POST');
    expect(postReq.request.body).toEqual({ bankStatementLineId: 101, journalLedgerId: 201 });
    
    postReq.flush({});
    await promise;

    expect(component.lines()[0].isReconciled).toBe(true);
  });
});
