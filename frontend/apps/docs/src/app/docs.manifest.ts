/**
 * The documentation table of contents. Adding a page means adding a markdown
 * file under apps/docs/content and an entry here — nothing else.
 */
export interface DocPage {
  slug: string;
  title: string;
  /** Reflects whether the documented feature is built, not whether the page is written. */
  status: 'built' | 'partial' | 'planned';
}

export interface DocSection {
  title: string;
  pages: DocPage[];
}

export const DOCS: DocSection[] = [
  {
    title: 'Overview',
    pages: [{ slug: 'overview', title: 'Overview', status: 'built' }],
  },
  {
    title: 'Platform',
    pages: [{ slug: 'platform', title: 'Platform', status: 'built' }],
  },
  {
    title: 'Masters',
    pages: [{ slug: 'masters', title: 'Masters', status: 'built' }],
  },
  {
    title: 'Reports',
    pages: [{ slug: 'reports', title: 'Reports', status: 'partial' }],
  },
  {
    title: 'Accounts',
    pages: [{ slug: 'accounting', title: 'Accounts', status: 'built' }],
  },
  {
    title: 'Purchase',
    pages: [{ slug: 'purchase', title: 'Purchase', status: 'built' }],
  },
  {
    title: 'Sales',
    // In the order the documents actually run: a quote becomes an order,
    // an order goes out on a challan, is billed on an invoice, and an invoice
    // is corrected by a credit note.
    pages: [
      { slug: 'quotes', title: 'Quotes', status: 'built' },
      { slug: 'sales-orders', title: 'Sales orders', status: 'built' },
      // Partial: a sale challan's clearing-account posting is not built.
      { slug: 'delivery-challans', title: 'Delivery challans', status: 'partial' },
      { slug: 'invoices', title: 'Invoices', status: 'built' },
      { slug: 'credit-notes', title: 'Credit notes', status: 'built' },
    ],
  },
  {
    title: 'People',
    // HRMS and Payroll share the employee master; leave, attendance and pay
    // join this section as each stage is built.
    pages: [
      { slug: 'people', title: 'Employees', status: 'partial' },
      { slug: 'payroll', title: 'Payroll', status: 'built' },
      { slug: 'recruitment', title: 'Recruitment', status: 'built' },
      { slug: 'performance', title: 'Performance', status: 'built' },
    ],
  },
  {
    title: 'School',
    // The School app (S0 onward): students, admissions, attendance, fees and
    // maintenance, joining this page as each stage is built.
    pages: [{ slug: 'school', title: 'School', status: 'partial' }],
  },
  {
    title: 'Releases',
    pages: [{ slug: 'releases', title: 'Release notes', status: 'built' }],
  },
  {
    title: 'Development',
    pages: [
      { slug: 'development', title: 'Development', status: 'built' },
      { slug: 'inputs', title: 'Input components', status: 'built' },
      // 'partial' rather than 'built': the Azure templates and pipeline are
      // complete and verified locally, but have not been run against Azure, and
      // the first run stops at the migration step until the admin migration is
      // re-squashed — see deploy/azure/README.md.
      { slug: 'deployment', title: 'Deployment', status: 'partial' },
    ],
  },
];

export const ALL_PAGES: DocPage[] = DOCS.flatMap((s) => s.pages);
