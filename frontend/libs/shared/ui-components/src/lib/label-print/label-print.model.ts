/**
 * Labels at an exact size (TK-159): the shapes Printing's `api/print/labels`
 * speaks. Pure, so a page can build its labels and test them without a DOM.
 */

export type LabelKind = 'PriceTag' | 'ShelfLabel' | 'BatchExpiry' | 'JewelleryTag';

export const LABEL_KINDS: readonly { value: LabelKind; label: string }[] = [
  { value: 'PriceTag', label: 'Price tag' },
  { value: 'ShelfLabel', label: 'Shelf label' },
  { value: 'BatchExpiry', label: 'Batch and expiry' },
  { value: 'JewelleryTag', label: 'Jewellery tag' },
];

/** What goes on one label, and how many of it. The page sends what it already shows. */
export interface LabelData {
  itemCode?: string | null;
  name: string;
  /** What the barcode carries; the item code when absent. */
  barcode?: string | null;
  price?: number | null;
  mrp?: number | null;
  batchNo?: string | null;
  /** ISO `yyyy-MM-dd`. */
  expiryDate?: string | null;
  manufactureDate?: string | null;
  serialNo?: string | null;
  grossWeightGrams?: number | null;
  netWeightGrams?: number | null;
  purity?: string | null;
  huid?: string | null;
  copies: number;
}

export interface LabelTemplate {
  labelTemplateId: number;
  kind: LabelKind;
  name: string;
  widthMm: number;
  heightMm: number;
  gapMm: number;
  columns: number;
  columnGapMm: number;
  isDefault: boolean;
}

export interface PrintLabelsRequest {
  kind: LabelKind;
  labelTemplateId?: number | null;
  sheet: boolean;
  labels: LabelData[];
}

export interface PrintLabelsResponse {
  html: string;
  count: number;
}

/** The most labels one request prints; the server refuses more. */
export const MAX_LABELS = 5000;

/** Labels the request will print, copies included. */
export function totalCopies(labels: readonly LabelData[]): number {
  return labels.reduce((sum, label) => sum + Math.max(1, Math.floor(label.copies || 1)), 0);
}

/** The size a template prints at, as a person reads it: `50 × 25 mm, 2 across`. */
export function sizeLabel(template: Pick<LabelTemplate, 'widthMm' | 'heightMm' | 'columns'>): string {
  const across = template.columns > 1 ? `, ${template.columns} across` : '';
  return `${template.widthMm} × ${template.heightMm} mm${across}`;
}

/** The templates for one kind, its default first. */
export function templatesFor(templates: readonly LabelTemplate[], kind: LabelKind): LabelTemplate[] {
  return templates.filter((t) => t.kind === kind).sort((a, b) => Number(b.isDefault) - Number(a.isDefault));
}

/**
 * One label per unit received (TK-159): a goods receipt or bill line of 12
 * becomes 12 copies. Fractions — kilograms of rice — print one label.
 */
export function copiesForQuantity(quantity: number | null | undefined): number {
  const whole = Math.floor(Number(quantity) || 0);
  return whole >= 1 && Number(quantity) === whole ? whole : 1;
}
