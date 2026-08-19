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
    title: 'Accounting',
    pages: [{ slug: 'accounting', title: 'Accounting', status: 'built' }],
  },
  {
    title: 'Purchase',
    pages: [{ slug: 'purchase', title: 'Purchase', status: 'built' }],
  },
  {
    title: 'Releases',
    pages: [{ slug: 'releases', title: 'Release notes', status: 'built' }],
  },
  {
    title: 'Development',
    pages: [{ slug: 'development', title: 'Development', status: 'built' }],
  },
];

export const ALL_PAGES: DocPage[] = DOCS.flatMap((s) => s.pages);
