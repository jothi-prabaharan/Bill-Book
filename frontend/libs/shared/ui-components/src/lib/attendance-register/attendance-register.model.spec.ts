import { describe, expect, it } from 'vitest';
import { RegisterStatus, countByStatus, moveFocus, nextStatus, statusForKey } from './attendance-register.model';

type Mark = 'Present' | 'Absent' | 'Late';

const marks: RegisterStatus<Mark>[] = [
  { value: 'Present', label: 'Present', key: 'P', glyph: 'P', tone: 'good' },
  { value: 'Absent', label: 'Absent', key: 'A', glyph: 'A', tone: 'bad' },
  { value: 'Late', label: 'Late', key: 'L', glyph: 'L', tone: 'warn' },
];

/** The register's rules (TK-63): what a tap, a key and an arrow do. */
describe('attendance register', () => {
  it('REG-01: a tap moves to the next mark and wraps round', () => {
    expect(nextStatus<Mark>('Present', marks)).toBe('Absent');
    expect(nextStatus<Mark>('Late', marks)).toBe('Present');
  });

  it('REG-02: a mark the list does not know starts again at the first', () => {
    expect(nextStatus('Holiday' as Mark, marks)).toBe('Present');
  });

  it('REG-03: a key sets its mark, in either case, and other keys mean nothing', () => {
    expect(statusForKey('a', marks)).toBe('Absent');
    expect(statusForKey('L', marks)).toBe('Late');
    expect(statusForKey('x', marks)).toBeNull();
    expect(statusForKey('Enter', marks)).toBeNull();
  });

  it('REG-04: the counts cover every mark, including those nobody has', () => {
    const counts = countByStatus<Mark>(
      [
        { id: 1, label: 'Arun', status: 'Present' },
        { id: 2, label: 'Meera', status: 'Present' },
        { id: 3, label: 'Priya', status: 'Absent' },
      ],
      marks,
    );

    expect(counts.map((c) => [c.status.value, c.count])).toEqual([
      ['Present', 2],
      ['Absent', 1],
      ['Late', 0],
    ]);
  });

  it('REG-05: arrows move across and down a grid, stopping at the edges', () => {
    expect(moveFocus(0, 'ArrowRight', 10, 4)).toBe(1);
    expect(moveFocus(1, 'ArrowDown', 10, 4)).toBe(5);
    expect(moveFocus(0, 'ArrowLeft', 10, 4)).toBe(0);
    expect(moveFocus(8, 'ArrowDown', 10, 4)).toBe(8);
    expect(moveFocus(5, 'Home', 10, 4)).toBe(0);
    expect(moveFocus(5, 'End', 10, 4)).toBe(9);
    expect(moveFocus(5, 'p', 10, 4)).toBeNull();
  });
});
