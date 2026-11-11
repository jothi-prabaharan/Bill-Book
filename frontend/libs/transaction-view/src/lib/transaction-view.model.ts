/**
 * The pure half of the view page (TK-263): what the server sends, how the document is set on its desk, how it
 * is scaled to the screen, and what the page says about a document that is not final. No DOM and no
 * injection, so each rule can be tested on its own.
 */

/**
 * A document laid out by its print template, as a service's `view` route answers it. `html` is exactly what
 * Printing rendered, which is what prints; the other fields say which document it is.
 */
export interface TransactionView {
  /** The document's pages as HTML. */
  html: string;

  pageCount: number;

  /** Fields the template names that this document could not fill. They print as nothing. */
  unknownTags: string[];

  /** Null when the layout was the platform's own, not a stored template. */
  printTemplateId: number | null;

  /** The name of the template the document was laid out with. Null with the platform's own layout (TK-265). */
  printTemplateName?: string | null;

  /**
   * The template the document itself names, set on its create screen. Null when it names none, and then the
   * branch's default is used. When it is set and differs from `printTemplateId`, the one it names could not
   * be used (TK-265).
   */
  chosenPrintTemplateId?: number | null;

  /** Null while a receipt, payment or journal is a draft: none is issued until it posts. */
  documentNo: string | null;

  documentTypeCode: string | null;

  /** Draft, ReadyToPost, Posted, Void or Reversed. */
  status: string | null;
}

/** The colour of the desk the pages lie on. Paper is white whatever the theme, so the desk is one grey too. */
export const DESK_COLOUR = '#d4d7dd';

/** The gap between two pages on the desk, in pixels. */
export const PAGE_GAP = 16;

/**
 * Sets the document the template produced on a desk, the way a PDF viewer does: pages one under another with
 * a gap and a shadow, on grey.
 *
 * <b>The pages are the renderer's own and are not touched</b> — their size, their margins and their
 * `@page` rule come through as they were, so what is seen here is what prints. The gap and the shadow are
 * for the screen only; a print rule takes them away so the pages meet exactly, as they did before.
 */
export function viewerDocument(html: string): string {
  return (
    '<!doctype html><html><head><meta charset="utf-8"><style>' +
    `html{background:${DESK_COLOUR}}` +
    'body{margin:0}' +
    `.pt-page+.pt-page{margin-top:${PAGE_GAP}px}` +
    '.pt-page{box-shadow:0 1px 4px rgba(0,0,0,.3)}' +
    '@media print{html{background:#fff}.pt-page+.pt-page{margin-top:0}.pt-page{box-shadow:none}}' +
    `</style></head><body>${html}</body></html>`
  );
}

/** The smallest and largest the paper may be scaled to. */
export const MIN_ZOOM = 0.25;
export const MAX_ZOOM = 3;

const ZOOM_STEPS: readonly number[] = [0.25, 0.5, 0.75, 1, 1.25, 1.5, 2, 3];

/**
 * The scale that fits a paper of this width into the space there is, never above life size: a phone shows the
 * whole width of the page, a wide screen shows it at its own size rather than blown up.
 */
export function fitScale(available: number, paperWidth: number): number {
  if (!(available > 0) || !(paperWidth > 0)) {
    return 1;
  }

  return Math.min(1, Math.max(MIN_ZOOM, available / paperWidth));
}

/** Keeps a scale between the smallest and largest the viewer allows. */
export function clampZoom(zoom: number): number {
  return Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, zoom));
}

/** The next step up (`1`) or down (`-1`) from a scale, stopping at the ends. */
export function nextZoom(current: number, direction: 1 | -1): number {
  const epsilon = 0.001;

  if (direction === 1) {
    return ZOOM_STEPS.find((step) => step > current + epsilon) ?? ZOOM_STEPS[ZOOM_STEPS.length - 1];
  }

  return [...ZOOM_STEPS].reverse().find((step) => step < current - epsilon) ?? ZOOM_STEPS[0];
}

/** A scale as a percentage for the control: `0.755` → `76`. */
export function zoomPercent(scale: number): number {
  return Math.round(scale * 100);
}

/** Whether a document in this status can still be edited: only a draft, or one waiting to be posted. */
export function isEditableStatus(status: string | null | undefined): boolean {
  return status === 'Draft' || status === 'ReadyToPost';
}

/** The words for a status. */
export function statusLabel(status: string | null | undefined): string {
  switch (status) {
    case 'ReadyToPost':
      return 'Ready to post';
    case null:
    case undefined:
    case '':
      return '';
    default:
      return status;
  }
}

/** The colour class a status wears, the same on every screen: it went well, it needs a look, it went wrong, or it is only information. */
export function statusTone(status: string | null | undefined): string {
  switch (status) {
    case 'Posted':
      return 'tag-success';
    case 'Void':
      return 'tag-danger';
    case 'Draft':
    case 'ReadyToPost':
    case 'Reversed':
      return 'tag-warning';
    default:
      return 'tag-info';
  }
}

/**
 * What the page says above a document that is not final, or null for a posted one. The paper itself is
 * stamped by the server (DRAFT, VOID, REVERSED) whatever the template holds; this says it in words too, so
 * nobody has to read a watermark to know.
 */
export function statusNotice(label: string, status: string | null | undefined): string | null {
  switch (status) {
    case 'Draft':
    case 'ReadyToPost':
      return `This ${label} is a draft. It is stamped so, and is not final until it is posted.`;
    case 'Void':
      return `This ${label} is void.`;
    case 'Reversed':
      return `This ${label} has been reversed by another entry.`;
    default:
      return null;
  }
}

/** What the page says about the template a document was laid out with. */
export interface TemplateNote {
  text: string;

  /** True when the layout is not the one the document asked for, or is no stored template at all. */
  warning: boolean;
}

/**
 * Names the template a document printed with, and says so plainly when it is not the one the document names
 * (TK-265). Printing falls back from the template a document names to the branch's default and then to its
 * own generated layout, so that a document never becomes unprintable; the fallback stays, but it is never
 * silent: a person who chose a design and sees another is told why.
 */
export function templateNote(
  label: string,
  view: Pick<TransactionView, 'printTemplateId' | 'printTemplateName' | 'chosenPrintTemplateId'>,
): TemplateNote {
  const used = view.printTemplateId ?? null;
  const chosen = view.chosenPrintTemplateId ?? null;
  const name = view.printTemplateName?.trim() || null;

  if (used === null) {
    return {
      warning: true,
      text:
        chosen === null
          ? `No print template is set up for this kind of document, so this ${label} is laid out in the standard layout.`
          : `The template chosen for this ${label} is no longer available, and none is set as the default, so it is laid out in the standard layout.`,
    };
  }

  const shown = name ?? 'its template';

  if (chosen !== null && chosen !== used) {
    return {
      warning: true,
      text: `The template chosen for this ${label} is no longer available, so it is laid out with “${shown}”, the branch default.`,
    };
  }

  return {
    warning: false,
    text:
      chosen === null
        ? `Template: ${shown}, the branch default. No template was chosen for this ${label}.`
        : `Template: ${shown}.`,
  };
}

/** The heading: the document's name and number, or a plain note while it has no number yet. */
export function documentHeading(label: string, documentNo: string | null | undefined): string {
  const name = label.charAt(0).toUpperCase() + label.slice(1);
  return documentNo ? `${name} ${documentNo}` : `${name} (no number yet)`;
}
