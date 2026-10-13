/**
 * The shell's fallback picture of the product.
 *
 * `GET /api/menu` is the source of truth for navigation: the server owns which
 * modules exist, what hangs under them, where each screen lives and what this
 * user may do there. `MenuService` reads it, and everything in the chrome reads
 * `MenuService`.
 *
 * This file is what the shell falls back to when that call cannot be made — the
 * endpoint is down, the browser is offline, the response is unusable. Without it
 * a failed request would leave a person staring at an empty rail. It is a safety
 * net, not a second definition: when the call succeeds, nothing here is read.
 *
 * Because it is a fallback it will drift, and that is tolerable. Keep it to the
 * screens a person would be stranded without; do not treat it as a catalogue to
 * maintain in step with the seed.
 *
 * `module` is the permission prefix `AuthService.canView` takes, not a folder
 * name — a screen with `module: null` is open to anyone who is signed in.
 */
export interface ShellScreen {
  /** What the person calls it. */
  readonly label: string;
  /** Router path, absolute. */
  readonly path: string;
  /** Which section of the product it belongs to, used as the group heading. */
  readonly group: string;
  /** Permission prefix, or null when the screen needs no permission. */
  readonly module: string | null;
  /** Subgroup heading inside the secondary menu panel. */
  readonly subGroup?: string;
}

/**
 * Kept in the order the rail presents the modules, so the grouped panels come
 * out in the same order the person navigates in.
 */
