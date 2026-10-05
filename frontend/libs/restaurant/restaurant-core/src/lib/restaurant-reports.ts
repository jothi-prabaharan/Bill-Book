import { RestaurantSettingsView } from './restaurant-settings';

/** The restaurant reports this caller may open (TK-150). */
export const RESTAURANT_REPORTS_API = '/api/restaurant/reports';

/** One restaurant report, as the catalog lists it. It runs on the report screen at `/reports/{reportKey}`. */
export interface RestaurantReportItem {
  reportKey: string;
  title: string;
  description: string | null;
  sortOrder: number;
}

type Switch = 'tablesEnabled' | 'kitchenEnabled' | 'recipesEnabled';

/** The reports a switch in Settings › Restaurant hides while it is off; the server is what hides them. */
const SWITCHED: { reportKey: string; title: string; setting: Switch; switchName: string }[] = [
  { reportKey: 'restaurant-table-turnover', title: 'Table turnover', setting: 'tablesEnabled', switchName: 'Tables' },
  { reportKey: 'restaurant-kot-cancellations', title: 'KOT cancellations', setting: 'kitchenEnabled', switchName: 'Kitchen (KOT)' },
  { reportKey: 'restaurant-food-cost', title: 'Food cost', setting: 'recipesEnabled', switchName: 'Recipes' },
  { reportKey: 'restaurant-wastage', title: 'Wastage', setting: 'recipesEnabled', switchName: 'Recipes' },
];

/** The route that opens a report. */
export function reportLink(reportKey: string): string[] {
  return ['/reports', reportKey];
}

/** The catalog in its seeded order. */
export function sortReports(reports: RestaurantReportItem[]): RestaurantReportItem[] {
  return [...reports].sort((a, b) => a.sortOrder - b.sortOrder || a.title.localeCompare(b.title));
}

/**
 * Why a report is missing, one sentence per switch that is off, so an owner
 * looking for table turnover learns where to turn it on rather than thinking
 * it does not exist.
 */
export function hiddenReportNotes(settings: RestaurantSettingsView): string[] {
  const off = SWITCHED.filter((s) => !settings[s.setting]);
  const bySwitch = new Map<string, string[]>();
  off.forEach((s) => bySwitch.set(s.switchName, [...(bySwitch.get(s.switchName) ?? []), s.title]));
  return [...bySwitch.entries()].map(
    ([name, titles]) => `${titles.join(' and ')} ${titles.length > 1 ? 'are' : 'is'} hidden while ${name} is off in Settings › Restaurant.`,
  );
}
