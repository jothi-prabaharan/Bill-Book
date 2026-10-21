import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { CashFlowSection, cashFlowRows } from './cash-flow-report.models';
import { CashFlowReportService } from './cash-flow-report.service';

/** The Cash Flow Statement's call and the rows its page draws (TK-201). */

function setup() {
  const get = vi.fn().mockReturnValue(of({ sections: [] }));

  TestBed.configureTestingModule({
    providers: [CashFlowReportService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(CashFlowReportService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

describe('CashFlowReportService', () => {
  it('CF-01: asks its own endpoint for the period alone when nothing else is chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-04-01', toDate: '2026-09-30' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/cash-flow');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-04-01');
    expect(params.get('toDate')).toBe('2026-09-30');
    expect(params.has('compareFromDate')).toBe(false);
    expect(params.has('compareToDate')).toBe(false);
  });

  it('CF-02: sends the comparison period when given', async () => {
    const { service, get } = setup();

    await service.run({
      fromDate: '2026-04-01',
      toDate: '2026-09-30',
      compareFromDate: '2025-04-01',
      compareToDate: '2025-09-30',
    });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('compareFromDate')).toBe('2025-04-01');
    expect(params.get('compareToDate')).toBe('2025-09-30');
  });
});

describe('cashFlowRows', () => {
  it('CF-03: each line comes first, then the accounts that make it up, and an empty line stands alone', () => {
    const section: CashFlowSection = {
      activity: 'Operating',
      title: 'Operating activities',
      total: 900,
      compareTotal: null,
      lines: [
        {
          line: 'ReceiptsFromCustomers',
          title: 'Receipts from customers',
          amount: 1000,
          compareAmount: null,
          accounts: [
            { accountId: 1, accountCode: '1100', accountName: 'Accounts Receivable', amount: 800, compareAmount: null },
            { accountId: 2, accountCode: '4100', accountName: 'Sales Revenue', amount: 200, compareAmount: null },
          ],
        },
        { line: 'GstAndTds', title: 'GST and TDS paid or refunded', amount: -100, compareAmount: null, accounts: [] },
      ],
    };

    expect(cashFlowRows(section).map((r) => [r.kind, r.code, r.label, r.amount])).toEqual([
      ['line', '', 'Receipts from customers', 1000],
      ['account', '1100', 'Accounts Receivable', 800],
      ['account', '4100', 'Sales Revenue', 200],
      ['line', '', 'GST and TDS paid or refunded', -100],
    ]);
  });
});
