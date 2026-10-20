import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { samePeriodLastYear } from './profit-and-loss-report.models';
import { ProfitAndLossReportService } from './profit-and-loss-report.service';

/** The Profit & Loss's call and the comparison-period helper its page uses (TK-200). */

function setup() {
  const get = vi.fn().mockReturnValue(of({ sections: [] }));

  TestBed.configureTestingModule({
    providers: [ProfitAndLossReportService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(ProfitAndLossReportService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

describe('ProfitAndLossReportService', () => {
  it('PL-01: asks its own endpoint for the period alone when nothing else is chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-04-01', toDate: '2026-09-30' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/profit-and-loss');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-04-01');
    expect(params.get('toDate')).toBe('2026-09-30');
    expect(params.has('compareFromDate')).toBe(false);
    expect(params.has('compareToDate')).toBe(false);
    expect(params.has('isZeroBalanceShown')).toBe(false);
  });

  it('PL-02: sends the comparison period and the zero-movement choice when given', async () => {
    const { service, get } = setup();

    await service.run({
      fromDate: '2026-04-01',
      toDate: '2026-09-30',
      compareFromDate: '2025-04-01',
      compareToDate: '2025-09-30',
      isZeroBalanceShown: true,
    });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('compareFromDate')).toBe('2025-04-01');
    expect(params.get('compareToDate')).toBe('2025-09-30');
    expect(params.get('isZeroBalanceShown')).toBe('true');
  });
});

describe('samePeriodLastYear', () => {
  it('PL-03: both ends a year earlier, and 29 February becomes 28 February', () => {
    expect(samePeriodLastYear('2026-04-01', '2026-09-30')).toEqual({ from: '2025-04-01', to: '2025-09-30' });
    expect(samePeriodLastYear('2028-02-01', '2028-02-29')).toEqual({ from: '2027-02-01', to: '2027-02-28' });
  });
});
