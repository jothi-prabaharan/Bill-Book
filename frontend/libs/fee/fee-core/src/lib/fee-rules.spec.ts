import { describe, expect, it } from 'vitest';
import { allocationProblem, autoAllocate, isDue, periodOf, unallocated } from './fee-rules';
import { FeeDemand } from './fee.models';

const demand = (id: number, open: number): FeeDemand => ({
  feeDemandId: id,
  demandNo: `FDM/2627/0000${id}`,
  studentId: 1,
  enrolmentId: 1,
  feeStructureId: 1,
  periodKey: '2026-06',
  contactId: 501,
  demandDate: '2026-06-01',
  dueDate: '2026-06-10',
  documentStatus: 'Posted',
  currencyCode: 'INR',
  totalAmount: open,
  concessionAmount: 0,
  netAmount: open,
  paidAmount: 0,
  openAmount: open,
  lines: [],
});

describe('fee rules (TK-64)', () => {
  it('fall due by frequency from the first month, as the server counts', () => {
    expect(isDue('Monthly', 6, 9)).toBe(true);
    expect(isDue('Quarterly', 6, 9)).toBe(true);
    expect(isDue('Quarterly', 6, 10)).toBe(false);
    expect(isDue('Termly', 6, 2)).toBe(true);
    expect(isDue('OneTime', 6, 7)).toBe(false);
  });

  it('settle the oldest demands first and keep the rest as an advance', () => {
    const allocations = autoAllocate(4000, [demand(1, 3100), demand(2, 2500)]);
    expect(allocations).toEqual([
      { feeDemandId: 1, amount: 3100 },
      { feeDemandId: 2, amount: 900 },
    ]);
    expect(unallocated(5000, [{ feeDemandId: 1, amount: 3100 }])).toBe(1900);
  });

  it('refuse an allocation above what is owed or above what was paid', () => {
    const open = [demand(1, 3100), demand(2, 2500)];
    expect(allocationProblem(4000, [{ feeDemandId: 1, amount: 3200 }], open)).not.toBeNull();
    expect(allocationProblem(1000, [{ feeDemandId: 1, amount: 600 }, { feeDemandId: 2, amount: 600 }], open)).not.toBeNull();
    expect(allocationProblem(4000, [{ feeDemandId: 1, amount: 3100 }], open)).toBeNull();
  });

  it('name a period by its year and month', () => {
    expect(periodOf('2026-06-15')).toBe('2026-06');
  });
});