export const SHELL_SCREENS: readonly ShellScreen[] = [
  { label: 'Dashboard', path: '/dashboard', group: 'Home', module: null },

  { label: 'Contacts', path: '/contacts', group: 'Contacts', module: 'contacts' },

  { label: 'Items', path: '/inventory/items', group: 'Inventory', module: 'inventory' },
  { label: 'Item categories', path: '/inventory/categories', group: 'Inventory', module: 'inventory' },
  { label: 'Stock', path: '/inventory/stock', group: 'Inventory', module: 'inventory' },
  { label: 'Stock adjustments', path: '/inventory/stock-adjustments', group: 'Inventory', module: 'inventory' },
  { label: 'Price lists', path: '/inventory/price-lists', group: 'Inventory', module: 'inventory' },
  { label: 'Warehouses', path: '/inventory/warehouses', group: 'Inventory', module: 'inventory' },

  { label: 'Purchase orders', path: '/purchase/purchase-orders', group: 'Purchase', module: 'purchase' },
  { label: 'Goods receipts', path: '/purchase/goods-receipts', group: 'Purchase', module: 'purchase' },
  { label: 'Bills', path: '/purchase/bills', group: 'Purchase', module: 'purchase' },
  { label: 'Debit notes', path: '/purchase/debit-notes', group: 'Purchase', module: 'purchase' },
  { label: 'Recurring bills', path: '/purchase/recurring-bills', group: 'Purchase', module: 'purchase' },

  { label: 'Quotes', path: '/sales/quotes', group: 'Sales', module: 'sales' },
  { label: 'Sales orders', path: '/sales/sales-orders', group: 'Sales', module: 'sales' },
  { label: 'Delivery challans', path: '/sales/delivery-challans', group: 'Sales', module: 'sales' },
  { label: 'Invoices', path: '/sales/invoices', group: 'Sales', module: 'sales' },
  { label: 'Credit notes', path: '/sales/credit-notes', group: 'Sales', module: 'sales' },
  { label: 'Recurring invoices', path: '/sales/recurring-invoices', group: 'Sales', module: 'sales' },

  { label: 'Dashboard', path: '/banking/dashboard', group: 'Banking', module: 'banking' },
  { label: 'Banks', path: '/banking/banks', group: 'Banking', module: 'banking' },
  { label: 'Bank accounts', path: '/banking/accounts', group: 'Banking', module: 'banking' },
  { label: 'Receive money', path: '/banking/receive-money', group: 'Banking', module: 'banking' },
  { label: 'Spend money', path: '/banking/spend-money', group: 'Banking', module: 'banking' },
  { label: 'Transfer money', path: '/banking/transfer-money', group: 'Banking', module: 'banking' },
  { label: 'Bank statements', path: '/banking/statements', group: 'Banking', module: 'banking' },
  { label: 'Reconciliation', path: '/accounting/reconciliation', group: 'Banking', module: 'banking' },

  { label: 'Chart of accounts', path: '/accounting/chart-of-accounts', group: 'Accounts', module: 'accounting' },
  { label: 'Sub-accounts', path: '/accounting/sub-accounts', group: 'Accounts', module: 'accounting' },
  { label: 'Journal entries', path: '/accounting/journals', group: 'Accounts', module: 'accounting' },
  { label: 'Opening balance', path: '/accounting/opening-balance', group: 'Accounts', module: 'accounting' },

  { label: 'Reports', path: '/reports', group: 'Reports', module: 'reports' },
  // Sales Reports
  { label: 'Aged Receivables Summary', path: '/reports/aged-receivables-summary', group: 'Reports', module: 'reports', subGroup: 'Sales Reports' },
  { label: 'Aged Receivables Detail', path: '/reports/aged-receivables-detail', group: 'Reports', module: 'reports', subGroup: 'Sales Reports' },
  { label: 'Receivable Invoice Summary', path: '/reports/receivable-invoice-summary', group: 'Reports', module: 'reports', subGroup: 'Sales Reports' },
  { label: 'Receivable Invoice Detail', path: '/reports/receivable-invoice-detail', group: 'Reports', module: 'reports', subGroup: 'Sales Reports' },
  { label: 'Invoice Track Report', path: '/reports/invoice-track-report', group: 'Reports', module: 'reports', subGroup: 'Sales Reports' },
  { label: 'Invoice/DN Payment Collection Report', path: '/reports/invoice-dn-payment-collection-report', group: 'Reports', module: 'reports', subGroup: 'Sales Reports' },
  { label: 'Sales Analysis', path: '/reports/sales-analysis', group: 'Reports', module: 'reports', subGroup: 'Sales Reports' },
  { label: 'Sales Analysis Detail Report', path: '/reports/sales-analysis-detail-report', group: 'Reports', module: 'reports', subGroup: 'Sales Reports' },
  { label: 'Sales Order Track Report', path: '/reports/sales-order-track-report', group: 'Reports', module: 'reports', subGroup: 'Sales Reports' },
  { label: 'Quotation Track Report', path: '/reports/quotation-track-report', group: 'Reports', module: 'reports', subGroup: 'Sales Reports' },
  { label: 'Delivery Order Track Report', path: '/reports/delivery-order-track-report', group: 'Reports', module: 'reports', subGroup: 'Sales Reports' },
  // Purchase Report
  { label: 'Aged Payables Summary', path: '/reports/aged-payables-summary', group: 'Reports', module: 'reports', subGroup: 'Purchase Report' },
  { label: 'Aged Payables Details', path: '/reports/aged-payables-details', group: 'Reports', module: 'reports', subGroup: 'Purchase Report' },
  { label: 'Payable Invoice Summary', path: '/reports/payable-invoice-summary', group: 'Reports', module: 'reports', subGroup: 'Purchase Report' },
  { label: 'Payable Invoice Detail', path: '/reports/payable-invoice-detail', group: 'Reports', module: 'reports', subGroup: 'Purchase Report' },
  { label: 'Bills Track Report', path: '/reports/bills-track-report', group: 'Reports', module: 'reports', subGroup: 'Purchase Report' },
  { label: 'Bill/DN Payment Report', path: '/reports/bill-dn-payment-report', group: 'Reports', module: 'reports', subGroup: 'Purchase Report' },
  { label: 'Purchase Analysis', path: '/reports/purchase-analysis', group: 'Reports', module: 'reports', subGroup: 'Purchase Report' },
  { label: 'Purchase Order Track Report', path: '/reports/purchase-order-track-report', group: 'Reports', module: 'reports', subGroup: 'Purchase Report' },
  { label: 'Purchase Receive Order Details', path: '/reports/purchase-receive-order-details', group: 'Reports', module: 'reports', subGroup: 'Purchase Report' },
  { label: 'Receive Order Track Report', path: '/reports/receive-order-track-report', group: 'Reports', module: 'reports', subGroup: 'Purchase Report' },
  // Inventory Report
  { label: 'Inventory Item Summary', path: '/reports/inventory-item-summary', group: 'Reports', module: 'reports', subGroup: 'Inventory Report' },
  { label: 'Inventory Item Detail', path: '/reports/inventory-item-detail', group: 'Reports', module: 'reports', subGroup: 'Inventory Report' },
  { label: 'Inventory Item List', path: '/reports/inventory-item-list', group: 'Reports', module: 'reports', subGroup: 'Inventory Report' },
  { label: 'Inventory Aging Report', path: '/reports/inventory-aging-report', group: 'Reports', module: 'reports', subGroup: 'Inventory Report' },
  { label: 'Batch Tracking Status Report', path: '/reports/batch-tracking-status-report', group: 'Reports', module: 'reports', subGroup: 'Inventory Report' },
  { label: 'Batch Tracking Detail Report', path: '/reports/batch-tracking-detail-report', group: 'Reports', module: 'reports', subGroup: 'Inventory Report' },
  { label: 'Serial Tracking Status Report', path: '/reports/serial-tracking-status-report', group: 'Reports', module: 'reports', subGroup: 'Inventory Report' },
  { label: 'Serial Tracking Detail Report', path: '/reports/serial-tracking-detail-report', group: 'Reports', module: 'reports', subGroup: 'Inventory Report' },
  { label: 'Warehouse Tracking Status Report', path: '/reports/warehouse-tracking-status-report', group: 'Reports', module: 'reports', subGroup: 'Inventory Report' },
  // Accounting reports
  { label: 'Trial Balance', path: '/accounting/trial-balance', group: 'Reports', module: 'accounting', subGroup: 'Accounting' },
  { label: 'General ledger', path: '/accounting/ledger', group: 'Reports', module: 'accounting', subGroup: 'Accounting' },
  { label: 'Journal Report', path: '/reports/journal-report', group: 'Reports', module: 'reports', subGroup: 'Accounting' },
  { label: 'Account Transaction', path: '/reports/account-transaction', group: 'Reports', module: 'reports', subGroup: 'Accounting' },
  { label: 'Bank Summary', path: '/reports/bank-summary', group: 'Reports', module: 'reports', subGroup: 'Accounting' },
  { label: 'Foreign Currency Gain or Loss', path: '/reports/foreign-currency-gain-or-loss', group: 'Reports', module: 'reports', subGroup: 'Accounting' },
  { label: 'Foreign Currency Gain or Loss Details', path: '/reports/foreign-currency-gain-or-loss-details', group: 'Reports', module: 'reports', subGroup: 'Accounting' },
  // Finance reports
  { label: 'Balance Sheet', path: '/reports/balance-sheet', group: 'Reports', module: 'reports', subGroup: 'Finance' },
  { label: 'Profit & Loss', path: '/reports/profit-and-loss', group: 'Reports', module: 'reports', subGroup: 'Finance' },
  { label: 'Cash Flow Statement - Direct', path: '/reports/cash-flow-statement-direct', group: 'Reports', module: 'reports', subGroup: 'Finance' },
  { label: 'Reconciliation Report', path: '/reports/reconciliation-report', group: 'Reports', module: 'reports', subGroup: 'Finance' },
  // Financial performance
  { label: 'Business Performance', path: '/reports/business-performance', group: 'Reports', module: 'reports', subGroup: 'Financial performance' },
  // Fixed Asset
  { label: 'Fixed Assets Schedule', path: '/reports/fixed-assets-schedule', group: 'Reports', module: 'reports', subGroup: 'Fixed Asset' },
  { label: 'Depreciation Schedule', path: '/reports/depreciation-schedule', group: 'Reports', module: 'reports', subGroup: 'Fixed Asset' },
  { label: 'Disposal Schedule Report', path: '/reports/disposal-schedule-report', group: 'Reports', module: 'reports', subGroup: 'Fixed Asset' },
  { label: 'Fixed Asset Reconciliation', path: '/reports/fixed-asset-reconciliation', group: 'Reports', module: 'reports', subGroup: 'Fixed Asset' },

  // Settings
  { label: 'Organization', path: '/settings/organization', group: 'Settings', module: 'settings', subGroup: 'Organisation' },
  { label: 'Branches', path: '/settings/branches', group: 'Settings', module: 'settings', subGroup: 'Organisation' },
  { label: 'Configuration', path: '/settings/configuration', group: 'Settings', module: 'settings', subGroup: 'Organisation' },
  { label: 'Number series', path: '/settings/numbering', group: 'Settings', module: 'settings', subGroup: 'Organisation' },
  { label: 'Currencies', path: '/settings/currencies', group: 'Settings', module: 'settings', subGroup: 'Organisation' },
  { label: 'Email', path: '/settings/email', group: 'Settings', module: 'settings', subGroup: 'Organisation' },
  { label: 'Support & Bug reports', path: '/settings/support', group: 'Settings', module: 'support', subGroup: 'Organisation' },

  { label: 'Users', path: '/settings/users', group: 'Settings', module: 'settings', subGroup: 'Users and access' },
  { label: 'Roles', path: '/settings/roles', group: 'Settings', module: 'settings', subGroup: 'Users and access' },
  { label: 'API keys', path: '/settings/api-clients', group: 'Settings', module: 'settings', subGroup: 'Users and access' },
  { label: 'Audit log', path: '/settings/audit-log', group: 'Settings', module: 'settings', subGroup: 'Users and access' },
  { label: 'My Profile', path: '/profile', group: 'Settings', module: null, subGroup: 'Users and access' },

  { label: 'Tax master', path: '/settings/tax', group: 'Settings', module: 'settings', subGroup: 'Accounting' },
  { label: 'Payment terms', path: '/settings/payment-terms', group: 'Settings', module: 'settings', subGroup: 'Accounting' },
  { label: 'Closing dates', path: '/settings/closing-dates', group: 'Settings', module: 'settings', subGroup: 'Accounting' },
  { label: 'Contact person roles', path: '/settings/contact-person-roles', group: 'Settings', module: 'settings', subGroup: 'Accounting' },

  { label: 'Print templates', path: '/settings/print-templates', group: 'Settings', module: 'settings', subGroup: 'Print templates' },

  { label: 'UOM types', path: '/settings/unit-types', group: 'Inventory', module: 'settings' },
  { label: 'HSN/SAC codes', path: '/settings/hsn-sac', group: 'Inventory', module: 'settings' },
  { label: 'Metal purities', path: '/settings/metal-purities', group: 'Inventory', module: 'settings' },
];

