import { normaliseBusinessType } from '@bill-book/auth';
import type { EditLogArea } from '@bill-book/ui-components';

/**
 * The edit logs Settings › Audit log offers (TK-118). Each service keeps its
 * own log in its own schema, so an area is a gateway cluster, and each area's
 * routes demand that module's own view permission — a user sees the log of
 * what they may already see.
 *
 * Grouped by what brings them (TK-136): a trade, School, or an add-on. The
 * page offers the groups the branch's business type and add-ons reach.
 */
export const AUDIT_AREAS = {
  Trading: [
    { service: 'sales', label: 'Sales' },
    { service: 'purchase', label: 'Purchase' },
    { service: 'accounting', label: 'Accounting and banking' },
    { service: 'inventory', label: 'Inventory' },
    { service: 'master', label: 'Contacts' },
    { service: 'customer', label: 'Leads and tickets' },
    { service: 'printing', label: 'Print templates' },
    { service: 'reporting', label: 'Reports' },
  ],
  School: [
    { service: 'sis', label: 'Students' },
    { service: 'admission', label: 'Admissions' },
    { service: 'attendance', label: 'Attendance' },
    { service: 'fee', label: 'Fees' },
    { service: 'facility', label: 'Facilities' },
    { service: 'workorder', label: 'Work orders' },
    { service: 'preventive', label: 'Preventive maintenance' },
    { service: 'amc', label: 'Maintenance contracts' },
    { service: 'hrm', label: 'Staff' },
    { service: 'master', label: 'Contacts' },
  ],
  Hrms: [
    { service: 'hrm', label: 'Employees' },
    { service: 'timeleave', label: 'Time and leave' },
    { service: 'claims', label: 'Claims' },
    { service: 'recruitment', label: 'Recruitment' },
    { service: 'performance', label: 'Performance' },
  ],
  Payroll: [
    { service: 'payroll', label: 'Payroll' },
    { service: 'hrm', label: 'Employees' },
  ],
} satisfies Record<string, EditLogArea[]>;

/**
 * The areas a branch reaches, each service once (the first group to name it
 * labels it). A branch with no business type is an HRMS or Payroll customer;
 * one with neither type nor add-on falls back to the trading areas.
 */
export function auditAreasFor(businessType: string | null | undefined, addOns: string | null | undefined): EditLogArea[] {
  const type = normaliseBusinessType(businessType);
  const flags = (addOns ?? '').split(',').map((a) => a.trim());
  const groups: EditLogArea[][] = [];

  if (type === 'School') groups.push(AUDIT_AREAS.School);
  else if (type) groups.push(AUDIT_AREAS.Trading);
  if (flags.includes('Hrms')) groups.push(AUDIT_AREAS.Hrms);
  if (flags.includes('Payroll')) groups.push(AUDIT_AREAS.Payroll);
  if (groups.length === 0) groups.push(AUDIT_AREAS.Trading);

  const seen = new Set<string>();
  return groups.flat().filter((area) => !seen.has(area.service) && (seen.add(area.service), true));
}
