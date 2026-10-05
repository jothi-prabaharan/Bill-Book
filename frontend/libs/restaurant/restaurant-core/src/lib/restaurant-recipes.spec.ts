import { describe, expect, it } from 'vitest';
import { RecipeDraft, foodCostTone, recipeProblem, recipeRequest, wastageReasonLabel } from './restaurant-recipes';

const draft = (patch: Partial<RecipeDraft> = {}): RecipeDraft => ({
  yield: 1,
  notes: '',
  isActive: true,
  lines: [{ ingredientItemId: 5, ingredientName: 'Paneer', uomTypeId: 2, quantity: 0.15, uomId: 9 }],
  ...patch,
});

describe('recipes', () => {
  it('accepts a sound recipe', () => {
    expect(recipeProblem(draft(), 40)).toBeNull();
  });

  it('refuses no ingredients, a zero yield, the dish itself and a repeat', () => {
    expect(recipeProblem(draft({ lines: [] }), 40)).toBe('Add at least one ingredient.');
    expect(recipeProblem(draft({ yield: 0 }), 40)).toContain('more than 0 portions');
    expect(recipeProblem(draft(), 5)).toBe('A dish cannot be an ingredient of itself.');
    const line = draft().lines[0];
    expect(recipeProblem(draft({ lines: [line, { ...line }] }), 40)).toContain('listed twice');
  });

  it('asks for each quantity and unit', () => {
    const line = draft().lines[0];
    expect(recipeProblem(draft({ lines: [{ ...line, quantity: null }] }), 40)).toContain('how much Paneer');
    expect(recipeProblem(draft({ lines: [{ ...line, uomId: null }] }), 40)).toContain('unit for Paneer');
  });

  it('sends notes trimmed and the lines bare', () => {
    expect(recipeRequest(draft({ notes: '  pot of ten ', yield: 10 }))).toEqual({
      yield: 10,
      notes: 'pot of ten',
      isActive: true,
      lines: [{ ingredientItemId: 5, quantity: 0.15, uomId: 9 }],
    });
  });

  it('reads a food cost', () => {
    expect(foodCostTone(null)).toBe('none');
    expect(foodCostTone(30)).toBe('good');
    expect(foodCostTone(38)).toBe('watch');
    expect(foodCostTone(45)).toBe('high');
    expect(wastageReasonLabel('ReturnedByGuest')).toBe('Returned by a guest');
  });
});
