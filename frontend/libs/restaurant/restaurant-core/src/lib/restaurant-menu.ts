/**
 * The restaurant menu (TK-144): the shapes Inventory's `api/restaurant/menu`
 * returns and saves, and the pure helpers the menu page and the order screen use.
 */

import { OrderType } from './restaurant-settings';

export const RESTAURANT_MENU_API = '/api/restaurant/menu';

export type MenuStation = 'Kitchen' | 'Bar' | 'Counter';

export const ORDER_TYPES: readonly { value: OrderType; label: string }[] = [
  { value: 'DineIn', label: 'Dine-in' },
  { value: 'Takeaway', label: 'Takeaway' },
  { value: 'Delivery', label: 'Delivery' },
];

export const STATIONS: readonly { value: MenuStation; label: string }[] = [
  { value: 'Kitchen', label: 'Kitchen' },
  { value: 'Bar', label: 'Bar' },
  { value: 'Counter', label: 'Counter' },
];

/** The days a timed menu runs on, as the server's flags: Monday is 1, Sunday 64. */
export const MENU_DAYS: readonly { flag: number; label: string; short: string }[] = [
  { flag: 1, label: 'Monday', short: 'Mon' },
  { flag: 2, label: 'Tuesday', short: 'Tue' },
  { flag: 4, label: 'Wednesday', short: 'Wed' },
  { flag: 8, label: 'Thursday', short: 'Thu' },
  { flag: 16, label: 'Friday', short: 'Fri' },
  { flag: 32, label: 'Saturday', short: 'Sat' },
  { flag: 64, label: 'Sunday', short: 'Sun' },
];

export const EVERY_DAY = 127;
export const WEEKDAYS = 31;
export const WEEKEND = 96;

export interface MenuCategory {
  menuCategoryId: number;
  name: string;
  displayOrder: number;
  isActive: boolean;
  itemCount: number;
}

export interface MenuSchedule {
  menuScheduleId: number;
  name: string;
  /** The server sends the flags enum by name ("Weekdays", "Monday, Friday") or as a number. */
  days: number | string;
  startTime: string;
  endTime: string;
  isActive: boolean;
}

export interface ItemVariant {
  itemVariantId: number;
  name: string;
  price: number;
  portionFactor: number;
}

export interface MenuItemPrice {
  orderType: OrderType;
  itemVariantId: number | null;
  variantName: string | null;
  price: number;
}

export interface Modifier {
  modifierId: number;
  name: string;
  priceChange: number;
  linkedItemId: number | null;
  linkedQuantity: number | null;
  isAvailable: boolean;
}

export interface ModifierGroup {
  modifierGroupId: number;
  name: string;
  minChoices: number;
  maxChoices: number;
  isRequired: boolean;
  modifiers: Modifier[];
}

export interface MenuItem {
  itemId: number;
  itemCode: string;
  itemName: string;
  basePrice: number | null;
  menuCategoryId: number;
  categoryName: string;
  menuScheduleId: number | null;
  scheduleName: string | null;
  station: MenuStation;
  isAvailable: boolean;
  displayOrder: number;
  variants: ItemVariant[];
  prices: MenuItemPrice[];
  modifierGroups: ModifierGroup[];
}

export interface OrderingVariant {
  itemVariantId: number;
  name: string;
  price: number;
}

export interface OrderingItem {
  itemId: number;
  name: string;
  station: MenuStation;
  isAvailable: boolean;
  price: number | null;
  variants: OrderingVariant[];
  modifierGroups: ModifierGroup[];
}

export interface OrderingCategory {
  menuCategoryId: number;
  name: string;
  items: OrderingItem[];
}

export interface OrderingMenu {
  orderType: OrderType;
  categories: OrderingCategory[];
}

// ---- Drafts: what the editor holds ----

export interface VariantDraft {
  itemVariantId: number | null;
  name: string;
  price: number | null;
  portionFactor: number | null;
}

export interface PriceDraft {
  orderType: OrderType;
  variantName: string;
  price: number | null;
}

export interface ModifierDraft {
  modifierId: number | null;
  name: string;
  priceChange: number | null;
  linkedItemId: number | null;
  linkedQuantity: number | null;
  isAvailable: boolean;
}

export interface GroupDraft {
  modifierGroupId: number | null;
  name: string;
  minChoices: number | null;
  maxChoices: number | null;
  modifiers: ModifierDraft[];
}

export interface MenuItemDraft {
  itemId: number | null;
  menuCategoryId: number | null;
  menuScheduleId: number | null;
  station: MenuStation;
  isAvailable: boolean;
  variants: VariantDraft[];
  prices: PriceDraft[];
  groups: GroupDraft[];
}

