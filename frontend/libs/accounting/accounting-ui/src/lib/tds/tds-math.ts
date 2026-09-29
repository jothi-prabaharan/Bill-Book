/**
 * Dates as TDS counts them (TK-117): the April-to-March financial year named
 * by its first calendar year, and its four quarters. Pure, so the page and the
 * spec agree with the server's `TdsCalculationRepository.FinancialYearOf`.
 */

/** The financial year an ISO date (yyyy-MM-dd) falls in: April 2026 to March 2027 is 2026. */
export function financialYearOf(isoDate: string): number {
  const [year, month] = isoDate.split('-').map(Number);
  return month >= 4 ? year : year - 1;
}

/** "2026-27". */
export function financialYearLabel(year: number): string {
  return `${year}-${String((year + 1) % 100).padStart(2, '0')}`;
}

/** The quarter (1 to 4) of the financial year an ISO date falls in: April to June is 1, January to March is 4. */
export function quarterOf(isoDate: string): number {
  const month = Number(isoDate.split('-')[1]);
  return month >= 4 ? Math.floor((month - 4) / 3) + 1 : 4;
}

/** "June 2026" for the first day of a month. */
export function monthLabel(isoDate: string): string {
  const [year, month] = isoDate.split('-').map(Number);
  const names = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
  return `${names[month - 1]} ${year}`;
}

/** The last few financial years up to the current one, newest first, for a year picker. */
export function recentFinancialYears(todayIso: string, count = 4): number[] {
  const current = financialYearOf(todayIso);
  return Array.from({ length: count }, (_, i) => current - i);
}
