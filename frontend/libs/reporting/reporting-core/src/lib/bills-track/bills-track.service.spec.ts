import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { localIsoDate } from '../invoice-track/invoice-track.models';
import {
  BILL_TRACK_STATUSES,
  BillTrackStatus,
  BillsTrackResult,
  BillsTrackRow,
  appendBillsTrackPage,
  billApprovalLabel,
  billApprovalTag,
  billInBooks,
  billTrackStatusLabel,
  billTrackStatusTag,
  billsTrackRows,
  billsTrackSentence,
  billsTrackTabs,
  hasMoreBillsTrack,
  isForeignBillTrack,
} from './bills-track.models';
import { BillsTrackService } from './bills-track.service';

/** The Bills Track Report's calls and the helpers its page uses (TK-237). */

function setup(answer: unknown = { bills: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [BillsTrackService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(BillsTrackService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const row = (over: Partial<BillsTrackRow> = {}): BillsTrackRow => ({
  typeCode: 'BIL',
  documentId: 1,
  documentNo: 'BIL-0001',
  vendorBillNo: 'V/0001',
  documentDate: '2026-10-02',
  postedAt: '2026-10-02T09:00:00Z',
  dueDate: '2026-10-17',
  lastPaymentDate: '2026-10-12',
  daysOverdue: null,
  contactId: 101,
  contactCode: 'STEELCO',
  contactName: 'Steelco Metals',
  currencyCode: 'INR',
  reverseCharge: false,
  totalInCurrency: 11800,
  total: 11800,
  payable: 11800,
  paid: 11800,
  due: 0,
  status: 'Paid',
  approvalStatus: null,
  approvalStep: null,
  ...over,
});

const result = (bills: BillsTrackRow[], over: Partial<BillsTrackResult> = {}): BillsTrackResult => ({
  fromDate: '2026-10-01',
  toDate: '2026-10-31',
  baseCurrencyCode: 'INR',
  bills,
  totalBills: bills.length,
  pageNumber: 1,
  pageSize: 25,
  statusCounts: { Draft: 0, ReadyToPost: 0, AwaitingPayment: 0, PartlyPaid: 0, Overdue: 0, Paid: bills.length, Void: 0 },
  totals: {
    total: bills.reduce((t, r) => t + r.total, 0),
    paid: bills.reduce((t, r) => t + r.paid, 0),
    due: bills.reduce((t, r) => t + r.due, 0),
  },
  ...over,
});

describe('BillsTrackService', () => {
  it('BT-01: runs with both dates and nothing else when nothing was chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/bills-track-report');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-10-01');
    expect(params.get('toDate')).toBe('2026-10-31');
    for (const name of ['contactId', 'status', 'pageNumber', 'pageSize']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('BT-02: sends the vendor, status and page that were chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31', contactId: 102, status: 'Overdue', pageNumber: 2, pageSize: 25 });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('contactId')).toBe('102');
    expect(params.get('status')).toBe('Overdue');
    expect(params.get('pageNumber')).toBe('2');
    expect(params.get('pageSize')).toBe('25');
  });

  it('BT-03: gives back what the endpoint answered', async () => {
    const answer = result([row()]);
    const { service } = setup(answer);

    expect(await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' })).toEqual(answer);
  });

  it('BT-04: reads the vendors the filter offers from the report’s own lookup', async () => {
    const vendors = [{ contactId: 101, contactCode: 'STEELCO', contactName: 'Steelco Metals' }];
    const { service, get } = setup(vendors);

    expect(await service.vendors()).toEqual(vendors);
    expect(get.mock.calls[0][0]).toBe('/api/statements/bills-track-report/vendors');
  });
});

describe('bills track helpers', () => {
  it('BT-05: the statuses run along a bill’s life and read as words', () => {
    expect(BILL_TRACK_STATUSES.map((s) => s.value)).toEqual(['Draft', 'ReadyToPost', 'AwaitingPayment', 'PartlyPaid', 'Overdue', 'Paid', 'Void']);
    expect(billTrackStatusLabel('ReadyToPost')).toBe('Ready to post');
    expect(billTrackStatusLabel('AwaitingPayment')).toBe('Awaiting payment');
  });

  it('BT-06: paid reads as done, overdue as a problem and what is not in the books as outlined', () => {
    expect(billTrackStatusTag('Paid')).toBe('tag-success');
    expect(billTrackStatusTag('Overdue')).toBe('tag-danger');
    expect(billTrackStatusTag('PartlyPaid')).toBe('tag-warning');
    expect(billTrackStatusTag('AwaitingPayment')).toBe('tag-info');
    expect(billTrackStatusTag('Draft')).toBe('tag-outline');
    expect(billTrackStatusTag('ReadyToPost')).toBe('tag-outline');
    expect(billTrackStatusTag('Void')).toBe('tag-neutral');
  });

  it('BT-07: the tabs are All and then every status, with a nought kept', () => {
    const tabs = billsTrackTabs({ Draft: 1, ReadyToPost: 1, AwaitingPayment: 1, PartlyPaid: 2, Overdue: 1, Paid: 1, Void: 1 });

    expect(tabs.map((t) => t.label)).toEqual(['All', 'Draft', 'Ready to post', 'Awaiting payment', 'Partly paid', 'Overdue', 'Paid', 'Void']);
    expect(tabs.map((t) => t.value)).toEqual(['', 'Draft', 'ReadyToPost', 'AwaitingPayment', 'PartlyPaid', 'Overdue', 'Paid', 'Void']);
    expect(tabs.map((t) => t.count)).toEqual([7, 1, 1, 1, 2, 1, 1, 1]);
  });

  it('BT-08: All leaves the void ones out, as the list does without a status', () => {
    const counts: Record<BillTrackStatus, number> = { Draft: 1, ReadyToPost: 0, AwaitingPayment: 0, PartlyPaid: 0, Overdue: 0, Paid: 3, Void: 4 };

    expect(billsTrackTabs(counts)[0].count).toBe(4);
  });

  it('BT-09: with no counts yet every tab reads nought', () => {
    expect(billsTrackTabs(undefined).map((t) => t.count)).toEqual([0, 0, 0, 0, 0, 0, 0, 0]);
  });

  it('BT-10: a draft and a ready-to-post bill are not in the books, and every other status is', () => {
    expect(billInBooks('Draft')).toBe(false);
    expect(billInBooks('ReadyToPost')).toBe(false);
    for (const status of ['AwaitingPayment', 'PartlyPaid', 'Overdue', 'Paid', 'Void'] as const) {
      expect(billInBooks(status)).toBe(true);
    }
  });

  it('BT-11: a foreign row is told by a currency other than the branch’s, whatever the case', () => {
    expect(isForeignBillTrack({ currencyCode: 'USD' }, 'INR')).toBe(true);
    expect(isForeignBillTrack({ currencyCode: 'inr' }, 'INR')).toBe(false);
    expect(isForeignBillTrack({ currencyCode: 'USD' }, '')).toBe(false);
  });

  it('BT-12: an approval reads as words only when there is something to say, and an in-approval bill names its level', () => {
    expect(billApprovalLabel('Approved', null)).toBe('Approved');
    expect(billApprovalLabel('Rejected', null)).toBe('Rejected');
    expect(billApprovalLabel('InApproval', 'Level 2')).toBe('In approval · Level 2');
    expect(billApprovalLabel('InApproval', null)).toBe('In approval');
    expect(billApprovalLabel('Draft', null)).toBeNull();
    expect(billApprovalLabel(null, null)).toBeNull();
  });

  it('BT-13: approved reads as done, rejected as a problem and waiting as pending', () => {
    expect(billApprovalTag('Approved')).toBe('tag-success');
    expect(billApprovalTag('Rejected')).toBe('tag-danger');
    expect(billApprovalTag('InApproval')).toBe('tag-warning');
  });

  it('BT-14: the grid has a row for each bill and then the totals of every posted bill', () => {
    const rows = billsTrackRows(result([row({ documentId: 1 }), row({ documentId: 2, documentNo: 'BIL-0002' })]));

    expect(rows.map((r) => r.kind)).toEqual(['bill', 'bill', 'total']);
    expect(new Set(rows.map((r) => r.key)).size).toBe(3);
    expect(rows[2].name).toBe('Total of posted');
    expect(rows[2].total).toBe(23600);
    expect(rows[2].approval).toBeNull();
  });

  it('BT-15: when not every bill is loaded the totals row says it is of all of them', () => {
    const rows = billsTrackRows(result([row()], { totalBills: 120 }));

    expect(rows[rows.length - 1].name).toBe('Total of posted, all 120');
  });

  it('BT-16: a bill row links to its page and carries the vendor’s bill number and the dates of its life', () => {
    const [bill] = billsTrackRows(result([row({ documentId: 7, daysOverdue: 11, status: 'Overdue' })]));

    expect(bill.link).toBe('/purchase/bills/7');
    expect(bill.vendorBillNo).toBe('V/0001');
    expect([bill.documentDate, bill.dueDate, bill.lastPaymentDate, bill.daysOverdue]).toEqual(['2026-10-02', '2026-10-17', '2026-10-12', 11]);
    expect(bill.postedOn).toBe(localIsoDate('2026-10-02T09:00:00Z'));
  });

  it('BT-17: a draft has no posted day, due date or last payment, and is not in the books so its paid and due are left blank', () => {
    const [draft] = billsTrackRows(
      result([row({ status: 'Draft', postedAt: null, lastPaymentDate: null, paid: 0, due: 0, payable: 0, dueDate: null })]),
    );

    expect([draft.postedOn, draft.lastPaymentDate, draft.dueDate, draft.inBooks]).toEqual([null, null, null, false]);
    expect(draft.owed).toBeNull();
  });

  it('BT-18: what the vendor is owed is shown only when it is not the total, as with TDS or reverse charge', () => {
    const rows = billsTrackRows(
      result([
        row({ documentId: 1, total: 59000, payable: 58000, paid: 0, due: 58000, status: 'AwaitingPayment' }),
        row({ documentId: 2, total: 9440, payable: 8000, reverseCharge: true, status: 'PartlyPaid', paid: 3000, due: 5000 }),
        row({ documentId: 3 }),
      ]),
    );

    expect(rows.slice(0, 3).map((r) => r.owed)).toEqual([58000, 8000, null]);
    expect(rows[1].reverseCharge).toBe(true);
  });

  it('BT-19: a bill that went through approval says where it stands and one that did not says nothing', () => {
    const rows = billsTrackRows(
      result([
        row({ documentId: 1, approvalStatus: 'Approved' }),
        row({ documentId: 2, status: 'Draft', postedAt: null, approvalStatus: 'InApproval', approvalStep: 'Level 2' }),
        row({ documentId: 3 }),
      ]),
    );

    expect(rows.slice(0, 3).map((r) => r.approval)).toEqual(['Approved', 'In approval · Level 2', null]);
    expect(rows[0].approvalTag).toBe('tag-success');
    expect(rows[1].approvalTag).toBe('tag-warning');
  });

  it('BT-20: a document in another currency carries its own total, and one in the branch’s does not', () => {
    const rows = billsTrackRows(result([row({ currencyCode: 'USD', totalInCurrency: 100, total: 8000 }), row({ documentId: 2, currencyCode: 'INR' })]));

    expect(rows[0].ownTotal).toBe(100);
    expect(rows[1].ownTotal).toBeNull();
  });

  it('BT-21: with no bills there are no rows, not even the totals', () => {
    expect(billsTrackRows(result([]))).toEqual([]);
  });

  it('BT-22: the next page is added to the ones loaded and the counts and totals are the latest answer’s', () => {
    const first = result([row({ documentId: 1 })], { totalBills: 3, pageNumber: 1 });
    const next = result([row({ documentId: 2 })], { totalBills: 3, pageNumber: 2, totals: { total: 9, paid: 9, due: 9 } });

    const merged = appendBillsTrackPage(first, next);

    expect(merged.bills.map((r) => r.documentId)).toEqual([1, 2]);
    expect(merged.pageNumber).toBe(2);
    expect(merged.totals.total).toBe(9);
  });

  it('BT-23: there is more while fewer bills are loaded than match', () => {
    expect(hasMoreBillsTrack({ bills: [row()], totalBills: 2 })).toBe(true);
    expect(hasMoreBillsTrack({ bills: [row(), row({ documentId: 2 })], totalBills: 2 })).toBe(false);
  });

  it('BT-24: the sentence says how many bills, and what the posted ones come to, with what is paid and still owed', () => {
    const answer = result([row({ total: 100, paid: 40, due: 60 }), row({ documentId: 2, total: 200, paid: 0, due: 200 })]);

    expect(billsTrackSentence(answer, money)).toBe('2 bills. Posted: ₹300.00, of which ₹40.00 is paid and ₹260.00 is still owed to vendors.');
    expect(billsTrackSentence(result([row()]), money)).toMatch(/^1 bill\. Posted: /);
  });
});
