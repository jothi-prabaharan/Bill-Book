/**
 * The till receipt as fixed-width text (TK-41): what goes on the paper, as
 * plain strings that can be tested, which `receiptHtml` turns into a page the
 * browser prints at the roll's exact width (TK-155).
 *
 * **Every figure comes from the posted invoice** (`GET api/sales/invoices/{id}`),
 * never from the cart: the server recomputes GST and rounds the total, and the
 * receipt is the customer's copy of what was posted. A reprint reads the same
 * invoice, so it prints the same receipt.
 */

/** 58 mm paper holds 32 characters in font A; 80 mm holds 48. */
export type PaperWidth = 58 | 80;

export function columnsFor(paper: PaperWidth): number {
  return paper === 58 ? 32 : 48;
}

/** The branch header, from `GET api/organizations/current`. */
export interface ReceiptBranch {
  name: string;
  addressLine1?: string | null;
  addressLine2?: string | null;
  city?: string | null;
  postalCode?: string | null;
  phoneNumber?: string | null;
  mobileNumber?: string | null;
  gstin?: string | null;
}

export interface ReceiptLineTax {
  taxComponent: string;
  rate: number;
  taxableAmount: number;
  amount: number;
}

export interface ReceiptLine {
  itemLabel?: string | null;
  description?: string | null;
  hsnSacCode?: string | null;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  taxes: ReceiptLineTax[];
}

export interface ReceiptTender {
  mode: string;
  amount: number;
  reference?: string | null;
}

/** The fields of the invoice view the receipt prints. */
export interface ReceiptSale {
  documentNo: string;
  documentDate: string;
  contactName?: string | null;
  contactGstin?: string | null;
  status: string;
  subTotal: number;
  discountAmount: number;
  taxableAmount: number;
  cgstAmount: number;
  sgstAmount: number;
  igstAmount: number;
  cessAmount: number;
  roundOffAmount: number;
  totalAmount: number;
  changeAmount?: number | null;
  lines: ReceiptLine[];
  tenders: ReceiptTender[];
}

export type RowStyle = 'normal' | 'bold' | 'title';

/** One printed row: already padded to the paper's width, except centred rows. */
export interface ReceiptRow {
  text: string;
  align: 'left' | 'center';
  style: RowStyle;
}

export interface ReceiptOptions {
  paper: PaperWidth;
  /** Printed above the header on a reprint, so a copy is never taken for the original. */
  reprint?: boolean;
  footer?: string;
}

/**
 * The rows of one receipt.
 *
 * Layout, top to bottom: the branch (name, address, phone, GSTIN), the bill
 * number and date, the customer when it is not the walk-in, one block per line
 * (the name, then quantity × rate and the amount), the totals, the GST split
 * per component and rate, the round-off, the total, each tender, the change,
 * and the footer.
 */
