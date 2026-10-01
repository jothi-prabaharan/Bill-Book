export enum RequirementLevel {
  Off = 'Off',
  DineInOnly = 'DineInOnly',
  Always = 'Always'
}

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
}
