import { LabelData, LabelKind } from '@bill-book/ui-components';

/** The fields of an item list row a label needs. */
export interface ItemLabelSource {
  itemId: number;
  itemCode: string;
  itemName: string;
  itemProfile: string;
  salesPrice: number | null;
  mrp: number | null;
}

/**
 * Labels for the items list (TK-159): the ticked items, or every listed item
 * when none is ticked. One copy each to start; the dialog sets how many. The
 * barcode carries the item code — the list does not carry the items' own
 * barcodes yet (TK-164).
 */
export function itemListLabels(rows: readonly ItemLabelSource[], picked: ReadonlySet<number>): LabelData[] {
  const chosen = picked.size > 0 ? rows.filter((row) => picked.has(row.itemId)) : rows;
  return chosen.map((row) => ({
    itemCode: row.itemCode,
    name: row.itemName,
    price: row.salesPrice,
    mrp: row.mrp,
    copies: 1,
  }));
}

/** The label to start on: a jewellery tag when every item is jewellery, a batch label for medicines, else a price tag. */
export function labelKindFor(profiles: readonly string[]): LabelKind {
  if (profiles.length > 0 && profiles.every((p) => p === 'Jewellery')) return 'JewelleryTag';
  if (profiles.length > 0 && profiles.every((p) => p === 'Pharma')) return 'BatchExpiry';
  return 'PriceTag';
}

/** One label for the item open in the form: its primary barcode, and a jewellery item's weights and purity. */
export function itemDetailLabel(
  item: ItemLabelSource & {
    barcodes: readonly { barcode: string; isPrimary: boolean; isActive: boolean }[];
    jewellery: Record<string, unknown> | null;
  },
  purityName: string | null,
): LabelData {
  const barcode =
    item.barcodes.find((b) => b.isPrimary && b.isActive && b.barcode.trim() !== '')?.barcode.trim() ??
    item.barcodes.find((b) => b.isActive && b.barcode.trim() !== '')?.barcode.trim() ??
    null;
  const weight = (key: string): number | null => {
    const value = Number(item.jewellery?.[key]);
    return Number.isFinite(value) && value > 0 ? value : null;
  };

  return {
    itemCode: item.itemCode,
    name: item.itemName,
    barcode,
    price: item.salesPrice,
    mrp: item.mrp,
    grossWeightGrams: weight('grossWeight'),
    netWeightGrams: weight('netWeight'),
    purity: purityName,
    copies: 1,
  };
}

/** An item batch as `GET internal/item-batches` returns it. */
export interface BatchLabelSource {
  batchNumber: string;
  manufactureDate?: string | null;
  expiryDate?: string | null;
  mrp?: number | null;
  isActive?: boolean;
}

/** An item serial as `GET internal/item-serials` returns it. */
export interface SerialLabelSource {
  serialNumber: string;
  hallmarkNumber?: string | null;
  status?: string | number | null;
}

/**
 * One label per active batch (TK-159): its number, dates and MRP, the barcode
 * carrying the item's own barcode so the till still finds the item.
 */
export function batchLabels(item: LabelData, batches: readonly BatchLabelSource[]): LabelData[] {
  return batches
    .filter((b) => b.isActive !== false)
    .map((b) => ({
      ...item,
      batchNo: b.batchNumber,
      manufactureDate: b.manufactureDate ?? null,
      expiryDate: b.expiryDate ?? null,
      mrp: b.mrp ?? item.mrp ?? null,
      copies: 1,
    }));
}

/**
 * One label per serial still in stock (TK-159), the barcode carrying the
 * serial itself — a serial label identifies the one piece — and a jewellery
 * piece's HUID beside it.
 */
export function serialLabels(item: LabelData, serials: readonly SerialLabelSource[]): LabelData[] {
  return serials
    .filter((s) => s.status === undefined || s.status === null || s.status === 'InStock' || s.status === 0)
    .map((s) => ({
      ...item,
      serialNo: s.serialNumber,
      barcode: s.serialNumber,
      huid: s.hallmarkNumber ?? null,
      copies: 1,
    }));
}
