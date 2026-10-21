import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { previousPeriod, ratioChangeText, ratioValueText } from './business-performance-report.models';
import { BusinessPerformanceReportService } from './business-performance-report.service';

/** Business Performance's call, and the period and unit helpers its page uses (TK-203). */

function setup() {
  const get = vi.fn().mockReturnValue(of({ ratios: [] }));

  TestBed.configureTestingModule({
    providers: [BusinessPerformanceReportService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(BusinessPerformanceReportService), get };
}

const money = (amount: number) => `₹${amount}`;

describe('BusinessPerformanceReportService', () => {
  it('BP-01: asks its own endpoint for the period, and the comparison when given', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-04-01', toDate: '2026-09-30' });
    await service.run({ fromDate: '2026-04-01', toDate: '2026-09-30', compareFromDate: '2025-04-01', compareToDate: '2025-09-30' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/business-performance');
    const first = (get.mock.calls[0][1] as { params: HttpParams }).params;
    expect(first.get('fromDate')).toBe('2026-04-01');
    expect(first.has('compareFromDate')).toBe(false);
    const second = (get.mock.calls[1][1] as { params: HttpParams }).params;
    expect(second.get('compareFromDate')).toBe('2025-04-01');
    expect(second.get('compareToDate')).toBe('2025-09-30');
  });
});

describe('previousPeriod', () => {
  it('BP-02: the same number of days, ending the day before', () => {
    expect(previousPeriod('2026-04-01', '2026-09-30')).toEqual({ from: '2025-09-30', to: '2026-03-31' });
    expect(previousPeriod('2026-03-01', '2026-03-31')).toEqual({ from: '2026-01-29', to: '2026-02-28' });
    expect(previousPeriod('2026-10-21', '2026-10-21')).toEqual({ from: '2026-10-20', to: '2026-10-20' });
  });
});

describe('ratioValueText and ratioChangeText', () => {
  it('BP-03: each unit reads as itself, and no value is a dash', () => {
    expect(ratioValueText(45, 'Percent', money)).toBe('45.00%');
    expect(ratioValueText(91.5, 'Days', money)).toBe('91.50 days');
    expect(ratioValueText(3.68, 'Times', money)).toBe('3.68 times');
    expect(ratioValueText(112000, 'Amount', money)).toBe('₹112000');
    expect(ratioValueText(null, 'Percent', money)).toBe('—');
  });

  it('BP-04: a change is signed in its unit, and none is empty', () => {
    expect(ratioChangeText(5, 'Percent', money)).toBe('+5.00 pts');
    expect(ratioChangeText(-2.5, 'Days', money)).toBe('−2.50 days');
    expect(ratioChangeText(0.25, 'Times', money)).toBe('+0.25');
    expect(ratioChangeText(-1000, 'Amount', money)).toBe('−₹1000');
    expect(ratioChangeText(null, 'Percent', money)).toBe('');
  });
});