/** A heading plus the rows under it, as the popovers render them. */
export interface ShellScreenGroup {
  readonly name: string;
  readonly items: readonly ShellScreen[];
}

/**
 * Collapse a flat screen list into its groups, preserving the registry order
 * and dropping groups that ended up empty.
 */
export function groupScreens(screens: readonly ShellScreen[]): ShellScreenGroup[] {
  const groups: ShellScreenGroup[] = [];
  for (const screen of screens) {
    const last = groups[groups.length - 1];
    if (last && last.name === screen.group) {
      (last.items as ShellScreen[]).push(screen);
    } else {
      const existing = groups.find((g) => g.name === screen.group);
      if (existing) {
        (existing.items as ShellScreen[]).push(screen);
      } else {
        groups.push({ name: screen.group, items: [screen] });
      }
    }
  }
  return groups;
}

/**
 * The register (list) screens. Export and Import belong to these and to nothing
 * else, so the breadcrumb strip shows those controls here and only here — a
 * create form has nothing to export.
 */
export const SHELL_REGISTERS: readonly string[] = [
  '/contacts',
  '/inventory/items',
  '/inventory/categories',
  '/inventory/stock',
  '/inventory/stock-adjustments',
  '/inventory/price-lists',
  '/inventory/warehouses',
  '/purchase',
  '/purchase/bills',
  '/purchase/purchase-orders',
  '/purchase/goods-receipts',
  '/purchase/debit-notes',
  '/sales',
  '/sales/quotes',
  '/sales/delivery-challans',
  '/sales/credit-notes',
  '/sales/invoices',
  '/sales/sales-orders',
  '/accounting/chart-of-accounts',
  '/accounting/sub-accounts',
  '/accounting/journals',
  '/accounting/trial-balance',
  '/banking/banks',
  '/banking/accounts',
  '/banking/statements',
];

