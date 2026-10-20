import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { sameDayLastYear } from './balance-sheet-report.models';
import { BalanceSheetReportService } from './balance-sheet-report.service';

/** The Balance Sheet's call and the comparison-date helper its page uses (TK-199). */

function setup() {
  const get = vi.fn().mockReturnValue(of({ sections: [] }));

  TestBed.configureTestingModule({
    providers: [BalanceSheetReportService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(BalanceSheetReportService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

describe('BalanceSheetReportService', () => {
  it('BS-01: asks its own endpoint for the as-at date alone when nothing else is chosen', async () => {
    const { service, get } = setup();

    await service.run({ asOfDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/balance-sheet');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('asOfDate')).toBe('2026-10-31');
    expect(params.has('compareAsOfDate')).toBe(false);
    expect(params.has('isZeroBalanceShown')).toBe(false);
  });

  it('BS-02: sends the comparison date and the zero-balance choice when given', async () => {
    const { service, get } = setup();

    await service.run({ asOfDate: '2026-10-31', compareAsOfDate: '2025-10-31', isZeroBalanceShown: true });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('compareAsOfDate')).toBe('2025-10-31');
    expect(params.get('isZeroBalanceShown')).toBe('true');
  });
});

describe('sameDayLastYear', () => {
  it('BS-03: the same day a year earlier, and 29 February becomes 28 February', () => {
    expect(sameDayLastYear('2026-10-31')).toBe('2025-10-31');
    expect(sameDayLastYear('2028-02-29')).toBe('2027-02-28');
    expect(sameDayLastYear('2027-03-01')).toBe('2026-03-01');
  });
});
