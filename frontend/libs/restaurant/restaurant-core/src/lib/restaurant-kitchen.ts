/**
 * Kitchen tickets and the kitchen display (TK-147): the shapes Sales returns
 * and the pure rules the screens read. `nextKitchenSteps` mirrors
 * `KotRulesRepository.CanMove` on the server.
 */

import { MenuStation } from './restaurant-menu';
import { KitchenStatus } from './restaurant-orders';
import { OrderType } from './restaurant-settings';

export interface KotLine {
  kotLineId: number;
  restaurantOrderLineId: number;
  itemName: string;
  variantName: string | null;
  quantity: number;
  note: string | null;
  modifiers: string[];
  status: KitchenStatus;
  cancelReason: string | null;
  statusChangedAt: string;
}

export interface Kot {
  kotId: number;
  kotNo: string;
  restaurantOrderId: number;
  orderNo: string;
  orderType: OrderType;
  tableCode: string | null;
  waiterName: string | null;
  station: MenuStation;
  sentAt: string;
  printedCount: number;
  lines: KotLine[];
}

export interface PrintedDocument {
  html: string;
  pageCount: number;
}

/** How often the kitchen display asks for new tickets. SignalR needs a backplane, so it polls for now. */
export const KITCHEN_POLL_MS = 10_000;

/** The next steps a dish can take, in the order a cook would press them. */
export function nextKitchenSteps(status: KitchenStatus): KitchenStatus[] {
  switch (status) {
    case 'Sent':
      return ['Preparing', 'Ready'];
    case 'Preparing':
      return ['Ready'];
    case 'Ready':
      return ['Served'];
    default:
      return [];
  }
}

/** Whether cancelling needs someone who may approve: the kitchen has started the dish. */
export function cancelNeedsApproval(status: KitchenStatus): boolean {
  return status === 'Preparing' || status === 'Ready';
}

export function canCancel(status: KitchenStatus): boolean {
  return status === 'Sent' || status === 'Preparing' || status === 'Ready';
}

/** "Now", "4 min" or "1 h 12 min" since a time, for a ticket's age on the display. */
export function minutesSince(iso: string, now: Date = new Date()): string {
  const minutes = Math.max(0, Math.floor((now.getTime() - new Date(iso).getTime()) / 60_000));
  if (minutes < 1) return 'Now';
  if (minutes < 60) return `${minutes} min`;
  return `${Math.floor(minutes / 60)} h ${minutes % 60} min`;
}

/** A ticket waiting longer than this is flagged on the display. */
export const LATE_MINUTES = 20;

export function isLate(iso: string, now: Date = new Date()): boolean {
  return now.getTime() - new Date(iso).getTime() >= LATE_MINUTES * 60_000;
}
