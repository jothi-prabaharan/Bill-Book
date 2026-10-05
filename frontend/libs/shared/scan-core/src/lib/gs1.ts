/**
 * Reading a GS1 code (TK-162): the DataMatrix on a pharma pack, or a GS1-128
 * carton label, carries the product, its batch and its expiry in one symbol.
 *
 * Understood: `(01)` GTIN, `(10)` batch or lot, `(17)` expiry, `(11)`
 * production date, `(21)` serial. Accepted in the raw form a camera reads —
 * an optional `]d2` / `]C1` / `]Q3` prefix, variable fields ended by the GS
 * separator (ASCII 29) or by the end — and in the bracketed form printed under
 * the symbol, `(01)08901234567890(17)270331(10)B12`. Anything else is not GS1,
 * and an AI not listed above ends the reading with what was understood.
 *
 * Pure, so a `-core` lib can use it.
 */
export interface Gs1Data {
  /** The 14-digit GTIN as encoded. */
  gtin: string;
  batch: string | null;
  /** ISO `yyyy-MM-dd`; a day of 00 is the month's last day, as GS1 says. */
  expiry: string | null;
  productionDate: string | null;
  serial: string | null;
}

const GS = '\u001d';

/** Fixed-length AIs and their data lengths; the rest below are variable (up to 20). */
const FIXED: Record<string, number> = { '01': 14, '11': 6, '17': 6 };
const VARIABLE = new Set(['10', '21']);

/** The GS1 data in a scanned code, or null when it is not a GS1 code with a GTIN. */
export function parseGs1(raw: string | null | undefined): Gs1Data | null {
  const text = (raw ?? '').trim().replace(/^\][a-zA-Z]\d/, '');
  if (!text) {
    return null;
  }
  const fields = text.startsWith('(') ? bracketed(text) : elementString(text);
  if (!fields) {
    return null;
  }
  const gtin = fields.get('01');
  if (!gtin || !/^\d{14}$/.test(gtin)) {
    return null;
  }
  return {
    gtin,
    batch: fields.get('10') ?? null,
    expiry: gs1Date(fields.get('17')),
    productionDate: gs1Date(fields.get('11')),
    serial: fields.get('21') ?? null,
  };
}

/**
 * The codes an item's barcode could be stored as for a GTIN-14: the GTIN
 * itself, and the EAN-13 inside it when it starts with a 0 — which is how a
 * retail pack's barcode is usually kept on the item.
 */
export function gtinCandidates(gtin: string): string[] {
  return gtin.length === 14 && gtin.startsWith('0') ? [gtin.slice(1), gtin] : [gtin];
}

function bracketed(text: string): Map<string, string> | null {
  const fields = new Map<string, string>();
  const pattern = /\((\d{2})\)([^(]*)/g;
  let consumed = 0;
  for (let m = pattern.exec(text); m; m = pattern.exec(text)) {
    if (m.index !== consumed) {
      return null;
    }
    consumed = m.index + m[0].length;
    const ai = m[1];
    if (!(ai in FIXED) && !VARIABLE.has(ai)) {
      break;
    }
    fields.set(ai, m[2].trim());
  }
  return fields.size > 0 ? fields : null;
}

function elementString(text: string): Map<string, string> | null {
  if (!text.startsWith('01')) {
    return null;
  }
  const fields = new Map<string, string>();
  let at = 0;
  while (at < text.length) {
    if (text[at] === GS) {
      at++;
      continue;
    }
    const ai = text.slice(at, at + 2);
    at += 2;
    const fixed = FIXED[ai];
    if (fixed !== undefined) {
      const value = text.slice(at, at + fixed);
      if (value.length !== fixed) {
        return fields.size > 0 ? fields : null;
      }
      fields.set(ai, value);
      at += fixed;
    } else if (VARIABLE.has(ai)) {
      const end = text.indexOf(GS, at);
      const stop = end < 0 ? text.length : end;
      fields.set(ai, text.slice(at, Math.min(stop, at + 20)));
      at = stop;
    } else {
      break;
    }
  }
  return fields;
}

function gs1Date(value: string | undefined): string | null {
  if (!value || !/^\d{6}$/.test(value)) {
    return null;
  }
  const yy = Number(value.slice(0, 2));
  const mm = Number(value.slice(2, 4));
  let dd = Number(value.slice(4, 6));
  if (mm < 1 || mm > 12) {
    return null;
  }
  // GS1's century rule, simplified for dates this side of 2050.
  const year = yy >= 51 ? 1900 + yy : 2000 + yy;
  const last = new Date(Date.UTC(year, mm, 0)).getUTCDate();
  if (dd === 0) {
    dd = last;
  }
  if (dd > last) {
    return null;
  }
  return `${year}-${String(mm).padStart(2, '0')}-${String(dd).padStart(2, '0')}`;
}
