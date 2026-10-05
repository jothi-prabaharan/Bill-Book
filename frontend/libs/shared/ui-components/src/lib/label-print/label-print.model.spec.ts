import { describe, expect, it } from 'vitest';
import { LabelTemplate, copiesForQuantity, sizeLabel, templatesFor, totalCopies } from './label-print.model';

const template = (over: Partial<LabelTemplate>): LabelTemplate => ({
  labelTemplateId: 1,
  kind: 'PriceTag',
  name: 'Price tag',
  widthMm: 50,
  heightMm: 25,
  gapMm: 2,
  columns: 1,
  columnGapMm: 0,
  isDefault: false,
  ...over,
});

describe('label printing (TK-159)', () => {
  it('counts every copy, and a missing count as one', () => {
    expect(totalCopies([{ name: 'A', copies: 3 }, { name: 'B', copies: 0 }])).toBe(4);
  });

  it('reads a size the way a person says it', () => {
    expect(sizeLabel(template({}))).toBe('50 × 25 mm');
    expect(sizeLabel(template({ widthMm: 38, heightMm: 25, columns: 2 }))).toBe('38 × 25 mm, 2 across');
  });

  it('lists a kind’s templates with its default first', () => {
    const list = templatesFor(
      [template({ labelTemplateId: 1 }), template({ labelTemplateId: 2, isDefault: true }), template({ labelTemplateId: 3, kind: 'ShelfLabel' })],
      'PriceTag',
    );
    expect(list.map((t) => t.labelTemplateId)).toEqual([2, 1]);
  });

  it('prints one label per whole unit received, one for a fraction', () => {
    expect(copiesForQuantity(12)).toBe(12);
    expect(copiesForQuantity(2.5)).toBe(1);
    expect(copiesForQuantity(0)).toBe(1);
    expect(copiesForQuantity(null)).toBe(1);
  });
});