/** One entry in the navigation rail. */
export interface ShellRailItem {
  /**
   * The menu this entry opens: `MenuView.code` from the server's tree, or the
   * fallback tree's code (its group name, lowercased). What the rail, the
   * secondary menu and the phone sheet agree on as "this module" (TK-189).
   */
  code?: string;
  path: string;
  label: string;
  /** Lucide icon name, matching what the menu seed stores. */
  icon: string;
  /** Permission prefix, or null for an entry every signed-in user may open. */
  module: string | null;
}

/**
 * The rail before `/api/menu` answers, and if it never does.
 *
 * Declared once here rather than in each component that draws a rail — the shell
 * and the nav both need it, and two copies of a menu is how they come to disagree.
 */
export const FALLBACK_RAIL: readonly ShellRailItem[] = [
  { code: 'home', path: '/dashboard', label: 'Home', icon: 'house', module: null },
  { code: 'contacts', path: '/contacts', label: 'Contacts', icon: 'users-round', module: 'contacts' },
  { code: 'inventory', path: '/inventory', label: 'Inventory', icon: 'boxes', module: 'inventory' },
  { code: 'purchase', path: '/purchase', label: 'Purchase', icon: 'package', module: 'purchase' },
  { code: 'sales', path: '/sales', label: 'Sales', icon: 'shopping-cart', module: 'sales' },
  { code: 'banking', path: '/banking', label: 'Banking', icon: 'landmark', module: 'banking' },
  { code: 'accounts', path: '/accounting', label: 'Accounts', icon: 'book-open', module: 'accounting' }, // STRICT UI RULE: Accounts
  { code: 'reports', path: '/reports', label: 'Reports', icon: 'chart-no-axes-combined', module: 'reports' },
  { code: 'settings', path: '/settings', label: 'Settings', icon: 'settings', module: 'settings' },
];
