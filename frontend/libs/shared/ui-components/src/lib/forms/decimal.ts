/**
 * Decimal text in, exact numbers out — without floating point in the middle.
 *
 * **Why this exists rather than `parseFloat` and a multiply.** Bill-Book holds
 * money two ways: accounting works in decimal rupees, and the sales and
 * purchase line grid works in **integer paise**, because a document total that
 * ties to a ledger cannot be summed in binary floating point. Converting
 * between the two with arithmetic is where the corruption gets in —
 * `parseFloat('1.15') * 100` is `114.99999999999999`, and `Math.round` hides it
 * until the day a figure ends `.145`.
 *
 * So the conversion here is **string surgery**: the digits either side of the
 * point are moved, padded and truncated as text, and `Number()` is called once
 * at the end on a string that is already an integer. Nothing is multiplied and
 * nothing is divided.
 *
 * `minorDigits` is how many decimal places the *stored* value has been scaled
 * by: `0` means the control's value is the decimal itself (accounting's rupees),
 * `2` means it is an integer count of hundredths (the line grid's paise), `6`
 * means millionths (the line grid's quantity).
 */

/** The parts of a decimal literal, with the sign lifted out. */
export interface DecimalParts {
  negative: boolean;
  /** Digits left of the point. Never empty — an empty integer part reads '0'. */
  integer: string;
  /** Digits right of the point, unpadded. May be empty. */
  fraction: string;
}

/** Anything that is not a plain decimal literal, once grouping is stripped. */
const DECIMAL_TEXT = /^[+-]?(\d*)(?:\.(\d*))?$/;

/**
 * Splits decimal text into its parts, or null if it is not a number.
 *
 * Grouping separators and spaces are dropped first, so a value pasted from a
 * formatted display — `1,23,456.78` — parses rather than being rejected. A lone
 * sign, a lone point and an empty string are all null: they are what a
 * half-typed number looks like, and treating them as zero would fight the
 * person typing.
 */
export function parseDecimalParts(text: string): DecimalParts | null {
  // The non-breaking space is written as an escape rather than as itself: a
  // literal one is invisible in a diff and indistinguishable from an ordinary
  // space in every editor. It is here because a figure copied out of a
  // formatted display or a spreadsheet often carries one as its separator.
  const cleaned = text.replace(/[,\s\u00a0]/g, '');

  if (cleaned === '' || cleaned === '-' || cleaned === '+' || cleaned === '.') {
    return null;
  }

  const match = DECIMAL_TEXT.exec(cleaned);
  if (match === null) {
    return null;
  }

  const integer = match[1] ?? '';
  const fraction = match[2] ?? '';

  if (integer === '' && fraction === '') {
    return null;
  }

  return {
    negative: cleaned.startsWith('-'),
    integer: integer === '' ? '0' : integer,
    fraction,
  };
}

/**
 * Rounds a digit string to `places`, half away from zero.
 *
 * Half **away from zero** rather than JavaScript's half **up**, matching
 * `roundHalfAwayFromZero` in `line-math.ts` and `MidpointRounding.AwayFromZero`
 * in `LedgerPostingService`. A round-off line is signed, so the two differ on
 * exactly the values that produce one.
 *
 * Carrying is done on the digit string so a value wider than `Number.MAX_SAFE_INTEGER`
 * still rounds correctly before it is ever handed to `Number`.
 */
function roundDigits(digits: string, places: number): { digits: string; carry: boolean } {
  if (places >= digits.length) {
    return { digits: digits.padEnd(places, '0'), carry: false };
  }

  const kept = digits.slice(0, places);
  const next = digits.charCodeAt(places) - 48;

  if (next < 5) {
    return { digits: kept, carry: false };
  }

  // Increment the kept digits by one, right to left.
  const out = kept.split('');
  let at = out.length - 1;
  while (at >= 0) {
    if (out[at] === '9') {
      out[at] = '0';
      at -= 1;
    } else {
      out[at] = String(Number(out[at]) + 1);
      return { digits: out.join(''), carry: false };
    }
  }

  return { digits: out.join(''), carry: true };
}

