import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { FixedAssetReconciliationReportService } from './fixed-asset-reconciliation-report.service';

describe('FixedAssetReconciliationReportService', () => {
  let service: FixedAssetReconciliationReportService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        FixedAssetReconciliationReportService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(FixedAssetReconciliationReportService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('requests fixed asset reconciliation report with required date parameters', () => {
    service.getReport({ from: '2026-04-01', to: '2026-10-28' }).subscribe();

    const req = httpMock.expectOne((r) =>
      r.url === '/api/statements/fixed-asset-reconciliation' &&
      r.params.get('from') === '2026-04-01' &&
      r.params.get('to') === '2026-10-28'
    );
    expect(req.request.method).toBe('GET');
    req.flush({ accounts: [], summary: {} });
  });
});
