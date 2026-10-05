import { describe, expect, it } from 'vitest';
import { ScannableSheetLine, addSerial, batchFromScan, scanOntoSheet } from './stock-scan';

const blank = (): ScannableSheetLine => ({
  itemId: null,
  countedQuantity: null,
  quantity: null,
  batchNumber: null,
  batchExpiryDate: null,
});

describe('scanOntoSheet (TK-163)', () => {
  it('counting a shelf by scanning produces the scanned quantities', () => {
    let lines = [blank()];
    for (const itemId of [5, 5, 6, 5, 6]) {
      lines = scanOntoSheet(lines, itemId, true, null, blank);
    }

    expect(lines.map((l) => [l.itemId, l.countedQuantity])).toEqual([
      [5, 3],
      [6, 2],
    ]);
  });

  it('adds to the quantity, not the count, on a write-off', () => {
    const lines = scanOntoSheet(scanOntoSheet([blank()], 5, false, null, blank), 5, false, null, blank);

    expect(lines).toHaveLength(1);
    expect(lines[0].quantity).toBe(2);
    expect(lines[0].countedQuantity).toBeNull();
  });

  it('keeps each scanned batch on a line of its own', () => {
    let lines = scanOntoSheet([blank()], 5, true, { batch: 'B12', expiry: '2027-03-31' }, blank);
    lines = scanOntoSheet(lines, 5, true, { batch: 'B13', expiry: null }, blank);
    lines = scanOntoSheet(lines, 5, true, { batch: 'B12', expiry: '2027-03-31' }, blank);

    expect(lines.map((l) => [l.batchNumber, l.countedQuantity])).toEqual([
      ['B12', 2],
      ['B13', 1],
    ]);
    expect(lines[0].batchExpiryDate).toBe('2027-03-31');
  });

  it('a keyed count is added to, not replaced', () => {
    const keyed = { ...blank(), itemId: 5, countedQuantity: 10 };
    expect(scanOntoSheet([keyed], 5, true, null, blank)[0].countedQuantity).toBe(11);
  });
});

describe('batchFromScan', () => {
  it('reads the batch and expiry off a pack GS1 code', () => {
    expect(batchFromScan('(01)08901234567890(17)270331(10)B12')).toEqual({ batch: 'B12', expiry: '2027-03-31' });
  });

  it('takes a plain batch label as the batch itself', () => {
    expect(batchFromScan(' LOT-77 ')).toEqual({ batch: 'LOT-77', expiry: null });
  });
});

describe('addSerial', () => {
  it('adds each scanned serial once, one per line', () => {
    let result = addSerial('', 'S1');
    result = addSerial(result.text, 'S2');
    const again = addSerial(result.text, 'S1');

    expect(again.text).toBe('S1\nS2');
    expect(again.added).toBe(false);
    expect(again.count).toBe(2);
  });

  it('tidies blank lines a hand-typed list carries', () => {
    expect(addSerial('S1\n\n', 'S2')).toEqual({ text: 'S1\nS2', added: true, count: 2 });
  });
});
