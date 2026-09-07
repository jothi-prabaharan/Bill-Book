import { TestBed } from '@angular/core/testing';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { DEFAULT_FORMAT_SETTINGS, FormatSettings } from '@bill-book/currency-format';
import { groupDecimalText } from '../decimal';
import { ExchangeRateInputComponent } from './exchange-rate-input.component';
import { MoneyInputComponent } from './money-input.component';
import { PercentageInputComponent } from './percentage-input.component';
import { QuantityInputComponent } from './quantity-input.component';
import { UnitPriceInputComponent } from './unit-price-input.component';

/**
 * The five semantic numeric controls.
 *
 * They share one implementation, so what is worth testing per component is not
 * the parsing — that is `decimal.spec.ts` — but the **defaults**: the
 * precision, the range and the affix each one brings, which is the whole reason
 * there are five of them rather than one `bb-number-input` used everywhere.
 */
interface NumericHarness {
  displayText: () => string;
  readonly value: number | null;
  resolvedDecimals: () => number;
  resolvedMin: () => number | null;
  resolvedMax: () => number | null;
  resolvedStep: () => number;
  resolvedPrefix: () => string;
  resolvedSuffix: () => string;
  fieldType: () => string;
  onFocus: (event: FocusEvent) => void;
  minAttr: () => string | null;
  maxAttr: () => string | null;
  inputMode: () => string;
  effectiveAriaLabel: () => string | null;
  effectiveDisabled: () => boolean;
  invalid: () => boolean;
  describedBy: () => string | null;
  onInput: (event: Event) => void;
  onBlur: (event: FocusEvent) => void;
}

/** Vitest cannot compile a `templateUrl`, so the class is driven directly. */
function build<T>(make: () => T): { component: T; harness: NumericHarness } {
  const component = TestBed.runInInjectionContext(make);
  return { component, harness: component as unknown as NumericHarness };
}

function type(harness: NumericHarness, text: string): void {
  harness.onInput({ target: { value: text } } as unknown as Event);
}

/** Replaces a signal input on an instance, which is how these specs configure. */
function set(component: unknown, name: string, value: unknown): void {
  (component as Record<string, unknown>)[name] = () => value;
}