/**
 * Decimal text as a number scaled by `minorDigits` decimal places.
 *
 * `('12.345', 2)` is `1235` — the digits moved two places and the rest rounded
 * half away from zero. `('12.345', 0)` is `12.345` itself, because at zero
 * scale the caller wants the decimal and there is nothing to move.
 *
 * Returns null for text that is not a number, which is what an empty or
 * half-typed field gives.
 */
export function textToScaled(text: string, minorDigits: number): number | null {
  const parts = parseDecimalParts(text);
  if (parts === null) {
    return null;
  }

  if (minorDigits <= 0) {
    // No scaling to do. `Number` on the cleaned literal is the closest double
    // to the decimal typed, which is the best a JavaScript number can hold.
    const literal = `${parts.negative ? '-' : ''}${parts.integer}.${parts.fraction || '0'}`;
    const value = Number(literal);
    if (!Number.isFinite(value)) {
      return null;
    }
    // `Number('-0.0')` is -0, which serialises as "-0" and is not Object.is
    // equal to 0. Neither is a difference anybody wants in a form value.
    return value === 0 ? 0 : value;
  }

  const rounded = roundDigits(parts.fraction, minorDigits);
  const integer = rounded.carry ? incrementDigits(parts.integer) : parts.integer;
  const magnitude = Number(`${integer}${rounded.digits}`);

  if (!Number.isFinite(magnitude)) {
    return null;
  }

  // -0 is not a value anybody wants in a form control; it serialises as "-0"
  // and compares unequal to 0 under Object.is.
  return parts.negative && magnitude !== 0 ? -magnitude : magnitude;
}

function incrementDigits(digits: string): string {
  const out = digits.split('');
  let at = out.length - 1;
  while (at >= 0) {
    if (out[at] === '9') {
      out[at] = '0';
      at -= 1;
    } else {
      out[at] = String(Number(out[at]) + 1);
      return out.join('');
    }
  }
  return `1${out.join('')}`;
}

/**
 * A stored value back to the decimal text a person edits.
 *
 * At `minorDigits` zero this is `toFixed`. Above zero the point is *inserted*
 * into the integer's digits rather than the integer being divided, so 125050
 * paise reads `1250.50` exactly and never `1250.4999999999999`.
 *
 * `decimals` is the **minimum** number of places to show, not the maximum. A
 * quantity field showing two places must still show all six of `1.234567` —
 * truncating the display of a value the field is editing is how somebody saves
 * a rounded figure believing they saw the whole one. Trailing zeros beyond
 * `decimals` are dropped, so the common case reads `1.00` rather than
 * `1.000000`.
 */
export function scaledToText(
  value: number | null | undefined,
  minorDigits: number,
  decimals: number | null,
): string {
  if (value == null || !Number.isFinite(value)) {
    return '';
  }

  const floor = Math.max(0, decimals ?? 0);

  if (minorDigits <= 0) {
    const significant = fractionDigitsOf(value);
    const places = Math.max(floor, significant);
    return value.toFixed(places);
  }

  const negative = value < 0;
  const digits = Math.round(Math.abs(value)).toString().padStart(minorDigits + 1, '0');
  const whole = digits.slice(0, digits.length - minorDigits);
  let fraction = digits.slice(digits.length - minorDigits);

  // Trim to the floor, keeping any digit that actually carries information.
  while (fraction.length > floor && fraction.endsWith('0')) {
    fraction = fraction.slice(0, -1);
  }

  const body = fraction.length > 0 ? `${whole}.${fraction}` : whole;
  return negative ? `-${body}` : body;
}

/** How many decimal places a JavaScript number really carries, up to twelve. */
function fractionDigitsOf(value: number): number {
  const text = Math.abs(value).toString();
  if (text.includes('e') || text.includes('E')) {
    // Exponential notation means either a very large integer or a value far
    // below any precision this product uses; twelve places is past both.
    return 12;
  }
  const point = text.indexOf('.');
  return point < 0 ? 0 : Math.min(12, text.length - point - 1);
}

/**
 * The smallest step the field can move by, **in the units it displays**.
 *
 * The native `step` attribute is compared against the element's own value,
 * which is the decimal a person reads — so this follows the displayed precision
 * and not the stored scale. A money field showing two places steps by 0.01
 * whether its stored value is rupees or paise.
 */
export function stepFor(decimals: number): number {
  return decimals <= 0 ? 1 : Number(`1e-${decimals}`);
}
