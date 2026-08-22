/**
 * An amount, spelled out the way an Indian invoice spells it.
 *
 * **Not `Intl.NumberFormat`, and not the western grouping.** Indian numbering
 * groups by lakh (10⁵) and crore (10⁷), so ₹1,234,567 is "twelve lakh thirty
 * four thousand five hundred sixty seven", not "one million two hundred…".
 * Getting this wrong on a printed invoice is the kind of error a customer
 * notices and an auditor asks about, because the figure and the words are meant
 * to corroborate each other — that is the whole reason the words are there.
 *
 * Kept in `currency-format` rather than beside one document's print layout,
 * because every printed document in the product needs it and nine copies is
 * eight that drift.
 */

const ONES: readonly string[] = [
  '',
  'one',
  'two',
  'three',
  'four',
  'five',
  'six',
  'seven',
  'eight',
  'nine',
  'ten',
  'eleven',
  'twelve',
  'thirteen',
  'fourteen',
  'fifteen',
  'sixteen',
  'seventeen',
  'eighteen',
  'nineteen',
];

const TENS: readonly string[] = [
  '',
  '',
  'twenty',
  'thirty',
  'forty',
  'fifty',
  'sixty',
  'seventy',
  'eighty',
  'ninety',
];

/** 0–99. The teens are irregular, which is why ONES runs to nineteen. */
function underHundred(n: number): string {
  if (n < 20) {
    return ONES[n];
  }

  const tens = TENS[Math.floor(n / 10)];
  const ones = ONES[n % 10];

  return ones ? `${tens} ${ones}` : tens;
}

/** 0–999. */
function underThousand(n: number): string {
  const hundreds = Math.floor(n / 100);
  const rest = n % 100;

  if (hundreds === 0) {
    return underHundred(rest);
  }

  const head = `${ONES[hundreds]} hundred`;
  return rest ? `${head} ${underHundred(rest)}` : head;
}

/**
 * A whole number in Indian words. Groups are crore, lakh, thousand, then the
 * last three digits — which is why this is not a loop over thousands.
 */
function whole(n: number): string {
  if (n === 0) {
    return 'zero';
  }

  const crore = Math.floor(n / 10_000_000);
  const lakh = Math.floor((n % 10_000_000) / 100_000);
  const thousand = Math.floor((n % 100_000) / 1000);
  const rest = n % 1000;

  const parts: string[] = [];

  if (crore) {
    parts.push(`${whole(crore)} crore`);
  }
  if (lakh) {
    parts.push(`${underThousand(lakh)} lakh`);
  }
  if (thousand) {
    parts.push(`${underThousand(thousand)} thousand`);
  }
  if (rest) {
    parts.push(underThousand(rest));
  }

  return parts.join(' ');
}

/** Capitalises the first letter and nothing else, as a printed invoice does. */
function sentence(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1);
}

/**
 * `1234.50, 'Rupees', 'Paise'` → `"Rupees one thousand two hundred thirty four and fifty paise only"`.
 *
 * **The fraction is rounded, not truncated**, and rounded once — the same
 * rounding the printed figure gets, so the words and the number cannot disagree
 * by a paisa. A negative amount is spelled with a leading "minus" rather than
 * being quietly made positive: a credit that printed as a debit in words is
 * worse than an ugly sentence.
 */
export function amountInWords(
  amount: number,
  unit = 'Rupees',
  fractionUnit = 'paise',
): string {
  if (!Number.isFinite(amount)) {
    return '';
  }

  const negative = amount < 0;

  // Rounded to the paisa first, so 1234.505 does not spell 1234 and 51.
  const totalPaise = Math.round(Math.abs(amount) * 100);
  const rupees = Math.floor(totalPaise / 100);
  const paise = totalPaise % 100;

  const head = `${unit} ${whole(rupees)}`;
  const tail = paise > 0 ? ` and ${underHundred(paise)} ${fractionUnit}` : '';
  const signed = negative ? `minus ${head}` : head;

  return sentence(`${signed}${tail} only`);
}
