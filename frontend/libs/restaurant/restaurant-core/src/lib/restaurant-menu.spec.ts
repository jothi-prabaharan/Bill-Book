import { describe, expect, it } from 'vitest';
import {
  EVERY_DAY,
  MenuItem,
  daysLabel,
  daysValue,
  emptyGroup,
  emptyMenuItem,
  halfAndFull,
  hoursLabel,
  menuItemProblem,
  menuItemRequest,
  priceSummary,
  scheduleProblem,
  scheduleRequest,
} from './restaurant-menu';

const dish = () => ({ ...emptyMenuItem(3), itemId: 42 });

describe('restaurant menu', () => {
  it('needs an item and a category', () => {
    expect(menuItemProblem(emptyMenuItem())).toBe('Choose the item this dish is.');
    expect(menuItemProblem({ ...dish(), menuCategoryId: null })).toBe("Choose the dish's category.");
    expect(menuItemProblem(dish())).toBeNull();
  });

  it('prices a half at half the full price and half the recipe', () => {
    const [half, full] = halfAndFull(180);
    expect(half).toMatchObject({ name: 'Half', price: 90, portionFactor: 0.5 });
    expect(full).toMatchObject({ name: 'Full', price: 180, portionFactor: 1 });
  });

  it('refuses two portions with one name, whatever the case', () => {
    expect(menuItemProblem({ ...dish(), variants: [...halfAndFull(100), { itemVariantId: null, name: 'half', price: 1, portionFactor: 1 }] })).toBe(
      'Two portions are named half.',
    );
  });

  it('refuses a dish-level price once the dish has portions', () => {
    const draft = { ...dish(), variants: halfAndFull(100), prices: [{ orderType: 'Takeaway' as const, variantName: '', price: 120 }] };
    expect(menuItemProblem(draft)).toContain('price each portion');
  });

  it('refuses a price for a portion the dish does not have, and two prices for one', () => {
    const variants = halfAndFull(100);
    expect(menuItemProblem({ ...dish(), variants, prices: [{ orderType: 'Delivery', variantName: 'Quarter', price: 40 }] })).toContain('Quarter');
    expect(
      menuItemProblem({
        ...dish(),
        variants,
        prices: [
          { orderType: 'Delivery', variantName: 'Half', price: 60 },
          { orderType: 'Delivery', variantName: 'half', price: 65 },
        ],
      }),
    ).toContain('two delivery prices');
  });

  it('refuses a group that asks for more than it offers', () => {
    const group = { ...emptyGroup(), name: 'Extras', minChoices: 2, maxChoices: 3 };
    group.modifiers[0].name = 'Cheese';
    expect(menuItemProblem({ ...dish(), groups: [group] })).toBe('Extras asks for 2 choices but offers 1.');
    expect(menuItemProblem({ ...dish(), groups: [{ ...group, minChoices: 4 }] })).toContain('minimum cannot be more');
  });

  it('sends portion names for prices and null for a dish-level one', () => {
    const body = menuItemRequest({ ...dish(), variants: halfAndFull(100), prices: [{ orderType: 'Takeaway', variantName: ' Full ', price: 110 }] });
    expect(body.prices).toEqual([{ orderType: 'Takeaway', variantName: 'Full', price: 110 }]);
    expect(menuItemRequest({ ...dish(), prices: [{ orderType: 'Delivery', variantName: '', price: 99 }] }).prices[0].variantName).toBeNull();
  });

  it('reads days by name or by number', () => {
    expect(daysValue('Weekdays')).toBe(31);
    expect(daysValue('Monday, Friday')).toBe(17);
    expect(daysValue(96)).toBe(96);
    expect(daysLabel('Everyday')).toBe('Every day');
    expect(daysLabel(17)).toBe('Mon, Fri');
  });

  it('labels hours running past midnight', () => {
    expect(hoursLabel('07:00:00', '11:00:00')).toBe('07:00–11:00');
    expect(hoursLabel('22:00:00', '02:00:00')).toBe('22:00–02:00 (next day)');
  });

  it('checks and sends a timed menu', () => {
    const draft = { name: 'Breakfast', days: EVERY_DAY, startTime: '07:00', endTime: '11:00', isActive: true };
    expect(scheduleProblem(draft)).toBeNull();
    expect(scheduleProblem({ ...draft, days: 0 })).toBe('Choose at least one day.');
    expect(scheduleProblem({ ...draft, endTime: '07:00' })).toContain('different times');
    expect(scheduleRequest(draft)).toMatchObject({ startTime: '07:00:00', endTime: '11:00:00', days: 127 });
  });

  it('summarises a price by portion', () => {
    const money = (v: number) => `₹${v}`;
    const item = { variants: [{ name: 'Half', price: 90 }, { name: 'Full', price: 180 }], basePrice: 180 } as unknown as MenuItem;
    expect(priceSummary(item, money)).toBe('Half ₹90 · Full ₹180');
    expect(priceSummary({ ...item, variants: [], basePrice: null }, money)).toBe('No price');
  });
});
