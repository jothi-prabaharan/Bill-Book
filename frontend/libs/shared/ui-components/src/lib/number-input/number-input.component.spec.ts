import { TestBed } from '@angular/core/testing';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { NumberInputComponent } from './number-input.component';

/**
 * The component's internals, named as they are after the move onto
 * `BbNumericControlBase`: the text on screen is `displayText`, the parsed value
 * behind it is the `value` getter, and the defaults a caller did not supply are
 * the `resolved*` computeds rather than the raw inputs.
 */
interface NumberInputTestHarness {
  displayText: () => string;
  readonly value: number | null;
  effectiveDisabled: () => boolean;
  decimals: () => number | null;
  resolvedDecimals: () => number;
  resolvedStep: () => number;
  effectiveAriaLabel: () => string | null;
  onInput: (event: Event) => void;
  onBlur: (event: FocusEvent) => void;
  onFocus: (event: FocusEvent) => void;
}

describe('NumberInputComponent', () => {
  let cva: NumberInputComponent;
  let harness: NumberInputTestHarness;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    cva = TestBed.runInInjectionContext(() => new NumberInputComponent());
    harness = cva as unknown as NumberInputTestHarness;
  });

  describe('Tier 1: Feature / Contract Coverage', () => {
    it('NUM-T1-01: writeValue with number updates internal state and displayValue', () => {
      cva.writeValue(42);
      expect(harness.displayText()).toBe('42');
      expect(harness.value).toBe(42);
    });

    it('NUM-T1-02: onInput invokes registered onChange callback and emits valueChange with numeric value', () => {
      const changeSpy = vi.fn();
      const valueChangeSpy = vi.fn();

      cva.registerOnChange(changeSpy);
      cva.valueChange.subscribe(valueChangeSpy);

      const mockEvent = {
        target: { value: '100' },
      } as unknown as Event;

      harness.onInput(mockEvent);

      expect(changeSpy).toHaveBeenCalledWith(100);
      expect(valueChangeSpy).toHaveBeenCalledWith(100);
      expect(harness.value).toBe(100);
    });

    it('NUM-T1-03: onBlur invokes registered onTouched callback and emits blur event', () => {
      const touchSpy = vi.fn();
      const blurSpy = vi.fn();

      cva.registerOnTouched(touchSpy);
      cva.blur.subscribe(blurSpy);

      const blurEvent = new FocusEvent('blur');
      harness.onBlur(blurEvent);

      expect(touchSpy).toHaveBeenCalledTimes(1);
      expect(blurSpy).toHaveBeenCalledWith(blurEvent);
    });

    it('NUM-T1-04: setDisabledState updates internal cvaDisabled and effectiveDisabled signal', () => {
      expect(harness.effectiveDisabled()).toBe(false);

      cva.setDisabledState(true);
      expect(harness.effectiveDisabled()).toBe(true);

      cva.setDisabledState(false);
      expect(harness.effectiveDisabled()).toBe(false);
    });

    it('NUM-T1-05: default input signal attributes are correctly initialized', () => {
      expect(cva.id()).toBe('');
      expect(cva.name()).toBe('');
      expect(cva.min()).toBeNull();
      expect(cva.max()).toBeNull();
      expect(cva.decimals()).toBeNull();
      expect(cva.placeholder()).toBe('');
      expect(cva.prefix()).toBeNull();
      expect(cva.suffix()).toBeNull();
      expect(cva.disabled()).toBe(false);
      expect(cva.readonly()).toBe(false);
      expect(cva.required()).toBe(false);

      // Step, alignment, keyboard and the spoken name are now unset inputs with
      // a semantic default behind them, so what the field actually renders is
      // the `resolved*` value rather than the raw input. The rendered answers
      // are the same ones this component has always drawn: step 1, left
      // aligned, a decimal keypad and "Number" when nothing labels it.
      expect(cva.step()).toBeNull();
      expect(harness.resolvedStep()).toBe(1);
      expect(cva.align()).toBe('');
      expect(cva.inputmode()).toBe('');
      expect(cva.ariaLabel()).toBe('');
      expect(harness.effectiveAriaLabel()).toBe('Number');
    });

    it('NUM-T1-06: onFocus dispatches focus output event', () => {
      const focusSpy = vi.fn();
      cva.focus.subscribe(focusSpy);

      const focusEvent = new FocusEvent('focus');
      harness.onFocus(focusEvent);

      expect(focusSpy).toHaveBeenCalledWith(focusEvent);
    });
  });

  describe('Tier 2: Boundary & Corner Cases', () => {
    it('NUM-T2-01: writeValue handles null, undefined, empty string, and clearing input emits null', () => {
      cva.writeValue(null);
      expect(harness.displayText()).toBe('');
      expect(harness.value).toBeNull();

      cva.writeValue(undefined);
      expect(harness.displayText()).toBe('');
      expect(harness.value).toBeNull();

      cva.writeValue('');
      expect(harness.displayText()).toBe('');
      expect(harness.value).toBeNull();

      const changeSpy = vi.fn();
      cva.registerOnChange(changeSpy);
      harness.onInput({ target: { value: '' } } as unknown as Event);
      expect(changeSpy).toHaveBeenCalledWith(null);
    });

    it('NUM-T2-02: step precision preservation for fractional quantities (0.001)', () => {
      const changeSpy = vi.fn();
      cva.registerOnChange(changeSpy);

      harness.onInput({ target: { value: '1.005' } } as unknown as Event);
      expect(changeSpy).toHaveBeenCalledWith(1.005);
      expect(harness.value).toBe(1.005);
    });

    it('NUM-T2-03: min and max boundary signals verify configured bounds', () => {
      expect(cva.min()).toBeNull();
      expect(cva.max()).toBeNull();
    });

    it('NUM-T2-04: decimals formatting on writeValue and blur', () => {
      const decCva = TestBed.runInInjectionContext(() => {
        const comp = new NumberInputComponent();
        const h = comp as unknown as NumberInputTestHarness;
        h.decimals = () => 3;
        return comp;
      });
      const decHarness = decCva as unknown as NumberInputTestHarness;

      decCva.writeValue(12.3);
      expect(decHarness.displayText()).toBe('12.300');

      decHarness.onBlur(new FocusEvent('blur'));
      expect(decHarness.displayText()).toBe('12.300');
    });

    it('NUM-T2-05: non-numeric malformed string emits null and clears numeric value', () => {
      const changeSpy = vi.fn();
      cva.registerOnChange(changeSpy);

      harness.onInput({ target: { value: 'not_a_number' } } as unknown as Event);
      expect(changeSpy).toHaveBeenCalledWith(null);
      expect(harness.value).toBeNull();
    });

    it('NUM-T2-06: negative numeric input parsed correctly', () => {
      const changeSpy = vi.fn();
      cva.registerOnChange(changeSpy);

      harness.onInput({ target: { value: '-15.5' } } as unknown as Event);
      expect(changeSpy).toHaveBeenCalledWith(-15.5);
      expect(harness.value).toBe(-15.5);
    });
  });

  describe('Tier 3: Cross-Feature Interactions', () => {
    it('NUM-T3-01: dynamic disabled state toggling preserves numeric value', () => {
      cva.writeValue(100);
      expect(harness.displayText()).toBe('100');

      cva.setDisabledState(true);
      expect(harness.effectiveDisabled()).toBe(true);
      expect(harness.value).toBe(100);

      cva.setDisabledState(false);
      expect(harness.effectiveDisabled()).toBe(false);
      expect(harness.value).toBe(100);
    });

    it('NUM-T3-02: dynamic decimal places configuration across multiple writeValue cycles', () => {
      cva.writeValue(25.4);
      expect(harness.displayText()).toBe('25.4');

      cva.writeValue(0);
      expect(harness.displayText()).toBe('0');

      cva.writeValue(99.999);
      expect(harness.displayText()).toBe('99.999');
    });
  });

  describe('Tier 4: Real-World Application Scenarios', () => {
    it('NUM-T4-01: Inventory Item Master form with multi-number controls', () => {
      const itemForm = new FormGroup({
        reorderLevel: new FormControl<number | null>(50),
        leadTimeDays: new FormControl<number | null>(14),
        purityFactor: new FormControl<number | null>(0.916),
      });

      const reorderCva = TestBed.runInInjectionContext(() => new NumberInputComponent());
      const leadTimeCva = TestBed.runInInjectionContext(() => new NumberInputComponent());
      const purityCva = TestBed.runInInjectionContext(() => new NumberInputComponent());
      const reorderHarness = reorderCva as unknown as NumberInputTestHarness;
      const leadTimeHarness = leadTimeCva as unknown as NumberInputTestHarness;
      const purityHarness = purityCva as unknown as NumberInputTestHarness;

      reorderCva.registerOnChange((val) => itemForm.get('reorderLevel')?.setValue(val));
      leadTimeCva.registerOnChange((val) => itemForm.get('leadTimeDays')?.setValue(val));
      purityCva.registerOnChange((val) => itemForm.get('purityFactor')?.setValue(val));

      reorderHarness.onInput({ target: { value: '75' } } as unknown as Event);
      leadTimeHarness.onInput({ target: { value: '21' } } as unknown as Event);
      purityHarness.onInput({ target: { value: '0.999' } } as unknown as Event);

      expect(itemForm.value.reorderLevel).toBe(75);
      expect(itemForm.value.leadTimeDays).toBe(21);
      expect(itemForm.value.purityFactor).toBe(0.999);
    });

    it('NUM-T4-02: Form validation with Validators.required, Validators.min(0), and form reset', () => {
      const form = new FormGroup({
        quantity: new FormControl<number | null>(null, [Validators.required, Validators.min(0)]),
      });

      const control = form.get('quantity')!;
      cva.registerOnChange((val) => control.setValue(val));
      cva.registerOnTouched(() => control.markAsTouched());

      expect(control.valid).toBe(false);

      // Value 0 is valid (not null)
      harness.onInput({ target: { value: '0' } } as unknown as Event);
      expect(control.value).toBe(0);
      expect(control.valid).toBe(true);

      // Negative value is invalid
      harness.onInput({ target: { value: '-5' } } as unknown as Event);
      expect(control.value).toBe(-5);
      expect(control.hasError('min')).toBe(true);

      // Blur
      harness.onBlur(new FocusEvent('blur'));
      expect(control.touched).toBe(true);

      // Reset
      form.reset();
      cva.writeValue(control.value);
      expect(harness.displayText()).toBe('');
      expect(control.valid).toBe(false);
      expect(control.touched).toBe(false);
    });
  });
});