export interface ScheduleDraft {
  name: string;
  days: number;
  startTime: string;
  endTime: string;
  isActive: boolean;
}

export function emptyMenuItem(menuCategoryId: number | null = null): MenuItemDraft {
  return { itemId: null, menuCategoryId, menuScheduleId: null, station: 'Kitchen', isAvailable: true, variants: [], prices: [], groups: [] };
}

export function emptyVariant(name = ''): VariantDraft {
  return { itemVariantId: null, name, price: null, portionFactor: 1 };
}

export function emptyGroup(): GroupDraft {
  return { modifierGroupId: null, name: '', minChoices: 0, maxChoices: 1, modifiers: [emptyModifier()] };
}

export function emptyModifier(): ModifierDraft {
  return { modifierId: null, name: '', priceChange: 0, linkedItemId: null, linkedQuantity: null, isAvailable: true };
}

/** Half and Full, the usual pair: the half at half the full price and half the recipe. */
export function halfAndFull(fullPrice: number | null): VariantDraft[] {
  const half = fullPrice == null ? null : Math.round(fullPrice * 50) / 100;
  return [
    { itemVariantId: null, name: 'Half', price: half, portionFactor: 0.5 },
    { itemVariantId: null, name: 'Full', price: fullPrice, portionFactor: 1 },
  ];
}

export function menuItemDraftOf(item: MenuItem): MenuItemDraft {
  return {
    itemId: item.itemId,
    menuCategoryId: item.menuCategoryId,
    menuScheduleId: item.menuScheduleId,
    station: item.station,
    isAvailable: item.isAvailable,
    variants: item.variants.map((v) => ({ itemVariantId: v.itemVariantId, name: v.name, price: v.price, portionFactor: v.portionFactor })),
    prices: item.prices.map((p) => ({ orderType: p.orderType, variantName: p.variantName ?? '', price: p.price })),
    groups: item.modifierGroups.map((g) => ({
      modifierGroupId: g.modifierGroupId,
      name: g.name,
      minChoices: g.minChoices,
      maxChoices: g.maxChoices,
      modifiers: g.modifiers.map((m) => ({ ...m })),
    })),
  };
}

const sameName = (a: string, b: string) => a.trim().toLowerCase() === b.trim().toLowerCase();

function duplicate(names: string[]): string | null {
  const seen = new Set<string>();
  for (const name of names.map((n) => n.trim())) {
    const key = name.toLowerCase();
    if (seen.has(key)) return name;
    seen.add(key);
  }
  return null;
}

/** The first thing wrong with a dish, in the server's words, or null. */
export function menuItemProblem(draft: MenuItemDraft): string | null {
  if (!draft.itemId) return 'Choose the item this dish is.';
  if (!draft.menuCategoryId) return "Choose the dish's category.";

  for (const v of draft.variants) {
    if (!v.name.trim()) return "Enter each portion's name.";
    if (v.price == null || v.price < 0) return `Enter a price for ${v.name.trim()}.`;
    if (!v.portionFactor || v.portionFactor <= 0) return `${v.name.trim()}: a portion factor must be more than 0.`;
  }
  const variant = duplicate(draft.variants.map((v) => v.name));
  if (variant) return `Two portions are named ${variant}.`;

  const priced = new Set<string>();
  for (const p of draft.prices) {
    const name = p.variantName.trim();
    if (!name && draft.variants.length) return 'This dish is sold by portion, so price each portion for the order type instead.';
    if (name && !draft.variants.some((v) => sameName(v.name, name))) return `A price names the portion ${name}, which this dish does not have.`;
    if (p.price == null || p.price < 0) return 'Enter every order-type price.';
    const key = `${p.orderType}|${name.toLowerCase()}`;
    if (priced.has(key)) return `${name || 'The dish'} has two ${orderTypeLabel(p.orderType).toLowerCase()} prices.`;
    priced.add(key);
  }

  const group = duplicate(draft.groups.map((g) => g.name));
  if (group) return `Two choice groups are named ${group}.`;
  for (const g of draft.groups) {
    const name = g.name.trim();
    if (!name) return "Enter each choice group's name.";
    const min = g.minChoices ?? 0;
    const max = g.maxChoices ?? 0;
    if (max < 1) return `${name}: the maximum must be at least 1.`;
    if (min > max) return `${name}: the minimum cannot be more than the maximum.`;
    if (!g.modifiers.length) return `${name} has no choices in it.`;
    if (min > g.modifiers.length) return `${name} asks for ${min} choices but offers ${g.modifiers.length}.`;
    if (g.modifiers.some((m) => !m.name.trim())) return `${name}: enter each choice's name.`;
    const modifier = duplicate(g.modifiers.map((m) => m.name));
    if (modifier) return `${name} has two choices named ${modifier}.`;
    if (g.modifiers.some((m) => (m.linkedItemId == null) !== (m.linkedQuantity == null))) {
      return `${name}: a choice that uses up an item needs both the item and the quantity.`;
    }
  }

  return null;
}

