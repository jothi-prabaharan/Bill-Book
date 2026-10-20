/**
 * The documentation table of contents. Adding a page means adding a markdown
 * file under docs/User_Guide and an entry here — nothing else.
 */
export interface DocPage {
  slug: string;
  title: string;
  /** Reflects whether the documented feature is built, not whether the page is written. */
  status: 'built' | 'partial' | 'planned';
}

export interface DocSection {
  title: string;
  icon?: string;
  pages: DocPage[];
}

export interface DocGroup {
  name: string;
  sections: DocSection[];
}

export const DOC_GROUPS: DocGroup[] = [
  {
    name: 'Getting Started',
    sections: [
      {
        title: 'Overview',
        icon: 'overview',
        pages: [{ slug: 'overview', title: 'Overview', status: 'built' }],
      },
      {
        title: 'Platform',
        icon: 'platform',
        pages: [
          { slug: 'platform', title: 'Platform', status: 'built' },
          // 'partial': the Linux installers and a desktop update were built and run
          // end to end; the Windows, macOS, Android and iOS builds need their own
          // machines and signing, and are the owner's run (TK-184).
          { slug: 'desktop-and-mobile', title: 'Desktop and mobile apps', status: 'partial' },
          { slug: 'lists', title: 'Document lists', status: 'built' },
        ],
      },
      {
        title: 'Releases',
        icon: 'releases',
        pages: [{ slug: 'releases', title: 'Release notes', status: 'built' }],
      },
    ],
  },
  {
    name: 'Core Operations',
    sections: [
      {
        title: 'Sales',
        icon: 'sales',
        // In the order the documents actually run: a quote becomes an order,
        // an order goes out on a challan, is billed on an invoice, and an invoice
        // is corrected by a credit note.
        pages: [
          { slug: 'quotes', title: 'Quotes', status: 'built' },
          { slug: 'sales-orders', title: 'Sales orders', status: 'built' },
          // Partial: the order is chosen by number, and returns make no e-way bill.
          { slug: 'delivery-challans', title: 'Delivery challans', status: 'partial' },
          { slug: 'invoices', title: 'Invoices', status: 'built' },
          { slug: 'credit-notes', title: 'Credit notes', status: 'built' },
        ],
      },
      {
        title: 'Purchase',
        icon: 'purchase',
        pages: [{ slug: 'purchase', title: 'Purchase', status: 'built' }],
      },
      {
        title: 'Accounts',
        icon: 'accounts',
        pages: [{ slug: 'accounting', title: 'Accounts', status: 'built' }],
      },
      {
        title: 'Masters',
        icon: 'masters',
        pages: [{ slug: 'masters', title: 'Masters', status: 'built' }],
      },
      {
        title: 'Reports',
        icon: 'reports',
        pages: [{ slug: 'reports', title: 'Reports', status: 'partial' }],
      },
      {
        title: 'Approvals',
        icon: 'approvals',
        pages: [{ slug: 'approvals', title: 'Approvals', status: 'built' }],
      },
    ],
  },
  {
    name: 'People & HRMS',
    sections: [
      {
        title: 'People',
        icon: 'people',
        // HRMS and Payroll share the employee master; leave, attendance and pay
        // join this section as each stage is built.
        pages: [
          { slug: 'people', title: 'Employees', status: 'partial' },
          { slug: 'payroll', title: 'Payroll', status: 'built' },
          { slug: 'recruitment', title: 'Recruitment', status: 'built' },
          { slug: 'performance', title: 'Performance', status: 'built' },
        ],
      },
    ],
  },
  {
    name: 'Industry Verticals',
    sections: [
      {
        title: 'Restaurant',
        icon: 'restaurant',
        // Restaurant branches only (D-39). Settings (TK-143), the menu (TK-144), tables
        // (TK-145), orders (TK-146), kitchen tickets (TK-147), billing (TK-148),
        // recipes (TK-149) and reports (TK-150) are built.
        pages: [
          { slug: 'restaurant-settings', title: 'Restaurant settings', status: 'built' },
          { slug: 'restaurant-menu', title: 'Menu', status: 'built' },
          { slug: 'restaurant-tables', title: 'Tables', status: 'built' },
          { slug: 'restaurant-bookings', title: 'Bookings and waiting list', status: 'built' },
          { slug: 'restaurant-orders', title: 'Orders', status: 'built' },
          { slug: 'restaurant-kitchen', title: 'Kitchen tickets', status: 'built' },
          { slug: 'restaurant-billing', title: 'Billing', status: 'built' },
          { slug: 'restaurant-recipes', title: 'Recipes and wastage', status: 'built' },
          { slug: 'restaurant-reports', title: 'Reports', status: 'built' },
        ],
      },
      {
        title: 'School',
        icon: 'school',
        // The School add-on (S0 onward): students, admissions, attendance, fees and
        // maintenance, joining this page as each stage is built.
        pages: [{ slug: 'school', title: 'School', status: 'partial' }],
      },
      {
        title: 'Customer portal',
        icon: 'portal',
        // Partial: online payment runs on the sandbox gateway until D-25 names one.
        pages: [{ slug: 'portal', title: 'Customer portal', status: 'partial' }],
      },
    ],
  },
  {
    name: 'Extensions',
    sections: [
      {
        title: 'Add-ons',
        icon: 'addons',
        pages: [
          { slug: 'recurring-billing', title: 'Recurring Billing', status: 'built' },
          { slug: 'service-plans', title: 'Service Plans', status: 'built' },
          { slug: 'savings-scheme', title: 'Savings Scheme', status: 'built' },
          { slug: 'site-services', title: 'Site Services', status: 'partial' },
        ],
      },
    ],
  },
];

export const DOCS: DocSection[] = DOC_GROUPS.flatMap((g) => g.sections);

export const ALL_PAGES: DocPage[] = DOCS.flatMap((s) => s.pages);
