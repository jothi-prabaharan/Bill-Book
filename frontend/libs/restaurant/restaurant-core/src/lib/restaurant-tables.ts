/**
 * Floors and tables (TK-145): the shapes Sales's `api/restaurant` floor plan
 * returns, and the pure rules the floor plan page reads. `nextStatuses`
 * mirrors `RestaurantTableRulesRepository.CanChange` on the server.
 */

export const RESTAURANT_API = '/api/restaurant';

export type TableStatus = 'Free' | 'Occupied' | 'Billed' | 'Cleaning';

export interface RestaurantTable {
  restaurantTableId: number;
  floorId: number;
  code: string;
  seats: number;
  status: TableStatus;
  mergedIntoTableId: number | null;
  mergedIntoCode: string | null;
  mergedCodes: string[];
  isActive: boolean;
  openOrderId: number | null;
}

export interface Floor {
  floorId: number;
  name: string;
  displayOrder: number;
  isActive: boolean;
  tables: RestaurantTable[];
}

export interface FloorPlan {
  floors: Floor[];
}

const TRANSITIONS: Record<TableStatus, TableStatus[]> = {
  Free: ['Occupied', 'Cleaning'],
  Occupied: ['Billed', 'Free'],
  Billed: ['Occupied', 'Cleaning', 'Free'],
  Cleaning: ['Free'],
};

/** The statuses a table can move to next. A merged table has none: its head decides. */
export function nextStatuses(table: RestaurantTable): TableStatus[] {
  return table.mergedIntoTableId == null ? TRANSITIONS[table.status] : [];
}

/** The button for moving a table to a status, worded for the person tapping it. */
export function statusAction(to: TableStatus, from: TableStatus): string {
  switch (to) {
    case 'Occupied':
      return from === 'Billed' ? 'Reopen' : 'Seat guests';
    case 'Billed':
      return 'Mark billed';
    case 'Cleaning':
      return 'Clear table';
    default:
      return from === 'Occupied' ? 'Guests left' : 'Ready';
  }
}

export function statusLabel(status: TableStatus): string {
  return status === 'Cleaning' ? 'Being cleaned' : status;
}

/** Tables a group may be joined with or moved to: active, standing alone, and in the state each action needs. */
export function moveTargets(plan: FloorPlan, from: RestaurantTable): RestaurantTable[] {
  return allTables(plan).filter(
    (t) => t.restaurantTableId !== from.restaurantTableId && t.isActive && t.status === 'Free' && t.mergedIntoTableId == null && t.mergedCodes.length === 0,
  );
}

export function mergeCandidates(plan: FloorPlan, head: RestaurantTable): RestaurantTable[] {
  return allTables(plan).filter(
    (t) =>
      t.restaurantTableId !== head.restaurantTableId &&
      t.isActive &&
      (t.status === 'Free' || t.status === 'Occupied') &&
      t.mergedIntoTableId == null &&
      t.mergedCodes.length === 0,
  );
}

export function allTables(plan: FloorPlan): RestaurantTable[] {
  return plan.floors.flatMap((f) => f.tables);
}

/** "12 free of 20" for a floor's heading. */
export function floorSummary(floor: Floor): string {
  const active = floor.tables.filter((t) => t.isActive);
  const free = active.filter((t) => t.status === 'Free').length;
  return `${free} free of ${active.length}`;
}
