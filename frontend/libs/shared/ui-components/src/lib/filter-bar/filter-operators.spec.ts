import { describe, expect, it } from 'vitest';
import { arityOf, inputTypeFor, operatorsFor } from './filter-operators';

/**
 * Which operators each column type offers.
 *
 * The wrong answers here are quiet ones: `Contains` on a date produces a filter
 * the server refuses, and `GreaterThan` on text produces one it accepts and
 * answers strangely — "Cash" > "Bank" is true alphabetically, which is not what
 * anybody meant by it.
 */
describe('filter operators', () => {
  it('offers text operators to text columns and not comparisons', () => {
    const operators = operatorsFor('Text');

    expect(operators).toContain('Contains');
    expect(operators).toContain('StartsWith');
    expect(operators).not.toContain('GreaterThan');
    expect(operators).not.toContain('Between');
  });

  it('offers comparisons to numbers and dates and not text matching', () => {
    for (const type of ['Money', 'Quantity', 'Date', 'DateTime'] as const) {
      const operators = operatorsFor(type);

      expect(operators).toContain('Between');
      expect(operators).toContain('GreaterOrEqual');
      expect(operators).not.toContain('Contains');
    }
  });

  it('offers a multi-select to enums rather than free text', () => {
    expect(operatorsFor('Enum')).toContain('In');
    expect(operatorsFor('Enum')).not.toContain('Contains');
  });

  it('gives booleans only what a tri-state can express', () => {
    expect(operatorsFor('Boolean')).toEqual(['Equals', 'IsNull', 'IsNotNull']);
  });

  it('every column type offers the empty checks', () => {
    for (const type of ['Text', 'Money', 'Date', 'Enum', 'Boolean'] as const) {
      expect(operatorsFor(type)).toContain('IsNull');
      expect(operatorsFor(type)).toContain('IsNotNull');
    }
  });

  it('knows how many values each operator needs', () => {
    expect(arityOf('IsNull')).toBe('none');
    expect(arityOf('IsNotNull')).toBe('none');
    expect(arityOf('Between')).toBe('two');
    expect(arityOf('In')).toBe('list');
    expect(arityOf('NotIn')).toBe('list');
    expect(arityOf('Contains')).toBe('one');
    expect(arityOf('GreaterThan')).toBe('one');
  });

  it('gives a date column a date picker rather than a text box', () => {
    // A typed date is a date typed in somebody's local format, and the wire
    // format is not a locale.
    expect(inputTypeFor('Date')).toBe('date');
    expect(inputTypeFor('DateTime')).toBe('datetime-local');
    expect(inputTypeFor('Money')).toBe('number');
    expect(inputTypeFor('Text')).toBe('text');
  });
});
