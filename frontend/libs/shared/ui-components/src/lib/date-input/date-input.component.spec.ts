import { TestBed } from '@angular/core/testing';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { DateInputComponent } from './date-input.component';

interface DateInputTestHarness {
  innerValue: () => string;
  effectiveDisabled: () => boolean;
  onInput: (event: Event) => void;
  onBlur: (event: FocusEvent) => void;
  onFocus: (event: FocusEvent) => void;
}

describe('DateInputComponent', () => {
  let cva: DateInputComponent;
  let harness: DateInputTestHarness;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    cva = TestBed.runInInjectionContext(() => new DateInputComponent());
    harness = cva as unknown as DateInputTestHarness;
  });

  describe('Tier 1: Feature / Contract Coverage', () => {
    it('DATE-T1-01: writeValue with valid ISO date string updates internal signal', () => {
      cva.writeValue('2026-08-18');
      expect(harness.innerValue()).toBe('2026-08-18');
    });

    it('DATE-T1-02: onInput invokes registered onChange callback and emits valueChange', () => {
      const changeSpy = vi.fn();
      const valueChangeSpy = vi.fn();

      cva.registerOnChange(changeSpy);
      cva.valueChange.subscribe(valueChangeSpy);

      const mockEvent = {
        target: { value: '2026-12-31' },
      } as unknown as Event;

      harness.onInput(mockEvent);

      expect(changeSpy).toHaveBeenCalledWith('2026-12-31');
      expect(valueChangeSpy).toHaveBeenCalledWith('2026-12-31');
      expect(harness.innerValue()).toBe('2026-12-31');
    });

    it('DATE-T1-03: onBlur invokes registered onTouched callback and emits blur event', () => {
      const touchSpy = vi.fn();
      const blurSpy = vi.fn();

      cva.registerOnTouched(touchSpy);
      cva.blur.subscribe(blurSpy);

      const focusEvent = new FocusEvent('blur');
      harness.onBlur(focusEvent);

      expect(touchSpy).toHaveBeenCalledTimes(1);
      expect(blurSpy).toHaveBeenCalledWith(focusEvent);
    });

    it('DATE-T1-04: setDisabledState updates internal cvaDisabled and effectiveDisabled signal', () => {
      expect(harness.effectiveDisabled()).toBe(false);

      cva.setDisabledState(true);
      expect(harness.effectiveDisabled()).toBe(true);

      cva.setDisabledState(false);
      expect(harness.effectiveDisabled()).toBe(false);
    });

    it('DATE-T1-05: default input signal attributes are correctly initialized', () => {
      expect(cva.id()).toBe('');
      expect(cva.name()).toBe('');
      expect(cva.placeholder()).toBe('');
      expect(cva.min()).toBeNull();
      expect(cva.max()).toBeNull();
      expect(cva.disabled()).toBe(false);
      expect(cva.readonly()).toBe(false);
      expect(cva.required()).toBe(false);
      expect(cva.ariaLabel()).toBe('Date');
    });

    it('DATE-T1-06: onFocus dispatches focus output event', () => {
      const focusSpy = vi.fn();
      cva.focus.subscribe(focusSpy);

      const focusEvent = new FocusEvent('focus');
      harness.onFocus(focusEvent);

      expect(focusSpy).toHaveBeenCalledWith(focusEvent);
    });
  });

  describe('Tier 2: Boundary & Corner Cases', () => {
    it('DATE-T2-01: writeValue normalizes null, undefined, and empty string to empty string', () => {
      cva.writeValue('2026-08-18');
      expect(harness.innerValue()).toBe('2026-08-18');

      cva.writeValue(null);
      expect(harness.innerValue()).toBe('');

      cva.writeValue(undefined);
      expect(harness.innerValue()).toBe('');

      cva.writeValue('');
      expect(harness.innerValue()).toBe('');
    });

    it('DATE-T2-02: clearing date field emits null via onChange and valueChange', () => {
      const changeSpy = vi.fn();
      const valueChangeSpy = vi.fn();

      cva.registerOnChange(changeSpy);
      cva.valueChange.subscribe(valueChangeSpy);

      const mockEvent = {
        target: { value: '   ' },
      } as unknown as Event;

      harness.onInput(mockEvent);

      expect(changeSpy).toHaveBeenCalledWith(null);
      expect(valueChangeSpy).toHaveBeenCalledWith(null);
      expect(harness.innerValue()).toBe('   ');
    });

    it('DATE-T2-03: handles min and max boundary signals correctly', () => {
      expect(cva.min()).toBeNull();
      expect(cva.max()).toBeNull();
    });

    it('DATE-T2-04: handles leap year date and Date object instance correctly', () => {
      cva.writeValue('2028-02-29');
      expect(harness.innerValue()).toBe('2028-02-29');

      const dateObj = new Date(2028, 1, 29); // Feb 29, 2028
      cva.writeValue(dateObj);
      expect(harness.innerValue()).toBe('2028-02-29');
    });

    it('DATE-T2-05: extracts YYYY-MM-DD from ISO timestamp and handles invalid Date', () => {
      cva.writeValue('2026-08-18T15:30:00.000Z');
      expect(harness.innerValue()).toBe('2026-08-18');

      const invalidDate = new Date('invalid');
      cva.writeValue(invalidDate);
      expect(harness.innerValue()).toBe('');
    });
  });

  describe('Tier 3: Cross-Feature Interactions', () => {
    it('DATE-T3-01: dynamic disabled state toggle preserves current date and restores interactivity', () => {
      cva.writeValue('2026-08-18');
      expect(harness.innerValue()).toBe('2026-08-18');

      cva.setDisabledState(true);
      expect(harness.effectiveDisabled()).toBe(true);
      expect(harness.innerValue()).toBe('2026-08-18');

      cva.setDisabledState(false);
      expect(harness.effectiveDisabled()).toBe(false);
      expect(harness.innerValue()).toBe('2026-08-18');

      const changeSpy = vi.fn();
      cva.registerOnChange(changeSpy);
      harness.onInput({ target: { value: '2026-09-01' } } as unknown as Event);
      expect(changeSpy).toHaveBeenCalledWith('2026-09-01');
      expect(harness.innerValue()).toBe('2026-09-01');
    });

    it('DATE-T3-02: dynamic min/max date constraint adjustment for Date Range filtering', () => {
      const fromDate = TestBed.runInInjectionContext(() => new DateInputComponent());
      const toDate = TestBed.runInInjectionContext(() => new DateInputComponent());
      const fromHarness = fromDate as unknown as DateInputTestHarness;
      const toHarness = toDate as unknown as DateInputTestHarness;

      fromDate.writeValue('2026-04-01');
      toDate.writeValue('2026-04-30');

      expect(fromHarness.innerValue()).toBe('2026-04-01');
      expect(toHarness.innerValue()).toBe('2026-04-30');
    });
  });

  describe('Tier 4: Real-World Application Scenarios', () => {
    it('DATE-T4-01: Reactive Form integration with FormGroup, Validators.required, touch state, and form reset', () => {
      const form = new FormGroup({
        docDate: new FormControl('2026-08-18', { validators: [Validators.required] }),
      });

      const control = form.get('docDate')!;
      cva.writeValue(control.value);
      cva.registerOnChange((val) => control.setValue(val));
      cva.registerOnTouched(() => control.markAsTouched());

      expect(control.valid).toBe(true);
      expect(control.touched).toBe(false);
      expect(harness.innerValue()).toBe('2026-08-18');

      // User clears date input
      harness.onInput({ target: { value: '' } } as unknown as Event);
      expect(control.value).toBeNull();
      expect(control.valid).toBe(false);

      // User blurs
      harness.onBlur(new FocusEvent('blur'));
      expect(control.touched).toBe(true);

      // Form reset
      form.reset({ docDate: '2026-01-01' });
      cva.writeValue(control.value);
      expect(harness.innerValue()).toBe('2026-01-01');
      expect(control.valid).toBe(true);
      expect(control.touched).toBe(false);
    });

    it('DATE-T4-02: Multi-field date range entry in accounting report filter', () => {
      const fromComp = TestBed.runInInjectionContext(() => new DateInputComponent());
      const toComp = TestBed.runInInjectionContext(() => new DateInputComponent());
      const fromHarness = fromComp as unknown as DateInputTestHarness;
      const toHarness = toComp as unknown as DateInputTestHarness;

      const fromChangeSpy = vi.fn();
      const toChangeSpy = vi.fn();

      fromComp.registerOnChange(fromChangeSpy);
      toComp.registerOnChange(toChangeSpy);

      fromHarness.onInput({ target: { value: '2026-04-01' } } as unknown as Event);
      toHarness.onInput({ target: { value: '2027-03-31' } } as unknown as Event);

      expect(fromChangeSpy).toHaveBeenCalledWith('2026-04-01');
      expect(toChangeSpy).toHaveBeenCalledWith('2027-03-31');
    });
  });
});
