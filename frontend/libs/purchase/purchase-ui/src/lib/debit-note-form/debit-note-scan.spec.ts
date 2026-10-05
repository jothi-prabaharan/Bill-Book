import { describe, expect, it } from 'vitest';
import { debitScanned } from './debit-note-scan';

const line = (itemId: number, available: number, quantity = 0) => ({ itemId, available, quantity, billDetailId: itemId });

describe('debitScanned (TK-162)', () => {
  it('sends one more unit back on the bill line for the scanned item', () => {
    const { lines, outcome } = debitScanned([line(5, 3), line(6, 2)], 5);

    expect(outcome).toBe('added');
    expect(lines.map((l) => l.quantity)).toEqual([1, 0]);
  });

  it('stops at what the line still has to return, then moves to the next line for the item', () => {
    let result = debitScanned([line(5, 1), line(5, 2)], 5);
    result = debitScanned(result.lines, 5);
    result = debitScanned(result.lines, 5);
    expect(result.lines.map((l) => l.quantity)).toEqual([1, 2]);

    const full = debitScanned(result.lines, 5);
    expect(full.outcome).toBe('full');
  });

  it('never adds a line for an item the bill does not have', () => {
    const { lines, outcome } = debitScanned([line(5, 3)], 9);

    expect(outcome).toBe('notOnBill');
    expect(lines).toHaveLength(1);
  });
});
