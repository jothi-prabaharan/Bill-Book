import { TestBed } from '@angular/core/testing';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { CurrencyInputComponent } from './currency-input.component';

interface CurrencyInputTestHarness {
  displayValue: { (): string; set: (val: string) => void };
  isFocused: () => boolean;
  rawNumericValue: number | null;
  effectiveDisabled: () => boolean;
  inPaise: () => boolean;
  allowNegative: () => boolean;
  decimals: () => number;
  onInput: (event: Event) => void;
  onBlur: (event: FocusEvent) => void;
  onFocus: (event: FocusEvent) => void;
}

describe('CurrencyInputComponent', () => {
  let cva: CurrencyInputComponent;
  let harness: CurrencyInputTestHarness;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    cva = TestBed.runInInjectionContext(() => new CurrencyInputComponent());
    harness = cva as unknown as CurrencyInputTestHarness;
  });

  describe('Tier 1: Feature / Contract Coverage', () => {
    it('CURR-T1-01: writeValue with numeric amount updates internal state and displays formatted value', () => {
      cva.writeValue(1234.5);
      expect(harness.displayValue()).toBe('1234.50');
      expect(harness.rawNumericValue).toBe(1234.5);
    });

    it('CURR-T1-02: onInput invokes registered onChange callback and emits valueChange with numeric value', () => {
      const changeSpy = vi.fn();
      const valueChangeSpy = vi.fn();

      cva.registerOnChange(changeSpy);
      cva.valueChange.subscribe(valueChangeSpy);

      const mockEvent = {
        target: { value: '500.75' },
      } as unknown as Event;

      harness.onInput(mockEvent);

      expect(changeSpy).toHaveBeenCalledWith(500.75);
      expect(valueChangeSpy).toHaveBeenCalledWith(500.75);
      expect(harness.rawNumericValue).toBe(500.75);
    });

    it('CURR-T1-03: onBlur invokes registered onTouched callback and reformats display', () => {
      const touchSpy = vi.fn();
      const blurSpy = vi.fn();

      cva.registerOnTouched(touchSpy);
      cva.blur.subscribe(blurSpy);

      cva.writeValue(50);
      harness.displayValue.set('50');

      const blurEvent = new FocusEvent('blur');
      harness.onBlur(blurEvent);

      expect(touchSpy).toHaveBeenCalledTimes(1);
      expect(blurSpy).toHaveBeenCalledWith(blurEvent);
      expect(harness.displayValue()).toBe('50.00');
    });

    it('CURR-T1-04: setDisabledState updates internal cvaDisabled and effectiveDisabled signal', () => {
      expect(harness.effectiveDisabled()).toBe(false);

      cva.setDisabledState(true);
      expect(harness.effectiveDisabled()).toBe(true);

      cva.setDisabledState(false);
      expect(harness.effectiveDisabled()).toBe(false);
    });

    it('CURR-T1-05: default input signal attributes are correctly initialized', () => {
      expect(cva.id()).toBe('');
      expect(cva.name()).toBe('');
      expect(cva.symbol()).toBe('₹');
      expect(cva.currencyCode()).toBe('INR');
      expect(cva.showSymbol()).toBe(false);
      expect(cva.decimals()).toBe(2);
      expect(cva.min()).toBeNull();
      expect(cva.max()).toBeNull();
      expect(cva.step()).toBe(0.01);
      expect(cva.placeholder()).toBe('0.00');
      expect(cva.disabled()).toBe(false);
      expect(cva.readonly()).toBe(false);
      expect(cva.required()).toBe(false);
      expect(cva.allowNegative()).toBe(false);
      expect(cva.inPaise()).toBe(false);
      expect(cva.align()).toBe('right');
      expect(cva.ariaLabel()).toBe('Amount');
    });

    it('CURR-T1-06: inPaise mode converts integer paise to decimal rupees on write and decimal to paise on input', () => {
      const paiseCva = TestBed.runInInjectionContext(() => {
        const comp = new CurrencyInputComponent();
        const h = comp as unknown as CurrencyInputTestHarness;
        h.inPaise = () => true;
        return comp;
      });
      const paiseHarness = paiseCva as unknown as CurrencyInputTestHarness;

      paiseCva.writeValue(25050); // 250.50 rupees
      expect(paiseHarness.displayValue()).toBe('250.50');

      const changeSpy = vi.fn();
      const valueChangeSpy = vi.fn();
      paiseCva.registerOnChange(changeSpy);
      paiseCva.valueChange.subscribe(valueChangeSpy);

      paiseHarness.onInput({ target: { value: '100.00' } } as unknown as Event);
      expect(changeSpy).toHaveBeenCalledWith(10000);
      expect(valueChangeSpy).toHaveBeenCalledWith(10000);
    });
  });

  describe('Tier 2: Boundary & Corner Cases', () => {
    it('CURR-T2-01: writeValue handles null, undefined, empty string, and zero (0)', () => {
      cva.writeValue(null);
      expect(harness.displayValue()).toBe('');
      expect(harness.rawNumericValue).toBeNull();

      cva.writeValue(undefined);
      expect(harness.displayValue()).toBe('');
      expect(harness.rawNumericValue).toBeNull();

      cva.writeValue('');
      expect(harness.displayValue()).toBe('');
      expect(harness.rawNumericValue).toBeNull();

      cva.writeValue(0);
      expect(harness.displayValue()).toBe('0.00');
      expect(harness.rawNumericValue).toBe(0);
    });

    it('CURR-T2-02: negative values stripped when allowNegative is false', () => {
      const changeSpy = vi.fn();
      cva.registerOnChange(changeSpy);

      const mockEvent = {
        target: { value: '-500.00' },
      } as unknown as Event;

      harness.onInput(mockEvent);

      expect(changeSpy).toHaveBeenCalledWith(500);
      expect(harness.rawNumericValue).toBe(500);
    });

    it('CURR-T2-03: negative values accepted when allowNegative is true', () => {
      const negCva = TestBed.runInInjectionContext(() => {
        const comp = new CurrencyInputComponent();
        const h = comp as unknown as CurrencyInputTestHarness;
        h.allowNegative = () => true;
        return comp;
      });
      const negHarness = negCva as unknown as CurrencyInputTestHarness;

      const changeSpy = vi.fn();
      negCva.registerOnChange(changeSpy);

      negHarness.onInput({ target: { value: '-150.25' } } as unknown as Event);

      expect(changeSpy).toHaveBeenCalledWith(-150.25);
      expect(negHarness.rawNumericValue).toBe(-150.25);
    });

    it('CURR-T2-04: decimal precision formatting on blur with custom decimals', () => {
      const decCva = TestBed.runInInjectionContext(() => {
        const comp = new CurrencyInputComponent();
        const h = comp as unknown as CurrencyInputTestHarness;
        h.decimals = () => 4;
        return comp;
      });
      const decHarness = decCva as unknown as CurrencyInputTestHarness;

      decCva.writeValue(12.34567);
      expect(decHarness.displayValue()).toBe('12.3457');

      decHarness.onBlur(new FocusEvent('blur'));
      expect(decHarness.displayValue()).toBe('12.3457');
    });

    it('CURR-T2-05: non-numeric malformed input string handled gracefully', () => {
      const changeSpy = vi.fn();
      cva.registerOnChange(changeSpy);

      harness.onInput({ target: { value: 'invalid_number' } } as unknown as Event);

      expect(changeSpy).toHaveBeenCalledWith(null);
      expect(harness.rawNumericValue).toBeNull();
    });

    it('CURR-T2-06: high magnitude amount precision handling (Crores)', () => {
      const largeAmount = 999999999.99;
      cva.writeValue(largeAmount);
      expect(harness.rawNumericValue).toBe(largeAmount);
      expect(harness.displayValue()).toBe('999999999.99');
    });
  });

  describe('Tier 3: Cross-Feature Interactions', () => {
    it('CURR-T3-01: focus and blur transitions update isFocused state without losing numeric value', () => {
      cva.writeValue(12345.67);
      expect(harness.isFocused()).toBe(false);

      const focusEvent = new FocusEvent('focus');
      const focusSpy = vi.fn();
      cva.focus.subscribe(focusSpy);
      harness.onFocus(focusEvent);

      expect(harness.isFocused()).toBe(true);
      expect(focusSpy).toHaveBeenCalledWith(focusEvent);

      const blurEvent = new FocusEvent('blur');
      harness.onBlur(blurEvent);
      expect(harness.isFocused()).toBe(false);
      expect(harness.displayValue()).toBe('12345.67');
    });

    it('CURR-T3-02: dynamic toggle and reformatting across writeValue calls', () => {
      cva.writeValue(500);
      expect(harness.displayValue()).toBe('500.00');

      cva.writeValue(null);
      expect(harness.displayValue()).toBe('');

      cva.writeValue(75.5);
      expect(harness.displayValue()).toBe('75.50');
    });
  });

  describe('Tier 4: Real-World Application Scenarios', () => {
    it('CURR-T4-01: Invoice Line item total calculation with Reactive FormGroup', () => {
      const lineForm = new FormGroup({
        unitPrice: new FormControl<number | null>(1000),
        discount: new FormControl<number | null>(100),
        lineTotal: new FormControl<number | null>(900),
      });

      const priceCva = TestBed.runInInjectionContext(() => new CurrencyInputComponent());
      const discountCva = TestBed.runInInjectionContext(() => new CurrencyInputComponent());
      const priceHarness = priceCva as unknown as CurrencyInputTestHarness;
      const discountHarness = discountCva as unknown as CurrencyInputTestHarness;

      priceCva.registerOnChange((val) => {
        lineForm.get('unitPrice')?.setValue(val);
        const total = (val ?? 0) - (lineForm.get('discount')?.value ?? 0);
        lineForm.get('lineTotal')?.setValue(total);
      });

      discountCva.registerOnChange((val) => {
        lineForm.get('discount')?.setValue(val);
        const total = (lineForm.get('unitPrice')?.value ?? 0) - (val ?? 0);
        lineForm.get('lineTotal')?.setValue(total);
      });

      priceHarness.onInput({ target: { value: '2000' } } as unknown as Event);
      expect(lineForm.get('lineTotal')?.value).toBe(1900);

      discountHarness.onInput({ target: { value: '300' } } as unknown as Event);
      expect(lineForm.get('lineTotal')?.value).toBe(1700);
    });

    it('CURR-T4-02: Form validation with Validators.min(100), required, and form reset', () => {
      const form = new FormGroup({
        amount: new FormControl<number | null>(null, [Validators.required, Validators.min(100)]),
      });

      const control = form.get('amount')!;
      cva.registerOnChange((val) => control.setValue(val));
      cva.registerOnTouched(() => control.markAsTouched());

      expect(control.valid).toBe(false);

      // Value below min
      harness.onInput({ target: { value: '50' } } as unknown as Event);
      expect(control.value).toBe(50);
      expect(control.hasError('min')).toBe(true);

      // Value above min
      harness.onInput({ target: { value: '150' } } as unknown as Event);
      expect(control.value).toBe(150);
      expect(control.valid).toBe(true);

      // Touch
      harness.onBlur(new FocusEvent('blur'));
      expect(control.touched).toBe(true);

      // Form reset
      form.reset();
      cva.writeValue(control.value);
      expect(harness.displayValue()).toBe('');
      expect(control.valid).toBe(false);
      expect(control.touched).toBe(false);
    });
  });
});
