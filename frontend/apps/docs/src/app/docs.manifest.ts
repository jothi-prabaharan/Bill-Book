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
    pages: [
      { slug: 'index', title: 'Introduction', status: 'built' },
      { slug: 'architecture', title: 'Architecture', status: 'built' },
      { slug: 'status', title: 'Build status', status: 'built' },
    ],
  },
  {
    title: 'Platform',
    pages: [
      { slug: 'tenancy', title: 'Tenancy model', status: 'built' },
      { slug: 'auth', title: 'Authentication', status: 'built' },
      { slug: 'signup', title: 'Signup & provisioning', status: 'built' },
      { slug: 'licensing', title: 'Licensing & trial expiry', status: 'built' },
      { slug: 'email', title: 'Email & invitations', status: 'built' },
    ],
  },
  {
    title: 'Masters',
    pages: [
      { slug: 'masters', title: 'Master data', status: 'partial' },
      { slug: 'currencies', title: 'Currencies', status: 'built' },
      { slug: 'roles-permissions', title: 'Roles & permissions', status: 'built' },
      { slug: 'configuration', title: 'Configuration', status: 'built' },
    ],
  },
  {
    title: 'Accounting',
    pages: [
      { slug: 'chart-of-accounts', title: 'Chart of accounts', status: 'planned' },
      { slug: 'ledger', title: 'Journal & ledger', status: 'planned' },
      { slug: 'gst', title: 'GST & tax', status: 'planned' },
    ],
  },
  {
    title: 'Releases',
    pages: [{ slug: 'release-notes', title: 'Release notes', status: 'built' }],
  },
  {
    title: 'Development',
    pages: [
      { slug: 'running-locally', title: 'Running locally', status: 'built' },
      { slug: 'conventions', title: 'Conventions', status: 'built' },
    ],
  },
];

export const ALL_PAGES: DocPage[] = DOCS.flatMap((s) => s.pages);
