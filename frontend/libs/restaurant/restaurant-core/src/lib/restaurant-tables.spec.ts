import { describe, expect, it } from 'vitest';
import { FloorPlan, RestaurantTable, floorSummary, mergeCandidates, moveTargets, nextStatuses, statusAction } from './restaurant-tables';

const table = (id: number, status: RestaurantTable['status'], extra: Partial<RestaurantTable> = {}): RestaurantTable => ({
  restaurantTableId: id,
  floorId: 1,
  code: `T${id}`,
  seats: 4,
  status,
  mergedIntoTableId: null,
  mergedIntoCode: null,
  mergedCodes: [],
  isActive: true,
  openOrderId: null,
  ...extra,
});

describe('restaurant tables', () => {
  it('follows the turn: free, occupied, billed, cleaning, free', () => {
    expect(nextStatuses(table(1, 'Free'))).toEqual(['Occupied', 'Cleaning']);
    expect(nextStatuses(table(1, 'Occupied'))).toEqual(['Billed', 'Free']);
    expect(nextStatuses(table(1, 'Billed'))).toEqual(['Occupied', 'Cleaning', 'Free']);
    expect(nextStatuses(table(1, 'Cleaning'))).toEqual(['Free']);
  });

  it('gives a merged table no actions of its own', () => {
    expect(nextStatuses(table(2, 'Occupied', { mergedIntoTableId: 1 }))).toEqual([]);
  });

  it('words each action for the moment', () => {
    expect(statusAction('Occupied', 'Free')).toBe('Seat guests');
    expect(statusAction('Occupied', 'Billed')).toBe('Reopen');
    expect(statusAction('Free', 'Occupied')).toBe('Guests left');
  });

  it('moves only to a free table standing alone', () => {
    const plan: FloorPlan = {
      floors: [
        {
          floorId: 1,
          name: 'Main',
          displayOrder: 10,
          isActive: true,
          tables: [
            table(1, 'Occupied'),
            table(2, 'Free'),
            table(3, 'Free', { mergedCodes: ['T4'] }),
            table(4, 'Free', { mergedIntoTableId: 3 }),
            table(5, 'Free', { isActive: false }),
            table(6, 'Occupied'),
          ],
        },
      ],
    };

    expect(moveTargets(plan, plan.floors[0].tables[0]).map((t) => t.code)).toEqual(['T2']);
    expect(mergeCandidates(plan, plan.floors[0].tables[0]).map((t) => t.code)).toEqual(['T2', 'T6']);
    expect(floorSummary(plan.floors[0])).toBe('3 free of 5');
  });
});
