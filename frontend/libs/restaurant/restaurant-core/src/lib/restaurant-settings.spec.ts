import { describe, expect, it } from 'vitest';
import {
  RESTAURANT_PRESETS,
  RequirementLevel,
  applyPreset,
  defaultRestaurantSettings,
  effectiveSettings,
  isCustomerRequired,
  isTableRequired,
  matchingPreset,
  settingsProblem,
} from './restaurant-settings';

const preset = (code: string) => RESTAURANT_PRESETS.find((p) => p.code === code)!;

describe('restaurant settings', () => {
  it('starts as billing only with nothing required', () => {
    const s = defaultRestaurantSettings();
    expect(matchingPreset(s)).toBe('billing');
    expect(isTableRequired(s, 'DineIn')).toBe(false);
    expect(isCustomerRequired(s, 'Delivery')).toBe(false);
  });

  it('full service turns both switches on and requires a table for dine-in', () => {
    const s = applyPreset(defaultRestaurantSettings(), preset('full-service'));
    expect(s.tablesEnabled).toBe(true);
    expect(s.kitchenEnabled).toBe(true);
    expect(s.tableRequired).toBe(RequirementLevel.DineInOnly);
    expect(isTableRequired(s, 'DineIn')).toBe(true);
    expect(isTableRequired(s, 'Takeaway')).toBe(false);
    expect(matchingPreset(s)).toBe('full-service');
  });

  it('a preset keeps a stricter table requirement the branch already chose', () => {
    const s = applyPreset({ ...defaultRestaurantSettings(), tableRequired: RequirementLevel.Always }, preset('dine-in'));
    expect(s.tableRequired).toBe(RequirementLevel.Always);
  });

  it('a preset leaves recipes, service charge and tips alone', () => {
    const before = { ...defaultRestaurantSettings(), recipesEnabled: true, serviceChargePercent: 10, tipsEnabled: true };
    const s = applyPreset(before, preset('counter-kitchen'));
    expect(s.recipesEnabled).toBe(true);
    expect(s.serviceChargePercent).toBe(10);
    expect(s.tipsEnabled).toBe(true);
  });

  it('a requirement counts only while its switch is on', () => {
    const s = effectiveSettings({ ...defaultRestaurantSettings(), tableRequired: RequirementLevel.Always, kotRequired: true });
    expect(s.tableRequired).toBe(RequirementLevel.Off);
    expect(s.kotRequired).toBe(false);
  });

  it('customer required always applies to every order type', () => {
    const s = { ...defaultRestaurantSettings(), customerRequired: RequirementLevel.Always };
    expect(isCustomerRequired(s, 'Takeaway')).toBe(true);
    expect(isCustomerRequired({ ...s, customerRequired: RequirementLevel.DineInOnly }, 'Delivery')).toBe(false);
  });

  it('refuses a service charge outside 0 to 25%', () => {
    expect(settingsProblem({ ...defaultRestaurantSettings(), serviceChargePercent: 25 })).toBeNull();
    expect(settingsProblem({ ...defaultRestaurantSettings(), serviceChargePercent: 26 })).toContain('25%');
    expect(settingsProblem({ ...defaultRestaurantSettings(), serviceChargePercent: -1 })).not.toBeNull();
  });
});
