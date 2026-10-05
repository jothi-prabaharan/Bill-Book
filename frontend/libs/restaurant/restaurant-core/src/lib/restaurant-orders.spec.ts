import { describe, expect, it } from 'vitest';
import { OrderingItem } from './restaurant-menu';
import { addsInOneTap, choiceProblem, linesRequest, orderPlace, orderProblem, pendingPrice, pendingTotal, requiredFields } from './restaurant-orders';
import { RequirementLevel, defaultRestaurantSettings } from './restaurant-settings';

const settings = (patch = {}) => ({ ...defaultRestaurantSettings(), ...patch });
const details = { orderType: 'DineIn' as const, restaurantTableId: null, waiterUserId: null, contactId: null };

describe('restaurant orders', () => {
  it('saves a takeaway with no table even when dine-in needs one', () => {
    const s = settings({ tablesEnabled: true, tableRequired: RequirementLevel.DineInOnly });
    expect(orderProblem(s, { ...details, orderType: 'Takeaway' })).toBeNull();
    expect(orderProblem(s, details)).toBe('Choose a table for this dine-in order.');
  });

  it('asks for no table while tables are off, whatever was saved', () => {
    const s = settings({ tablesEnabled: false, tableRequired: RequirementLevel.Always });
    expect(requiredFields(s, 'DineIn').table).toBe(false);
    expect(orderProblem(s, details)).toBeNull();
  });

  it('asks for a customer and a waiter when the settings say so', () => {
    expect(orderProblem(settings({ customerRequired: RequirementLevel.Always }), details)).toBe('Choose the customer for this order.');
    expect(orderProblem(settings({ waiterRequired: true }), details)).toBe('Choose the waiter taking this order.');
    expect(orderProblem(settings({ waiterRequired: true }), { ...details, waiterUserId: 'u1' })).toBeNull();
  });

  const curry: OrderingItem = {
    itemId: 7,
    name: 'Paneer curry',
    station: 'Kitchen',
    isAvailable: true,
    price: null,
    variants: [
      { itemVariantId: 1, name: 'Half', price: 120 },
      { itemVariantId: 2, name: 'Full', price: 220 },
    ],
    modifierGroups: [
      {
        modifierGroupId: 5,
        name: 'Spice level',
        minChoices: 1,
        maxChoices: 1,
        isRequired: true,
        modifiers: [
          { modifierId: 50, name: 'Mild', priceChange: 0, linkedItemId: null, linkedQuantity: null, isAvailable: true },
          { modifierId: 51, name: 'Extra butter', priceChange: 25, linkedItemId: null, linkedQuantity: null, isAvailable: true },
        ],
      },
    ],
  };

  it('checks choices against each group', () => {
    expect(choiceProblem(curry.modifierGroups, [])).toBe('Choose a spice level.');
    expect(choiceProblem(curry.modifierGroups, [50, 51])).toBe('Choose only one spice level.');
    expect(choiceProblem(curry.modifierGroups, [50])).toBeNull();
  });

  it('prices a portion with its choices and adds a plain dish in one tap', () => {
    expect(pendingPrice(curry, curry.variants[0], [51])).toBe(145);
    expect(addsInOneTap(curry)).toBe(false);
    expect(addsInOneTap({ ...curry, variants: [], modifierGroups: [], price: 90 })).toBe(true);
  });

  it('totals pending lines and sends them without prices', () => {
    const line = { key: 1, itemId: 7, name: 'x', itemVariantId: 1, variantName: 'Half', modifierIds: [50], modifierNames: ['Mild'], quantity: 2, unitPrice: 120, note: ' no onion ' };
    expect(pendingTotal([line, { ...line, key: 2, quantity: 1, unitPrice: 30.5 }])).toBe(270.5);
    expect(linesRequest([line])).toEqual([{ itemId: 7, itemVariantId: 1, modifierIds: [50], quantity: 2, note: 'no onion' }]);
  });

  it('calls an order by its table or its type', () => {
    expect(orderPlace({ orderType: 'DineIn', tableCode: 'T4' })).toBe('T4');
    expect(orderPlace({ orderType: 'Takeaway', tableCode: null })).toBe('Takeaway');
  });
});
