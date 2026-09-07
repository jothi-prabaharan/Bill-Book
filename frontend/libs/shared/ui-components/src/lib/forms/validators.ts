import { AbstractControl, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { BB_EMAIL_PATTERN } from './text/email-input.component';
import { BB_LANDLINE_PATTERN, BB_MOBILE_MAX_LENGTH } from './text/phone-input.component';
import { BB_URL_PATTERN } from './text/url-input.component';
import { isRichTextEmpty } from './rich-text/sanitize-html';

/**
 * Validators that agree with what the common inputs draw.
 *
 * **The point is that there is one expression per rule, not two.** A field
 * showing `pattern="…"` and a `FormControl` validating something slightly
 * different is how a form goes green while the API refuses it — and the two
 * drift the moment one of them is edited. So every regular expression here is
 * imported from the component that renders it, rather than written again.
 *
 * **None of this is the security boundary.** The API validates the same rules
 * with the same expressions in `Shared.Kernel.Validation`, and that is the one
 * that counts; nothing here may be relaxed on the grounds that the server also
 * checks, and nothing there may be relaxed because the screen does.
 */

/** An email address, matching what `bb-email-input` renders and the API takes. */
export function bbEmail(): ValidatorFn {
  return patternOf(BB_EMAIL_PATTERN, 'bbEmail');
}

/**
 * A URL with a scheme.
 *
 * Deliberately stricter than the browser's `type="url"`, which accepts any
 * scheme at all — `javascript:` included. See `bb-url-input`.
 */
export function bbUrl(): ValidatorFn {
  return patternOf(BB_URL_PATTERN, 'bbUrl');
}

/**
 * A telephone number, under whichever rule applies.
 *
 * A mobile number carries **no pattern**, only a length — lengths vary too much
 * by country for an expression to be anything but a source of false rejections,
 * which is the rule `MobileAttribute` states and this mirrors.
 */
export function bbPhone(kind: 'mobile' | 'landline' = 'mobile'): ValidatorFn {
  if (kind === 'landline') {
    return patternOf(BB_LANDLINE_PATTERN, 'bbPhone');
  }

  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value;
    if (value == null || value === '') {
      return null;
    }
    return String(value).length <= BB_MOBILE_MAX_LENGTH
      ? null
      : { bbPhone: { maxLength: BB_MOBILE_MAX_LENGTH } };
  };
}

/**
 * A percentage between zero and a hundred.
 *
 * **In percent, not as a fraction** — ten per cent is `10`. `scale` is the
 * control's `minorDigits` where the value is a scaled integer, so the bounds
 * move with it rather than needing to be restated.
 */
export function bbPercentage(options: { min?: number; max?: number; scale?: number } = {}): ValidatorFn {
  const factor = 10 ** (options.scale ?? 0);
  const low = (options.min ?? 0) * factor;
  const high = (options.max ?? 100) * factor;

  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value;
    if (value == null || value === '') {
      return null;
    }
    const numeric = Number(value);
    if (!Number.isFinite(numeric)) {
      return { bbPercentage: { reason: 'notANumber' } };
    }
    if (numeric < low || numeric > high) {
      return { bbPercentage: { min: low, max: high, actual: numeric } };
    }
    return null;
  };
}

/**
 * An exchange rate: finite and strictly greater than zero.
 *
 * Zero would make every converted amount zero, which the check constraint
 * refuses on save — so it is refused here, where somebody can still see why.
 */
export function bbExchangeRate(options: { scale?: number } = {}): ValidatorFn {
  const smallest = options.scale && options.scale > 0 ? 1 : 1e-8;

  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value;
    if (value == null || value === '') {
      return null;
    }
    const numeric = Number(value);
    if (!Number.isFinite(numeric) || numeric < smallest) {
      return { bbExchangeRate: { min: smallest, actual: value } };
    }
    return null;
  };
}

/**
 * An amount with no more precision than the field stores.
 *
 * At `minorDigits` zero a money field holds two decimal places; a third would
 * be silently rounded by the API and the person keying would never learn which
 * way. Above zero the value is already an integer count of minor units, so the
 * check is that it is one.
 */
export function bbMoneyPrecision(options: { decimals?: number; scale?: number } = {}): ValidatorFn {
  const scale = options.scale ?? 0;
  const decimals = options.decimals ?? 2;

  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value;
    if (value == null || value === '') {
      return null;
    }
    const numeric = Number(value);
    if (!Number.isFinite(numeric)) {
      return { bbMoneyPrecision: { reason: 'notANumber' } };
    }

    if (scale > 0) {
      return Number.isInteger(numeric)
        ? null
        : { bbMoneyPrecision: { reason: 'notWholeMinorUnits' } };
    }

    const places = decimalPlacesOf(numeric);
    return places <= decimals ? null : { bbMoneyPrecision: { decimals, actual: places } };
  };
}

/**
 * A rich-text field that has to carry something.
 *
 * `Validators.required` is not enough: `<p><br></p>` is what an emptied editor
 * leaves behind, and it is a non-empty string.
 */
export function bbRichTextRequired(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null =>
    isRichTextEmpty(String(control.value ?? '')) ? { required: true } : null;
}

/** A file field that has to carry at least one file. */
export function bbFileRequired(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value;
    return Array.isArray(value) && value.length > 0 ? null : { required: true };
  };
}

function patternOf(pattern: string, key: string): ValidatorFn {
  // Anchored, because `Validators.pattern` anchors a string pattern and the
  // components render the same expression into an HTML `pattern`, which the
  // browser also anchors. Building it here keeps the two identical.
  const inner = Validators.pattern(`^${pattern}$`);

  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value;
    if (value == null || value === '') {
      // An empty optional field is valid. Requiredness is `Validators.required`,
      // stated separately, so a field can be optional and still well-formed.
      return null;
    }
    return inner(control) === null ? null : { [key]: true };
  };
}

/** How many decimal places a number actually carries. */
function decimalPlacesOf(value: number): number {
  const text = Math.abs(value).toString();
  if (text.includes('e') || text.includes('E')) {
    return 12;
  }
  const point = text.indexOf('.');
  return point < 0 ? 0 : text.length - point - 1;
}
