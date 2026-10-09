/**
 * Paper size for a silent print (TK-184). Receipts and labels carry their own
 * `@page { size: 80mm auto }` or `size: 50mm 25mm`; a silent print has no
 * dialog to pick paper in, so the size is read from the page and given to the
 * printer exactly. `auto` height is the page's own measured height, which is
 * what a receipt roll wants.
 */
export interface PageSizeMicrons {
  readonly width: number;
  readonly height: number;
}

export interface DeclaredPageSize {
  readonly widthMm: number | null;
  /** Null for `auto` or no height. */
  readonly heightMm: number | null;
}

const PAGE_RULE = /@page\s*\{[^}]*?\bsize\s*:\s*([^;}]+)/i;
const TO_MM: Readonly<Record<string, number>> = { mm: 1, cm: 10, in: 25.4, px: 25.4 / 96, pt: 25.4 / 72 };
/** Electron refuses a custom page side under 353 microns. */
const MIN_MICRONS = 353;

export function declaredPageSize(html: string): DeclaredPageSize {
  const match = PAGE_RULE.exec(html);

  if (!match) {
    return { widthMm: null, heightMm: null };
  }

  const [width, height] = match[1].trim().split(/\s+/).map(lengthInMm);

  return { widthMm: width ?? null, heightMm: width != null ? (height ?? null) : null };
}

function lengthInMm(token: string): number | null {
  const match = /^(\d+(?:\.\d+)?)(mm|cm|in|px|pt)$/i.exec(token);
  return match ? Number(match[1]) * TO_MM[match[2].toLowerCase()] : null;
}

/** The page size to print at, or null to leave it to the printer (A4, letter, a page with no rule). */
export function printPageSize(html: string, contentHeightPx: number): PageSizeMicrons | null {
  const declared = declaredPageSize(html);

  if (declared.widthMm === null) {
    return null;
  }

  const heightMm = declared.heightMm ?? (Math.max(contentHeightPx, 1) * 25.4) / 96;

  return {
    width: Math.max(MIN_MICRONS, Math.round(declared.widthMm * 1000)),
    height: Math.max(MIN_MICRONS, Math.ceil(heightMm * 1000)),
  };
}

/** The hidden print window's width, so the page lays out at the paper's width. */
export function layoutWidthPx(html: string): number {
  const { widthMm } = declaredPageSize(html);
  return widthMm === null ? 800 : Math.max(120, Math.ceil((widthMm * 96) / 25.4));
}
