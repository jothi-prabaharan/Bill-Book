import { describe, expect, it } from 'vitest';
import { mergeProjectItems, toBillingLines, UnbilledTimeItem } from './project-billing';

const hour = (timeEntryId: number, projectTaskId: number, hours: number, rate = 800): UnbilledTimeItem => ({
  timeEntryId,
  workDate: '2026-09-01',
  userId: 'u',
  projectTaskId,
  taskName: `Task ${projectTaskId}`,
  hours,
  notes: null,
  rate,
  amount: hours * rate,
});

describe('toBillingLines', () => {
  it('bills hours as one line per task and rate, naming every entry', () => {
    const { lines, items } = toBillingLines(7, { time: [hour(1, 10, 2), hour(2, 10, 1.5), hour(3, 11, 1)], expenses: [], bills: [], milestones: [] }, 400);

    expect(lines).toHaveLength(2);
    expect(lines[0]).toMatchObject({ quantity: 3.5, unitPrice: 800, projectId: 7, accountId: 400, itemId: null });
    expect(items.map((i) => i.itemId)).toEqual([1, 2, 3]);
  });

  it('charges an expense on at its billable amount, markup included', () => {
    const { lines, items } = toBillingLines(
      7,
      {
        time: [],
        expenses: [{ spendMoneyDetailId: 9, transactionDate: '2026-09-01', transactionNo: 'PAY/1', memo: 'Tiles', amount: 1000, markupPercent: 10, billableAmount: 1100 }],
        bills: [],
        milestones: [{ projectMilestoneId: 4, name: 'Design', amount: 5000, dueDate: null }],
      },
      null,
    );

    expect(lines.map((l) => l.unitPrice)).toEqual([1100, 5000]);
    expect(items).toEqual([
      { kind: 'Expense', itemId: 9, projectId: 7 },
      { kind: 'Milestone', itemId: 4, projectId: 7 },
    ]);
  });

  it('adds an item to the invoice only once', () => {
    expect(mergeProjectItems([{ kind: 'TimeEntry', itemId: 1 }], [{ kind: 'TimeEntry', itemId: 1 }, { kind: 'Expense', itemId: 1 }])).toHaveLength(2);
  });
});
