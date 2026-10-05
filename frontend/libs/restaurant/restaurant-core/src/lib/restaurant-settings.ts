/**
 * Settings › Restaurant (D-39, TK-143): the shape Master's
 * `api/restaurant/settings` returns and saves, and the pure rules the page and
 * every restaurant screen read it by. Mirrors `Shared.Kernel.Restaurant`.
 */

export enum RequirementLevel {
  Off = 'Off',
  DineInOnly = 'DineInOnly',
  Always = 'Always',
}

export type OrderType = 'DineIn' | 'Takeaway' | 'Delivery';

export interface RestaurantSettingsView {
  tablesEnabled: boolean;
  kitchenEnabled: boolean;
  recipesEnabled: boolean;
  tableRequired: RequirementLevel;
  kotRequired: boolean;
  customerRequired: RequirementLevel;
  waiterRequired: boolean;
  serviceChargePercent: number;
  tipsEnabled: boolean;
  /** Loyalty points a bill earns its customer per ₹100 (TK-166); 0 earns nothing. */
  loyaltyEarnRate: number;
}

export const RESTAURANT_SETTINGS_API = '/api/restaurant/settings';

/** The highest service charge the server accepts, as a percentage. */
export const MAX_SERVICE_CHARGE_PERCENT = 25;

/** The most loyalty points ₹100 can earn: a point per rupee (TK-166). */
export const MAX_LOYALTY_EARN_RATE = 100;

/** A fresh branch: billing only, nothing required. The same as the server's defaults. */
export function defaultRestaurantSettings(): RestaurantSettingsView {
  return {
    tablesEnabled: false,
    kitchenEnabled: false,
    recipesEnabled: false,
    tableRequired: RequirementLevel.Off,
    kotRequired: false,
    customerRequired: RequirementLevel.Off,
    waiterRequired: false,
    serviceChargePercent: 0,
    tipsEnabled: false,
    loyaltyEarnRate: 0,
  };
}

export type RestaurantPresetCode = 'billing' | 'counter-kitchen' | 'dine-in' | 'full-service';

export interface RestaurantPreset {
  code: RestaurantPresetCode;
  label: string;
  description: string;
  tablesEnabled: boolean;
  kitchenEnabled: boolean;
}

/**
 * The four ways a restaurant usually runs. A preset sets only the two switches
 * and the requirement each one implies; it leaves recipes, the service charge
 * and tips as they were.
 */
export const RESTAURANT_PRESETS: readonly RestaurantPreset[] = [
  { code: 'billing', label: 'Billing only', description: 'A counter that bills. No tables, no kitchen tickets.', tablesEnabled: false, kitchenEnabled: false },
  { code: 'counter-kitchen', label: 'Counter and kitchen', description: 'Orders at the counter, sent to the kitchen on a ticket.', tablesEnabled: false, kitchenEnabled: true },
  { code: 'dine-in', label: 'Dine-in, no kitchen tickets', description: 'Orders by table; the kitchen works from the order itself.', tablesEnabled: true, kitchenEnabled: false },
  { code: 'full-service', label: 'Full service', description: 'Orders by table, sent to the kitchen on a ticket.', tablesEnabled: true, kitchenEnabled: true },
];

export function applyPreset(settings: RestaurantSettingsView, preset: RestaurantPreset): RestaurantSettingsView {
  return effectiveSettings({
    ...settings,
    tablesEnabled: preset.tablesEnabled,
    kitchenEnabled: preset.kitchenEnabled,
    tableRequired: preset.tablesEnabled ? (settings.tableRequired === RequirementLevel.Off ? RequirementLevel.DineInOnly : settings.tableRequired) : RequirementLevel.Off,
    kotRequired: preset.kitchenEnabled ? settings.kotRequired : false,
  });
}

/** The preset the switches match, or null when the branch is set up some other way. */
export function matchingPreset(settings: RestaurantSettingsView): RestaurantPresetCode | null {
  return RESTAURANT_PRESETS.find((p) => p.tablesEnabled === settings.tablesEnabled && p.kitchenEnabled === settings.kitchenEnabled)?.code ?? null;
}

/**
 * What the settings mean in effect: a table cannot be required while tables
 * are off, nor a kitchen ticket while the kitchen is off. The server applies
 * the same rule (`RestaurantSettingsRules.Effective`).
 */
export function effectiveSettings(settings: RestaurantSettingsView): RestaurantSettingsView {
  return {
    ...settings,
    tableRequired: settings.tablesEnabled ? settings.tableRequired : RequirementLevel.Off,
    kotRequired: settings.kitchenEnabled ? settings.kotRequired : false,
  };
}

/** Whether an order of this type needs a table before it can be saved. */
export function isTableRequired(settings: RestaurantSettingsView, orderType: OrderType): boolean {
  return requires(effectiveSettings(settings).tableRequired, orderType);
}

/** Whether an order of this type needs a customer before it can be saved. */
export function isCustomerRequired(settings: RestaurantSettingsView, orderType: OrderType): boolean {
  return requires(settings.customerRequired, orderType);
}

function requires(level: RequirementLevel, orderType: OrderType): boolean {
  return level === RequirementLevel.Always || (level === RequirementLevel.DineInOnly && orderType === 'DineIn');
}

/** The first thing wrong with the settings, or null. The server says the same in its own words. */
export function settingsProblem(settings: RestaurantSettingsView): string | null {
  const charge = Number(settings.serviceChargePercent);
  if (!Number.isFinite(charge) || charge < 0 || charge > MAX_SERVICE_CHARGE_PERCENT) {
    return `The service charge must be between 0 and ${MAX_SERVICE_CHARGE_PERCENT}%.`;
  }

  const rate = Number(settings.loyaltyEarnRate ?? 0);
  if (!Number.isFinite(rate) || rate < 0 || rate > MAX_LOYALTY_EARN_RATE) {
    return `Loyalty points per ₹100 must be between 0 and ${MAX_LOYALTY_EARN_RATE}.`;
  }

  return null;
}
