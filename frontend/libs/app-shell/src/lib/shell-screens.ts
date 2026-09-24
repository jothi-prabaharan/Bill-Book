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
}

/**
 * Kept in the order the rail presents the modules, so the grouped panels come
 * out in the same order the person navigates in.
 */
export const SHELL_SCREENS: readonly ShellScreen[] = [
  { label: 'Dashboard', path: '/dashboard', group: 'Home', module: null },

  { label: 'Contacts', path: '/contacts', group: 'Contacts', module: 'contacts' },
  { label: 'Leads', path: '/customer/leads', group: 'Contacts', module: 'crm' },
  { label: 'Tickets', path: '/customer/tickets', group: 'Contacts', module: 'support' },

  { label: 'Items', path: '/inventory/items', group: 'Inventory', module: 'inventory' },
  { label: 'Item categories', path: '/inventory/categories', group: 'Inventory', module: 'inventory' },
  { label: 'Stock', path: '/inventory/stock', group: 'Inventory', module: 'inventory' },
  { label: 'Stock adjustments', path: '/inventory/stock-adjustments', group: 'Inventory', module: 'inventory' },
  { label: 'Price lists', path: '/inventory/price-lists', group: 'Inventory', module: 'inventory' },
  { label: 'Warehouses', path: '/inventory/warehouses', group: 'Inventory', module: 'inventory' },

  { label: 'Purchase register', path: '/purchase', group: 'Purchase', module: 'accounting' },
  { label: 'New purchase order', path: '/purchase/purchase-orders/new', group: 'Purchase', module: 'accounting' },
  { label: 'New goods receipt', path: '/purchase/goods-receipts/new', group: 'Purchase', module: 'accounting' },
  { label: 'New bill', path: '/purchase/bills/new', group: 'Purchase', module: 'accounting' },
  { label: 'New debit note', path: '/purchase/debit-notes/new', group: 'Purchase', module: 'accounting' },

  { label: 'Sales register', path: '/sales', group: 'Sales', module: 'accounting' },
  { label: 'Invoices', path: '/sales/invoices', group: 'Sales', module: 'accounting' },
  { label: 'Sales orders', path: '/sales/sales-orders', group: 'Sales', module: 'accounting' },
  { label: 'New invoice', path: '/sales/invoices/new', group: 'Sales', module: 'accounting' },
  { label: 'New sales order', path: '/sales/sales-orders/new', group: 'Sales', module: 'accounting' },
  { label: 'New quote', path: '/sales/quotes/new', group: 'Sales', module: 'accounting' },
  { label: 'New delivery challan', path: '/sales/delivery-challans/new', group: 'Sales', module: 'accounting' },
  { label: 'New credit note', path: '/sales/credit-notes/new', group: 'Sales', module: 'accounting' },

  { label: 'Banks', path: '/banking/banks', group: 'Banking', module: 'banking' },
  { label: 'Bank accounts', path: '/banking/accounts', group: 'Banking', module: 'banking' },
  { label: 'Receive money', path: '/banking/receive-money', group: 'Banking', module: 'banking' },
  { label: 'Spend money', path: '/banking/spend-money', group: 'Banking', module: 'banking' },
  { label: 'Transfer money', path: '/banking/transfer-money', group: 'Banking', module: 'banking' },
  { label: 'Bank statements', path: '/banking/statements', group: 'Banking', module: 'banking' },

  { label: 'Chart of accounts', path: '/accounting/chart-of-accounts', group: 'Accounts', module: 'accounting' },
  { label: 'Sub-accounts', path: '/accounting/sub-accounts', group: 'Accounts', module: 'accounting' },
  { label: 'Journal entries', path: '/accounting/journals', group: 'Accounts', module: 'accounting' },
  { label: 'Opening balance', path: '/accounting/opening-balance', group: 'Accounts', module: 'accounting' },
  { label: 'Trial balance', path: '/accounting/trial-balance', group: 'Accounts', module: 'accounting' },
  { label: 'Account ledger', path: '/accounting/ledger', group: 'Accounts', module: 'accounting' },
  { label: 'Reconciliation', path: '/accounting/reconciliation', group: 'Accounts', module: 'accounting' },

  { label: 'Reports', path: '/reports', group: 'Reports', module: null },
  { label: 'Profit and loss', path: '/reports/statements/profit-and-loss', group: 'Reports', module: null },
  { label: 'Balance sheet', path: '/reports/statements/balance-sheet', group: 'Reports', module: null },

  { label: 'Organization', path: '/settings/organization', group: 'Settings', module: 'settings' },
  { label: 'Branches', path: '/settings/branches', group: 'Settings', module: 'settings' },
  { label: 'Configuration', path: '/settings/configuration', group: 'Settings', module: 'settings' },
  { label: 'Users', path: '/settings/users', group: 'Settings', module: 'settings' },
  { label: 'Roles', path: '/settings/roles', group: 'Settings', module: 'settings' },
  { label: 'Currencies', path: '/settings/currencies', group: 'Settings', module: 'settings' },
  { label: 'Email', path: '/settings/email', group: 'Settings', module: 'settings' },
  { label: 'Print templates', path: '/settings/print-templates', group: 'Settings', module: 'settings' },
  { label: 'Tax master', path: '/settings/tax', group: 'Settings', module: 'settings' },
  { label: 'Number series', path: '/settings/numbering', group: 'Settings', module: 'settings' },
  { label: 'Payment terms', path: '/settings/payment-terms', group: 'Settings', module: 'settings' },
  { label: 'Closing dates', path: '/settings/closing-dates', group: 'Settings', module: 'settings' },
  { label: 'Contact person roles', path: '/settings/contact-person-roles', group: 'Settings', module: 'settings' },
  { label: 'Unit types', path: '/settings/unit-types', group: 'Settings', module: 'settings' },
  { label: 'HSN/SAC codes', path: '/settings/hsn-sac', group: 'Settings', module: 'settings' },
  { label: 'Metal purities', path: '/settings/metal-purities', group: 'Settings', module: 'settings' },
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
  '/sales',
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
  { path: '/dashboard', label: 'Home', icon: 'house', module: null },
  { path: '/contacts', label: 'Contacts', icon: 'users-round', module: 'contacts' },
  { path: '/inventory', label: 'Inventory', icon: 'boxes', module: 'inventory' },
  { path: '/purchase', label: 'Purchase', icon: 'package', module: 'purchase' },
  { path: '/sales', label: 'Sales', icon: 'shopping-cart', module: 'sales' },
  { path: '/banking', label: 'Banking', icon: 'landmark', module: 'banking' },
  { path: '/accounting', label: 'Accounts', icon: 'book-open', module: 'accounting' }, // STRICT UI RULE: Accounts
  { path: '/reports', label: 'Reports', icon: 'chart-no-axes-combined', module: 'reports' },
  { path: '/settings', label: 'Settings', icon: 'settings', module: 'settings' },
];
