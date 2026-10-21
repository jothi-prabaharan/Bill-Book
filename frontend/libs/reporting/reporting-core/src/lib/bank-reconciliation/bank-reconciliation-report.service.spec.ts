import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { bankAccountLabel } from './bank-reconciliation-report.models';
import { BankReconciliationReportService } from './bank-reconciliation-report.service';

/** The Reconciliation Report's calls and the account label its filter shows (TK-202). */

function setup() {
  const get = vi.fn().mockReturnValue(of([]));

  TestBed.configureTestingModule({
    providers: [BankReconciliationReportService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(BankReconciliationReportService), get };
}

describe('BankReconciliationReportService', () => {
  it('BR-01: asks its own endpoint for one bank account at the end of one day', async () => {
    const { service, get } = setup();

    await service.run({ bankAccountId: 7, asOfDate: '2026-09-30' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/bank-reconciliation');
    const params = (get.mock.calls[0][1] as { params: HttpParams }).params;
    expect(params.get('bankAccountId')).toBe('7');
    expect(params.get('asOfDate')).toBe('2026-09-30');
  });

  it('BR-02: reads the bank accounts from the endpoint beside it', async () => {
    const { service, get } = setup();

    await service.accounts();

    expect(get.mock.calls[0][0]).toBe('/api/statements/bank-reconciliation/accounts');
  });
});

describe('bankAccountLabel', () => {
  it('BR-03: name, number and currency, with a closed account marked and a placeholder number left out', () => {
    expect(
      bankAccountLabel({ bankAccountId: 7, accountName: 'Main Bank', accountNumber: '0012345678', currencyCode: 'INR', isActive: true }),
    ).toBe('Main Bank · 0012345678 (INR)');
    expect(
      bankAccountLabel({ bankAccountId: 8, accountName: 'Petty Cash', accountNumber: '-', currencyCode: 'INR', isActive: false }),
    ).toBe('Petty Cash (INR) · inactive');
  });
});
