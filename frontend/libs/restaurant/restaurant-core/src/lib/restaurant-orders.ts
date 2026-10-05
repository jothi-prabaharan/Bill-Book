/**
 * Restaurant orders (TK-146): the shapes Sales's `api/restaurant/orders`
 * returns, the lines a waiter builds up before saving, and the pure checks
 * that mark what an order still needs. The server applies the same rules
 * (`RestaurantOrderRulesRepository`); these only say so sooner.
 */

import { MenuStation, ModifierGroup, OrderingItem, OrderingVariant } from './restaurant-menu';
import { OrderType, RestaurantSettingsView, isCustomerRequired, isTableRequired } from './restaurant-settings';

export const RESTAURANT_ORDERS_API = '/api/restaurant/orders';

export type RestaurantOrderStatus = 'Open' | 'Billed' | 'Cancelled' | 'Merged';

export type KitchenStatus = 'NotSent' | 'Sent' | 'Preparing' | 'Ready' | 'Served' | 'Cancelled';

export interface RestaurantOrderModifier {
  modifierId: number;
  name: string;
  priceChange: number;
}

export interface RestaurantOrderLine {
  restaurantOrderLineId: number;
  lineNo: number;
  itemId: number;
  itemName: string;
  itemVariantId: number | null;
  variantName: string | null;
  station: MenuStation;
  quantity: number;
  unitPrice: number;
  amount: number;
  note: string | null;
  kitchenStatus: KitchenStatus;
  cancelReason: string | null;
  modifiers: RestaurantOrderModifier[];
}

export interface RestaurantOrder {
  restaurantOrderId: number;
  orderNo: string;
  orderDate: string;
  openedAt: string;
  orderType: OrderType;
  restaurantTableId: number | null;
  tableCode: string | null;
  waiterUserId: string | null;
  waiterName: string | null;
  contactId: number | null;
  contactName: string | null;
  guests: number | null;
  status: RestaurantOrderStatus;
  notes: string | null;
  cancelReason: string | null;
  total: number;
  lineCount: number;
  unsentCount: number;
  lines: RestaurantOrderLine[];
}

export interface StaffMember {
  id: string;
  name: string;
}

/** A dish chosen but not yet saved to the order. */
export interface PendingLine {
  key: number;
  itemId: number;
  name: string;
  itemVariantId: number | null;
  variantName: string | null;
  modifierIds: number[];
  modifierNames: string[];
  quantity: number;
  /** What the menu showed; the server prices it again when it is saved. */
  unitPrice: number;
  note: string;
}

export interface OrderDetails {
  orderType: OrderType;
  restaurantTableId: number | null;
  waiterUserId: string | null;
  contactId: number | null;
}

/** The fields the effective settings require for this order, so the page can mark them. */
export function requiredFields(settings: RestaurantSettingsView, orderType: OrderType): { table: boolean; customer: boolean; waiter: boolean } {
  return {
    table: settings.tablesEnabled && isTableRequired(settings, orderType),
    customer: isCustomerRequired(settings, orderType),
    waiter: settings.waiterRequired,
  };
}

/** The first thing the order still needs before it can be saved, worded as the server words it, or null. */
export function orderProblem(settings: RestaurantSettingsView, order: OrderDetails): string | null {
  const required = requiredFields(settings, order.orderType);
  if (required.table && order.restaurantTableId == null) {
    return order.orderType === 'DineIn'
      ? 'Choose a table for this dine-in order.'
      : 'This branch takes every order against a table. Choose one, or take it as dine-in.';
  }
  if (required.customer && order.contactId == null) return 'Choose the customer for this order.';
  if (required.waiter && !order.waiterUserId) return 'Choose the waiter taking this order.';
  return null;
}

/** Why the choices on a dish are not yet a valid set, or null. Mirrors the menu's own check. */
export function choiceProblem(groups: ModifierGroup[], chosen: number[]): string | null {
  for (const group of groups) {
    const count = group.modifiers.filter((m) => chosen.includes(m.modifierId)).length;
    if (count < group.minChoices) {
      return group.minChoices === 1 ? `Choose a ${group.name.toLowerCase()}.` : `Choose at least ${group.minChoices} for ${group.name}.`;
    }
    if (count > group.maxChoices) {
      return group.maxChoices === 1 ? `Choose only one ${group.name.toLowerCase()}.` : `Choose at most ${group.maxChoices} for ${group.name}.`;
    }
  }
  return null;
}

/** Whether a dish can go straight onto the order with one tap: one price and nothing to choose. */
export function addsInOneTap(item: OrderingItem): boolean {
  return item.variants.length === 0 && item.modifierGroups.length === 0;
}

/** The unit price of a dish with a portion and choices, as the menu showed it. */
export function pendingPrice(item: OrderingItem, variant: OrderingVariant | null, chosen: number[]): number {
  const base = variant?.price ?? item.price ?? 0;
  const extras = item.modifierGroups.flatMap((g) => g.modifiers).filter((m) => chosen.includes(m.modifierId)).reduce((sum, m) => sum + m.priceChange, 0);
  return Math.round((base + extras) * 100) / 100;
}

export function pendingTotal(lines: PendingLine[]): number {
  return Math.round(lines.reduce((sum, l) => sum + l.quantity * l.unitPrice, 0) * 100) / 100;
}

/** The body for adding pending lines to an order. */
export function linesRequest(lines: PendingLine[]) {
  return lines.map((l) => ({ itemId: l.itemId, itemVariantId: l.itemVariantId, modifierIds: l.modifierIds, quantity: l.quantity, note: l.note.trim() || null }));
}

export function kitchenStatusLabel(status: KitchenStatus): string {
  return status === 'NotSent' ? 'Not sent' : status;
}

/** "T4", "Takeaway" or "Delivery": how an order is called out. */
export function orderPlace(order: Pick<RestaurantOrder, 'orderType' | 'tableCode'>): string {
  if (order.tableCode) return order.tableCode;
  return order.orderType === 'DineIn' ? 'Dine-in' : order.orderType;
}
