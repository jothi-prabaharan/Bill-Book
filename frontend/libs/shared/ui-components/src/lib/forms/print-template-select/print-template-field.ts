import type { BbSelectOption } from '../field.model';

/**
 * What the Print template field works from: one layout a document type can print with, as the document's
 * own module offers it (TK-265). Structural, so this file needs nothing from the API library.
 */
export interface PrintTemplateOffer {
  printTemplateId: number;
  templateName: string;
  paperSizeName?: string | null;
  isSample?: boolean | null;
  isDefault: boolean;
}

/** The empty row: the document names no template, so Printing uses the branch's default for its type. */
export const NO_TEMPLATE_LABEL = 'Branch default';

/** Shown for a template a document names that is no longer offered (deleted or switched off). */
export const GONE_TEMPLATE_LABEL = 'The template chosen earlier is no longer available';

/** A paper size as a person reads it. Unknown names are shown as they are. */
export function paperSizeLabel(name: string | null | undefined): string {
  switch (name) {
    case 'Thermal3Inch':
      return 'Thermal 3″ (80 mm)';
    case 'Thermal4Inch':
      return 'Thermal 4″ (104 mm)';
    case null:
    case undefined:
      return '';
    default:
      return name;
  }
}

/**
 * How a choice reads in the list: its name, its paper size unless the name already says it, then Sample
 * and Default where they apply. "Tax invoice · A4 · Sample · Default".
 */
export function printTemplateLabel(offer: PrintTemplateOffer): string {
  const parts = [offer.templateName];

  const paper = paperSizeLabel(offer.paperSizeName);
  if (paper !== '') {
    const token = paper.split(' ')[0].toLowerCase();
    if (!offer.templateName.toLowerCase().includes(token)) {
      parts.push(paper);
    }
  }

  if (offer.isSample) {
    parts.push('Sample');
  }

  if (offer.isDefault) {
    parts.push('Default');
  }

  return parts.join(' · ');
}

/** The layout a new document starts with: the branch's default, else the first one offered. Null when none. */
export function pickDefaultTemplate(offers: readonly PrintTemplateOffer[]): number | null {
  const pick = offers.find((offer) => offer.isDefault) ?? offers[0];
  return pick?.printTemplateId ?? null;
}

/**
 * The list the field shows. A template the document already names but the module no longer offers is kept
 * as a disabled row, so an edit screen shows what the document has rather than a blank that looks like "none".
 * Only once the list has loaded: while it has not, or when it failed, nothing can be said about it.
 */
export function printTemplateOptions(
  offers: readonly PrintTemplateOffer[],
  saved: number | null,
  loaded: boolean,
): BbSelectOption<number>[] {
  const options: BbSelectOption<number>[] = offers.map((offer) => ({
    value: offer.printTemplateId,
    label: printTemplateLabel(offer),
  }));

  if (loaded && saved !== null && !offers.some((offer) => offer.printTemplateId === saved)) {
    options.unshift({ value: saved, label: GONE_TEMPLATE_LABEL, disabled: true });
  }

  return options;
}
