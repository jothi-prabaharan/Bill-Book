import type { AppId } from '@bill-book/auth';
import type { EditLogArea } from '@bill-book/ui-components';

/**
 * The edit logs each app's Settings › Audit log offers (TK-118). Each service
 * keeps its own log in its own schema, so an area is a gateway cluster, and
 * each area's routes demand that module's own view permission — a user sees
 * the log of what they may already see.
 */
export const AUDIT_AREAS: Record<AppId, EditLogArea[]> = {
  RetailErp: [
    { service: 'sales', label: 'Sales' },
    { service: 'purchase', label: 'Purchase' },
    { service: 'accounting', label: 'Accounting and banking' },
    { service: 'inventory', label: 'Inventory' },
    { service: 'master', label: 'Contacts' },
    { service: 'customer', label: 'Leads and tickets' },
    { service: 'printing', label: 'Print templates' },
    { service: 'reporting', label: 'Reports' },
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
};

export function auditAreasFor(app: AppId): EditLogArea[] {
  return AUDIT_AREAS[app] ?? AUDIT_AREAS.RetailErp;
}