export function receiptRows(branch: ReceiptBranch, sale: ReceiptSale, options: ReceiptOptions): ReceiptRow[] {
  const width = columnsFor(options.paper);
  const rows: ReceiptRow[] = [];
  const rule = (char = '-') => rows.push(left(char.repeat(width)));
  const center = (text: string, style: RowStyle = 'normal') => {
    for (const piece of wrap(text, width)) {
      rows.push({ text: piece, align: 'center', style });
    }
  };
  const pair = (label: string, amount: string, style: RowStyle = 'normal') =>
    rows.push({ text: twoColumns(label, amount, width), align: 'left', style });

  if (options.reprint) {
    center('*** DUPLICATE ***', 'bold');
  }

  center(branch.name, 'title');
  const address = [branch.addressLine1, branch.addressLine2, [branch.city, branch.postalCode].filter(Boolean).join(' ')]
    .map((part) => (part ?? '').trim())
    .filter((part) => part.length > 0);
  for (const part of address) {
    center(part);
  }
  const phone = branch.mobileNumber ?? branch.phoneNumber;
  if (phone) {
    center(`Ph: ${phone}`);
  }
  if (branch.gstin) {
    center(`GSTIN: ${branch.gstin}`);
  }

  center(sale.status === 'Voided' ? 'VOID - TAX INVOICE' : 'TAX INVOICE', 'bold');
  rule();
  pair(`Bill: ${sale.documentNo}`, sale.documentDate);
  if (sale.contactName && !isWalkIn(sale.contactName)) {
    rows.push(left(clip(`To: ${sale.contactName}`, width)));
  }
  if (sale.contactGstin) {
    rows.push(left(clip(`GSTIN: ${sale.contactGstin}`, width)));
  }
  rule();

  for (const line of sale.lines) {
    const name = lineName(line);
    for (const piece of wrap(name, width)) {
      rows.push(left(piece));
    }
    pair(`  ${qty(line.quantity)} x ${money(line.unitPrice)}`, money(line.lineTotal));
  }

  rule();
  pair('Sub total', money(sale.subTotal));
  if (sale.discountAmount !== 0) {
    pair('Discount', money(-sale.discountAmount));
  }
  pair('Taxable', money(sale.taxableAmount));
  for (const tax of gstSummary(sale.lines)) {
    pair(`${tax.component} @${rate(tax.rate)}%`, money(tax.amount));
  }
  if (sale.roundOffAmount !== 0) {
    pair('Round off', money(sale.roundOffAmount));
  }
  rule('=');
  pair('TOTAL', money(sale.totalAmount), 'title');
  rule('=');

  for (const tender of sale.tenders) {
    pair(tender.reference ? `${tender.mode} (${tender.reference})` : tender.mode, money(tender.amount));
  }
  if ((sale.changeAmount ?? 0) > 0) {
    pair('Change', money(sale.changeAmount ?? 0), 'bold');
  }

  rows.push(left(''));
  center(options.footer ?? 'Thank you. Visit again.');

  return rows;
}

/** GST per component and rate, in the order CGST, SGST, IGST, CESS, then by rate. */
export function gstSummary(lines: readonly ReceiptLine[]): { component: string; rate: number; amount: number }[] {
  const order = ['Cgst', 'Sgst', 'Igst', 'Cess'];
  const totals = new Map<string, { component: string; rate: number; amount: number }>();

  for (const line of lines) {
    for (const tax of line.taxes) {
      const key = `${tax.taxComponent}|${tax.rate}`;
      const entry = totals.get(key) ?? { component: tax.taxComponent.toUpperCase(), rate: tax.rate, amount: 0 };
      entry.amount = round2(entry.amount + tax.amount);
      totals.set(key, entry);
    }
  }

  const rank = (component: string) => {
    const at = order.findIndex((name) => name.toUpperCase() === component);
    return at < 0 ? order.length : at;
  };

  return [...totals.values()]
    .filter((entry) => entry.amount !== 0)
    .sort((a, b) => rank(a.component) - rank(b.component) || a.rate - b.rate);
}

/**
 * Text that keeps the receipt's columns straight: anything outside printable
 * ASCII becomes `?` (and the rupee sign `Rs`), because the columns are counted
 * in characters of one width. The browser could draw a Tamil or Chinese name,
 * but its glyphs are wider than a monospace cell and would push the amounts
 * out of line; that waits for a layout that measures rather than counts.
 */
export function printable(text: string): string {
  return text.replace(/₹/g, 'Rs').replace(/[^\x20-\x7E]/g, '?');
}

/** A label on the left and an amount on the right, the label clipped to make room. */
export function twoColumns(label: string, amount: string, width: number): string {
  const room = Math.max(0, width - amount.length - 1);
  return clip(label, room).padEnd(room, ' ') + ' ' + amount;
}

/** Splits text into rows no wider than `width`, at spaces where it can. */
export function wrap(text: string, width: number): string[] {
  const clean = printable(text).trim();
  if (clean.length === 0) {
    return [''];
  }

  const rows: string[] = [];
  let rest = clean;
  while (rest.length > width) {
    const space = rest.lastIndexOf(' ', width);
    const cut = space > 0 ? space : width;
    rows.push(rest.slice(0, cut).trimEnd());
    rest = rest.slice(cut).trimStart();
  }
  rows.push(rest);
  return rows;
}

