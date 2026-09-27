import { describe, expect, it } from 'vitest';
import { columnTotals, monthLabels, rowTotal, spreadByShape, spreadEvenly, yearLabel } from './budget-math';

describe('budget math', () => {
  it('spreads a year total evenly and adds back exactly', () => {
    const months = spreadEvenly(1000);
    expect(months).toHaveLength(12);
    expect(months.slice(0, 4)).toEqual([83.34, 83.34, 83.34, 83.34]);
    expect(months[4]).toBe(83.33);
    expect(rowTotal(months)).toBe(1000);
  });

  it('spreads by last year shape, rounding into the largest month', () => {
    const shape = [0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1];
    const months = spreadByShape(100, shape);
    expect(months.slice(0, 9)).toEqual([0, 0, 0, 0, 0, 0, 0, 0, 0]);
    expect(rowTotal(months)).toBe(100);
    expect(months[9]).toBe(33.34);
  });

  it('spreads evenly when last year had nothing', () => {
    expect(spreadByShape(120, new Array(12).fill(0))).toEqual(new Array(12).fill(10));
  });

  it('never spreads a negative total', () => {
    expect(rowTotal(spreadEvenly(-50))).toBe(0);
  });

  it('totals columns without float drift', () => {
    const totals = columnTotals([
      [0.1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
      [0.2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1],
    ]);
    expect(totals.months[0]).toBe(0.3);
    expect(totals.total).toBe(1.3);
  });

  it('labels the months and the year from an April start', () => {
    const labels = monthLabels('2026-04-01');
    expect(labels).toHaveLength(12);
    expect(labels[0]).toContain('2026');
    expect(labels[11]).toContain('2027');
    expect(yearLabel('2026-04-01')).toBe('FY 2026-27');
    expect(yearLabel('2026-01-01')).toBe('FY 2026');
  });
});