/** The body for `POST`/`PUT api/restaurant/menu/items`. Call only once `menuItemProblem` is null. */
export function menuItemRequest(draft: MenuItemDraft) {
  return {
    itemId: draft.itemId,
    menuCategoryId: draft.menuCategoryId,
    menuScheduleId: draft.menuScheduleId,
    station: draft.station,
    isAvailable: draft.isAvailable,
    variants: draft.variants.map((v) => ({ itemVariantId: v.itemVariantId, name: v.name.trim(), price: v.price ?? 0, portionFactor: v.portionFactor ?? 1 })),
    prices: draft.prices.map((p) => ({ orderType: p.orderType, variantName: p.variantName.trim() || null, price: p.price ?? 0 })),
    modifierGroups: draft.groups.map((g) => ({
      modifierGroupId: g.modifierGroupId,
      name: g.name.trim(),
      minChoices: g.minChoices ?? 0,
      maxChoices: g.maxChoices ?? 1,
      modifiers: g.modifiers.map((m) => ({
        modifierId: m.modifierId,
        name: m.name.trim(),
        priceChange: m.priceChange ?? 0,
        linkedItemId: m.linkedItemId,
        linkedQuantity: m.linkedQuantity,
        isAvailable: m.isAvailable,
      })),
    })),
  };
}

export function orderTypeLabel(orderType: OrderType): string {
  return ORDER_TYPES.find((o) => o.value === orderType)?.label ?? orderType;
}

/** A schedule's days as a number, whichever way the server sent them. */
export function daysValue(days: number | string): number {
  if (typeof days === 'number') return days;
  const names: Record<string, number> = { everyday: EVERY_DAY, weekdays: WEEKDAYS, weekend: WEEKEND, none: 0 };
  for (const d of MENU_DAYS) names[d.label.toLowerCase()] = d.flag;
  return days
    .split(',')
    .map((part) => part.trim().toLowerCase())
    .reduce((sum, part) => sum | (names[part] ?? (Number(part) || 0)), 0);
}

/** "Every day", "Weekdays", "Weekend" or "Mon, Wed, Fri". */
export function daysLabel(days: number | string): string {
  const value = daysValue(days);
  if (value === EVERY_DAY) return 'Every day';
  if (value === WEEKDAYS) return 'Weekdays';
  if (value === WEEKEND) return 'Weekend';
  return MENU_DAYS.filter((d) => value & d.flag).map((d) => d.short).join(', ') || 'No days';
}

/** "07:00–11:00", the seconds dropped. An end before the start reads "22:00–02:00 (next day)". */
export function hoursLabel(start: string, end: string): string {
  const hhmm = (t: string) => t.slice(0, 5);
  return `${hhmm(start)}–${hhmm(end)}${hhmm(end) < hhmm(start) ? ' (next day)' : ''}`;
}

export function scheduleDraftOf(s: MenuSchedule): ScheduleDraft {
  return { name: s.name, days: daysValue(s.days), startTime: s.startTime.slice(0, 5), endTime: s.endTime.slice(0, 5), isActive: s.isActive };
}

export function scheduleProblem(draft: ScheduleDraft): string | null {
  if (!draft.name.trim()) return "Enter the timed menu's name.";
  if (!(draft.days & EVERY_DAY)) return 'Choose at least one day.';
  if (!/^\d{2}:\d{2}$/.test(draft.startTime) || !/^\d{2}:\d{2}$/.test(draft.endTime)) return 'Enter the start and end as hours and minutes.';
  if (draft.startTime === draft.endTime) return 'A timed menu must start and end at different times.';
  return null;
}

/** The body for a timed menu; times go as `HH:mm:00`, days as the number. */
export function scheduleRequest(draft: ScheduleDraft) {
  return { name: draft.name.trim(), days: draft.days, startTime: `${draft.startTime}:00`, endTime: `${draft.endTime}:00`, isActive: draft.isActive };
}

/** What a dish sells at, shortest first: "₹180" or "Half ₹100 · Full ₹180". */
export function priceSummary(item: MenuItem, money: (value: number) => string): string {
  if (item.variants.length) return item.variants.map((v) => `${v.name} ${money(v.price)}`).join(' · ');
  return item.basePrice == null ? 'No price' : money(item.basePrice);
}