function left(text: string): ReceiptRow {
  return { text, align: 'left', style: 'normal' };
}

function clip(text: string, width: number): string {
  const clean = printable(text);
  return clean.length <= width ? clean : clean.slice(0, width);
}

function lineName(line: ReceiptLine): string {
  // `ItemLabel` is "CODE - Name"; the name alone is what a customer reads.
  const label = line.itemLabel ?? '';
  const dash = label.indexOf(' - ');
  const name = dash >= 0 ? label.slice(dash + 3) : label;
  return line.description?.trim() || name || 'Item';
}

function isWalkIn(name: string): boolean {
  return /walk[\s-]?in/i.test(name);
}

function money(amount: number): string {
  return round2(amount).toFixed(2);
}

function qty(quantity: number): string {
  return Number.isInteger(quantity) ? String(quantity) : String(Number(quantity.toFixed(3)));
}

function rate(value: number): string {
  return Number.isInteger(value) ? String(value) : String(Number(value.toFixed(2)));
}

function round2(value: number): number {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}

/**
 * The codes printed under a receipt's text (TK-164), each an SVG drawn by the
 * `-ui` lib: the invoice number as a barcode, so a return or a reprint finds
 * the sale by scanning it, and on a sale with something still owed a UPI QR.
 */
export interface ReceiptCodes {
  barcodeSvg?: string | null;
  upiQrSvg?: string | null;
}

/** The SVG as an image source, so its markup never mixes with the receipt's. */
export function svgDataUri(svg: string): string {
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
}

/**
 * The receipt as a page the browser prints (TK-155): one monospace row per
 * receipt row, the page exactly the roll's width (`@page { size: 80mm auto;
 * margin: 0 }`) so the operating system's driver feeds a receipt printer the
 * way it would any other page. No printer commands, no desktop app.
 */
export function receiptHtml(rows: readonly ReceiptRow[], paper: PaperWidth, codes?: ReceiptCodes | null): string {
  const body = rows
    .map((row) => {
      const classes = [row.align === 'center' ? 'c' : '', row.style === 'bold' ? 'b' : '', row.style === 'title' ? 't' : '']
        .filter(Boolean)
        .join(' ');
      const text = escapeHtml(row.text) || '&nbsp;';
      return classes ? `<div class="${classes}">${text}</div>` : `<div>${text}</div>`;
    })
    .join('\n');

  // The text area is a little narrower than the roll: receipt printers leave
  // a few millimetres unprinted at each edge.
  const textWidth = paper === 58 ? 48 : 72;

  const images: string[] = [];
  if (codes?.barcodeSvg) {
    images.push(`<img class="bar" alt="Invoice barcode" src="${svgDataUri(codes.barcodeSvg)}">`);
  }
  if (codes?.upiQrSvg) {
    images.push(`<img class="qr" alt="UPI payment QR code" src="${svgDataUri(codes.upiQrSvg)}">`);
    images.push('<div class="c">Scan to pay with any UPI app</div>');
  }
  const codeBlock = images.length > 0 ? `\n<section class="codes">\n${images.join('\n')}\n</section>` : '';

  return `<!doctype html>
<html><head><meta charset="utf-8"><title>Receipt</title>
<style>
@page { size: ${paper}mm auto; margin: 0; }
html, body { margin: 0; padding: 0; background: #fff; color: #000; }
body { width: ${textWidth}mm; margin: 0 auto; padding: 2mm 0 6mm; font: 9pt/1.25 "Courier New", Courier, monospace; }
div { white-space: pre; overflow: hidden; }
.c { text-align: center; }
.b { font-weight: bold; }
.t { font-weight: bold; font-size: 11pt; }
.codes { margin-top: 3mm; text-align: center; }
.codes img { display: block; margin: 0 auto 2mm; }
.bar { width: 100%; max-width: ${textWidth}mm; height: 12mm; }
.qr { width: ${paper === 58 ? 28 : 34}mm; height: ${paper === 58 ? 28 : 34}mm; }
</style></head>
<body>
${body}${codeBlock}
</body></html>`;
}

function escapeHtml(text: string): string {
  return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}