describe('Numeric inputs', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({});
  });

  describe('MoneyInputComponent', () => {
    it('MON-01: two decimal places, matching every money column in the schema', () => {
      const { component, harness } = build(() => new MoneyInputComponent());
      expect(harness.resolvedDecimals()).toBe(2);

      // Grouped at rest, in the branch's own style — the shipped default here
      // is the rupee's lakh-crore mask.
      component.writeValue(1250.5);
      expect(harness.displayText()).toBe('1,250.50');
    });

    it('MON-02: a valid decimal reaches the form as typed', () => {
      const { component, harness } = build(() => new MoneyInputComponent());
      const changed = vi.fn();
      component.registerOnChange(changed);

      type(harness, '1250.50');
      expect(changed).toHaveBeenCalledWith(1250.5);
      expect(harness.value).toBe(1250.5);
    });

    it('MON-03: text that is not a number publishes null', () => {
      const { component, harness } = build(() => new MoneyInputComponent());
      const changed = vi.fn();
      component.registerOnChange(changed);

      type(harness, 'not a number');
      expect(changed).toHaveBeenCalledWith(null);
      expect(harness.value).toBeNull();
    });

    it('MON-04: zero is a value, not an empty field', () => {
      const { component, harness } = build(() => new MoneyInputComponent());
      const changed = vi.fn();
      component.registerOnChange(changed);

      type(harness, '0');
      expect(changed).toHaveBeenCalledWith(0);

      component.writeValue(0);
      expect(harness.displayText()).toBe('0.00');
    });

    it('MON-05: negative is refused by default and allowed when asked for', () => {
      // Almost every amount in the product is a magnitude whose direction is
      // carried elsewhere — a debit column, a document type.
      const plain = build(() => new MoneyInputComponent());
      expect(plain.harness.resolvedMin()).toBe(0);

      const signed = build(() => {
        const component = new MoneyInputComponent();
        set(component, 'allowNegative', true);
        return component;
      });
      expect(signed.harness.resolvedMin()).toBeNull();
    });

    it('MON-06: min and max are stated in stored units and converted for the attribute', () => {
      const { harness } = build(() => {
        const component = new MoneyInputComponent();
        set(component, 'minorDigits', 2);
        set(component, 'min', 100);
        set(component, 'max', 1_000_000);
        return component;
      });

      // 100 paise is one rupee; the browser compares against what is displayed.
      expect(harness.minAttr()).toBe('1');
      expect(harness.maxAttr()).toBe('10000');
    });

    it('MON-07: in paise, no floating-point corruption anywhere in the round trip', () => {
      const { component, harness } = build(() => {
        const made = new MoneyInputComponent();
        set(made, 'minorDigits', 2);
        return made;
      });
      const changed = vi.fn();
      component.registerOnChange(changed);

      for (const [text, paise] of [
        ['1.15', 115],
        ['0.29', 29],
        ['8.20', 820],
        ['10000000.50', 1_000_000_050],
      ] as const) {
        type(harness, text);
        expect(changed).toHaveBeenLastCalledWith(paise);
        expect(Number.isInteger(harness.value)).toBe(true);

        component.writeValue(paise);
        // Grouped at rest; the figure underneath is exact either way.
        expect(harness.displayText()).toBe(
          groupDecimalText(Number(text).toFixed(2), '##,##,##0.00'),
        );

        // And plain the moment the field is focused, so no separator moves
        // under the caret while somebody types.
        harness.onFocus(new FocusEvent('focus'));
        expect(harness.displayText()).toBe(Number(text).toFixed(2));
        harness.onBlur(new FocusEvent('blur'));
      }
    });

    it('MON-08: a currency symbol is shown only when one is asked for', () => {
      // A column of amounts under a currency heading wants none, and a grid
      // full of repeated symbols is noise.
      const bare = build(() => new MoneyInputComponent());
      expect(bare.harness.resolvedPrefix()).toBe('');

      const explicit = build(() => {
        const component = new MoneyInputComponent();
        set(component, 'currencySymbol', '₹');
        return component;
      });
      expect(explicit.harness.resolvedPrefix()).toBe('₹');

      // `showCurrency` takes the branch's own symbol rather than a hard-coded one.
      const branch = build(() => {
        const component = new MoneyInputComponent();
        set(component, 'showCurrency', true);
        return component;
      });
      expect(branch.harness.resolvedPrefix()).toBe(DEFAULT_FORMAT_SETTINGS.currencySymbol);
    });

    it('MON-10: the mask, the precision and the symbol side come from the org currency', () => {
      // "Organization currency, currency format": the grouping, the decimal
      // places and which side the symbol sits are the currency's, not this
      // component's. A dollar org groups in threes; a rupee org in lakhs.
      const western: FormatSettings = {
        ...DEFAULT_FORMAT_SETTINGS,
        currencyCode: 'USD',
        currencySymbol: '$',
        currencyMask: '###,###,##0.00',
      };

      const { component, harness } = build(() => {
        const made = new MoneyInputComponent();
        set(made, 'formats', western);
        set(made, 'showCurrency', true);
        return made;
      });

      component.writeValue(1234567.89);
      expect(harness.displayText()).toBe('1,234,567.89');
      expect(harness.resolvedPrefix()).toBe('$');

      const suffixed = build(() => {
        const made = new MoneyInputComponent();
        set(made, 'formats', { ...western, symbolPosition: 'Suffix' as const });
        set(made, 'showCurrency', true);
        return made;
      });
      expect(suffixed.harness.resolvedPrefix()).toBe('');
      expect(suffixed.harness.resolvedSuffix()).toBe('$');
    });

    it('MON-11: a currency with no decimal places is drawn with none', () => {
      const yen: FormatSettings = {
        ...DEFAULT_FORMAT_SETTINGS,
        currencyCode: 'JPY',
        currencyMask: '###,###,##0',
        currencyDecimals: 0,
      };

      const { component, harness } = build(() => {
        const made = new MoneyInputComponent();
        set(made, 'formats', yen);
        return made;
      });

      expect(harness.resolvedDecimals()).toBe(0);
      component.writeValue(1234567);
      expect(harness.displayText()).toBe('1,234,567');
    });

    it('MON-12: the masked field is a text input, because a number one blanks a grouped value', () => {
      const { harness } = build(() => new MoneyInputComponent());
      expect(harness.fieldType()).toBe('text');
      expect(harness.inputMode()).toBe('decimal');

      // Everything else keeps the native number input and its range attributes.
      const plain = build(() => new QuantityInputComponent());
      expect(plain.harness.fieldType()).toBe('number');
    });

    it('MON-13: a grouped figure pasted back in is parsed, not rejected', () => {
      const { component, harness } = build(() => new MoneyInputComponent());
      const changed = vi.fn();
      component.registerOnChange(changed);

      type(harness, '12,34,567.89');
      expect(changed).toHaveBeenCalledWith(1234567.89);
    });

    it('MON-09: binds into a reactive form and carries its validators', () => {
      const form = new FormGroup({
        amount: new FormControl<number | null>(null, [Validators.required, Validators.min(0)]),
      });
      const control = form.controls.amount;

      const { component, harness } = build(() => new MoneyInputComponent());
      component.registerOnChange((value) => control.setValue(value));
      component.registerOnTouched(() => control.markAsTouched());

      expect(control.valid).toBe(false);

      type(harness, '250.75');
      expect(control.value).toBe(250.75);
      expect(control.valid).toBe(true);

      harness.onBlur(new FocusEvent('blur'));
      expect(control.touched).toBe(true);
    });
  });

  describe('UnitPriceInputComponent', () => {
    it('UPR-01: shows two places but never truncates the six the column holds', () => {
      // `UnitPrice` is decimal(28,6) where every other money column is (28,2).
      const { component, harness } = build(() => new UnitPriceInputComponent());
      expect(harness.resolvedDecimals()).toBe(2);

      component.writeValue(1250.5);
      expect(harness.displayText()).toBe('1250.50');

      component.writeValue(0.004375);
      expect(harness.displayText()).toBe('0.004375');
    });

    it('UPR-02: six-place precision survives being typed', () => {
      const { component, harness } = build(() => new UnitPriceInputComponent());
      const changed = vi.fn();
      component.registerOnChange(changed);

      type(harness, '0.004375');
      expect(changed).toHaveBeenCalledWith(0.004375);
    });

    it('UPR-03: never negative — a negative rate is not a discount', () => {
      const { harness } = build(() => new UnitPriceInputComponent());
      expect(harness.resolvedMin()).toBe(0);
    });

    it('UPR-04: the unit of measure is a suffix, not part of the value', () => {
      const { harness } = build(() => {
        const component = new UnitPriceInputComponent();
        set(component, 'perUnit', 'kg');
        return component;
      });
      expect(harness.resolvedSuffix()).toBe('/kg');
    });

    it('UPR-05: min and max apply', () => {
      const { harness } = build(() => {
        const component = new UnitPriceInputComponent();
        set(component, 'min', 5);
        set(component, 'max', 500);
        return component;
      });
      expect(harness.resolvedMin()).toBe(5);
      expect(harness.resolvedMax()).toBe(500);
    });
  });

  describe('QuantityInputComponent', () => {
    it('QTY-01: fractional by default, because Quantity is decimal(18,6)', () => {
      const { component, harness } = build(() => new QuantityInputComponent());
      expect(harness.resolvedDecimals()).toBe(2);

      const changed = vi.fn();
      component.registerOnChange(changed);
      type(harness, '2.5');
      expect(changed).toHaveBeenCalledWith(2.5);
    });

    it('QTY-02: integer mode rounds the precision and the step to whole units', () => {
      const { component, harness } = build(() => {
        const made = new QuantityInputComponent();
        set(made, 'integerOnly', true);
        return made;
      });

      expect(harness.resolvedDecimals()).toBe(0);
      expect(harness.resolvedStep()).toBe(1);
      expect(harness.inputMode()).toBe('numeric');

      component.writeValue(7);
      expect(harness.displayText()).toBe('7');
    });

    it('QTY-03: a decimal keypad whenever fractions are allowed', () => {
      const { harness } = build(() => new QuantityInputComponent());
      expect(harness.inputMode()).toBe('decimal');
    });

    it('QTY-04: never negative, and zero is allowed unless the caller says not', () => {
      const allowing = build(() => new QuantityInputComponent());
      expect(allowing.harness.resolvedMin()).toBe(0);

      const refusing = build(() => {
        const component = new QuantityInputComponent();
        set(component, 'allowZero', false);
        set(component, 'minorDigits', 6);
        return component;
      });
      expect(refusing.harness.resolvedMin()).toBe(1);
    });

    it('QTY-05: min, max and step are the caller’s to set', () => {
      const { harness } = build(() => {
        const component = new QuantityInputComponent();
        set(component, 'min', 1);
        set(component, 'max', 100);
        set(component, 'step', 0.5);
        return component;
      });
      expect(harness.resolvedMin()).toBe(1);
      expect(harness.resolvedMax()).toBe(100);
      expect(harness.resolvedStep()).toBe(0.5);
    });

    it('QTY-06: at the line grid’s scale, one unit is a million', () => {
      const { component, harness } = build(() => {
        const made = new QuantityInputComponent();
        set(made, 'minorDigits', 6);
        return made;
      });
      const changed = vi.fn();
      component.registerOnChange(changed);

      type(harness, '1');
      expect(changed).toHaveBeenCalledWith(1_000_000);

      component.writeValue(2_500_000);
      expect(harness.displayText()).toBe('2.50');
    });
  });

  describe('PercentageInputComponent', () => {
    it('PCT-01: ten per cent is 10, never 0.1', () => {
      // The schema holds percents, `line-math.ts` divides by a hundred and
      // `GstCalculator` does the same. Nothing here converts.
      const { component, harness } = build(() => new PercentageInputComponent());
      const changed = vi.fn();
      component.registerOnChange(changed);

      type(harness, '10');
      expect(changed).toHaveBeenCalledWith(10);

      component.writeValue(18);
      expect(harness.displayText()).toBe('18.00');
    });

    it('PCT-02: zero and a hundred are both inside the range', () => {
      const { harness } = build(() => new PercentageInputComponent());
      expect(harness.resolvedMin()).toBe(0);
      expect(harness.resolvedMax()).toBe(100);
    });

    it('PCT-03: over the maximum still publishes, so the page can say why', () => {
      // Silently rewriting what somebody typed is worse than refusing it: they
      // would never learn the field had a limit.
      const { component, harness } = build(() => new PercentageInputComponent());
      const changed = vi.fn();
      component.registerOnChange(changed);

      type(harness, '150');
      expect(changed).toHaveBeenCalledWith(150);
      expect(harness.maxAttr()).toBe('100');
    });

    it('PCT-04: decimal percentages keep their places', () => {
      const { component, harness } = build(() => new PercentageInputComponent());
      const changed = vi.fn();
      component.registerOnChange(changed);

      type(harness, '2.5');
      expect(changed).toHaveBeenCalledWith(2.5);

      component.writeValue(12.5);
      expect(harness.displayText()).toBe('12.50');
    });

    it('PCT-05: the maximum scales with the stored units', () => {
      const { harness } = build(() => {
        const component = new PercentageInputComponent();
        set(component, 'minorDigits', 4);
        return component;
      });
      expect(harness.resolvedMax()).toBe(1_000_000);
    });

    it('PCT-06: the per-cent sign is a label, and can be turned off', () => {
      const shown = build(() => new PercentageInputComponent());
      expect(shown.harness.resolvedSuffix()).toBe('%');

      const hidden = build(() => {
        const component = new PercentageInputComponent();
        set(component, 'showSuffix', false);
        return component;
      });
      expect(hidden.harness.resolvedSuffix()).toBe('');
    });
  });

  describe('ExchangeRateInputComponent', () => {
    it('FX-01: eight decimal places, because ExchangeRate is decimal(18,8)', () => {
      const { component, harness } = build(() => new ExchangeRateInputComponent());
      expect(harness.resolvedDecimals()).toBe(8);

      component.writeValue(83.12345678);
      expect(harness.displayText()).toBe('83.12345678');
    });

    it('FX-02: high precision survives being typed, with no rounding', () => {
      const { component, harness } = build(() => new ExchangeRateInputComponent());
      const changed = vi.fn();
      component.registerOnChange(changed);

      type(harness, '0.01123456');
      expect(changed).toHaveBeenCalledWith(0.01123456);
    });

    it('FX-03: strictly positive — zero would make every converted amount zero', () => {
      const { harness } = build(() => new ExchangeRateInputComponent());
      expect(harness.resolvedMin()).toBe(1e-8);
    });

    it('FX-04: zero and a negative still publish, so the page can refuse them', () => {
      const { component, harness } = build(() => new ExchangeRateInputComponent());
      const changed = vi.fn();
      component.registerOnChange(changed);

      type(harness, '0');
      expect(changed).toHaveBeenLastCalledWith(0);

      type(harness, '-1.5');
      expect(changed).toHaveBeenLastCalledWith(-1.5);
    });

    it('FX-05: the currency pair reads as a direction, not a symbol', () => {
      const { harness } = build(() => {
        const component = new ExchangeRateInputComponent();
        set(component, 'fromCurrency', 'USD');
        set(component, 'toCurrency', 'INR');
        return component;
      });
      expect(harness.resolvedPrefix()).toBe('USD → INR');
    });

    it('FX-06: a caller may lower the minimum but not lose the precision', () => {
      const { component, harness } = build(() => {
        const made = new ExchangeRateInputComponent();
        set(made, 'min', 0.5);
        return made;
      });
      expect(harness.resolvedMin()).toBe(0.5);

      component.writeValue(1.23456789);
      expect(harness.displayText()).toBe('1.23456789');
    });
  });

  describe('shared behaviour', () => {
    it('NUMBASE-01: blur settles the text at the field’s precision', () => {
      const { component, harness } = build(() => new MoneyInputComponent());
      component.registerOnChange(() => undefined);

      type(harness, '1.5');
      // Not re-formatted mid-edit: doing so moves the caret behind a trailing
      // zero and the next digit lands in the wrong place.
      expect(harness.displayText()).toBe('1.5');

      harness.onBlur(new FocusEvent('blur'));
      expect(harness.displayText()).toBe('1.50');
    });

    it('NUMBASE-02: blur on an emptied field leaves it empty, not zero', () => {
      const { component, harness } = build(() => new MoneyInputComponent());
      component.writeValue(42);

      type(harness, '');
      harness.onBlur(new FocusEvent('blur'));
      expect(harness.displayText()).toBe('');
      expect(harness.value).toBeNull();
    });

    it('NUMBASE-03: an error puts the field in its invalid state and names the message', () => {
      const { harness } = build(() => {
        const component = new MoneyInputComponent();
        set(component, 'id', 'inv-amount');
        set(component, 'error', 'Give the invoice an amount.');
        return component;
      });

      expect(harness.invalid()).toBe(true);
      expect(harness.describedBy()).toBe('inv-amount-error');
    });

    it('NUMBASE-04: a hint describes the field when there is no error', () => {
      const { harness } = build(() => {
        const component = new ExchangeRateInputComponent();
        set(component, 'id', 'inv-rate');
        set(component, 'hint', 'A snapshot at the document’s date.');
        return component;
      });

      expect(harness.invalid()).toBe(false);
      expect(harness.describedBy()).toBe('inv-rate-hint');
    });

    it('NUMBASE-05: a visible label means no aria-label competing with it', () => {
      const labelled = build(() => {
        const component = new MoneyInputComponent();
        set(component, 'label', 'Payment amount');
        return component;
      });
      expect(labelled.harness.effectiveAriaLabel()).toBeNull();

      const bare = build(() => new MoneyInputComponent());
      expect(bare.harness.effectiveAriaLabel()).toBe('Amount');
    });

    it('NUMBASE-06: disabled by the attribute or by the form, either counts', () => {
      const { component, harness } = build(() => new QuantityInputComponent());
      expect(harness.effectiveDisabled()).toBe(false);

      component.setDisabledState(true);
      expect(harness.effectiveDisabled()).toBe(true);

      component.setDisabledState(false);
      expect(harness.effectiveDisabled()).toBe(false);
    });

    it('NUMBASE-07: every control gets a distinct id without being given one', () => {
      const first = build(() => new MoneyInputComponent());
      const second = build(() => new MoneyInputComponent());

      expect(first.component.controlId()).not.toBe(second.component.controlId());
      expect(first.component.controlId()).toMatch(/^bb-money-\d+$/);
    });
  });
});
