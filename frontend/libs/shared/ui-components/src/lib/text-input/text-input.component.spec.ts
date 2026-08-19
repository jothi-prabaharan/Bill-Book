import { TestBed } from '@angular/core/testing';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { TextInputComponent } from './text-input.component';

interface TextInputTestHarness {
  innerValue: () => string;
  effectiveDisabled: () => boolean;
  uppercase: () => boolean;
  onInput: (event: Event) => void;
  onKeyDown: (event: KeyboardEvent) => void;
  onBlur: (event: FocusEvent) => void;
  onFocus: (event: FocusEvent) => void;
}

describe('TextInputComponent', () => {
  let cva: TextInputComponent;
  let harness: TextInputTestHarness;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    cva = TestBed.runInInjectionContext(() => new TextInputComponent());
    harness = cva as unknown as TextInputTestHarness;
  });

  describe('Tier 1: Feature / Contract Coverage', () => {
    it('TXT-T1-01: writeValue updates text signal and innerValue', () => {
      cva.writeValue('Acme Corp');
      expect(harness.innerValue()).toBe('Acme Corp');
    });

    it('TXT-T1-02: onInput invokes registered onChange callback and emits valueChange', () => {
      const changeSpy = vi.fn();
      const valueChangeSpy = vi.fn();

      cva.registerOnChange(changeSpy);
      cva.valueChange.subscribe(valueChangeSpy);

      const mockEvent = {
        target: { value: 'New String' },
      } as unknown as Event;

      harness.onInput(mockEvent);

      expect(changeSpy).toHaveBeenCalledWith('New String');
      expect(valueChangeSpy).toHaveBeenCalledWith('New String');
      expect(harness.innerValue()).toBe('New String');
    });

    it('TXT-T1-03: onBlur invokes registered onTouched callback and emits blur event', () => {
      const touchSpy = vi.fn();
      const blurSpy = vi.fn();

      cva.registerOnTouched(touchSpy);
      cva.blur.subscribe(blurSpy);

      const blurEvent = new FocusEvent('blur');
      harness.onBlur(blurEvent);

      expect(touchSpy).toHaveBeenCalledTimes(1);
      expect(blurSpy).toHaveBeenCalledWith(blurEvent);
    });

    it('TXT-T1-04: setDisabledState updates internal cvaDisabled and effectiveDisabled signal', () => {
      expect(harness.effectiveDisabled()).toBe(false);

      cva.setDisabledState(true);
      expect(harness.effectiveDisabled()).toBe(true);

      cva.setDisabledState(false);
      expect(harness.effectiveDisabled()).toBe(false);
    });

    it('TXT-T1-05: default input signal attributes are correctly initialized', () => {
      expect(cva.id()).toBe('');
      expect(cva.name()).toBe('');
      expect(cva.type()).toBe('text');
      expect(cva.placeholder()).toBe('');
      expect(cva.maxlength()).toBeNull();
      expect(cva.uppercase()).toBe(false);
      expect(cva.disabled()).toBe(false);
      expect(cva.readonly()).toBe(false);
      expect(cva.required()).toBe(false);
      expect(cva.autocomplete()).toBe('off');
      expect(cva.ariaLabel()).toBe('');
    });

    it('TXT-T1-06: onKeyDown Enter key emits enter output with current value', () => {
      cva.writeValue('Submit text');

      const enterSpy = vi.fn();
      cva.enter.subscribe(enterSpy);

      const enterEvent = {
        key: 'Enter',
      } as unknown as KeyboardEvent;

      harness.onKeyDown(enterEvent);

      expect(enterSpy).toHaveBeenCalledWith('Submit text');
    });
  });

  describe('Tier 2: Boundary & Corner Cases', () => {
    it('TXT-T2-01: writeValue normalizes null and undefined to empty string', () => {
      cva.writeValue('existing');
      expect(harness.innerValue()).toBe('existing');

      cva.writeValue(null);
      expect(harness.innerValue()).toBe('');

      cva.writeValue(undefined);
      expect(harness.innerValue()).toBe('');
    });

    it('TXT-T2-02: uppercase transform converts lowercase input to uppercase on write and onInput', () => {
      const upperCva = TestBed.runInInjectionContext(() => {
        const comp = new TextInputComponent();
        const h = comp as unknown as TextInputTestHarness;
        h.uppercase = () => true;
        return comp;
      });
      const upperHarness = upperCva as unknown as TextInputTestHarness;

      upperCva.writeValue('29aaaaa0000a1z5');
      expect(upperHarness.innerValue()).toBe('29AAAAA0000A1Z5');

      const changeSpy = vi.fn();
      upperCva.registerOnChange(changeSpy);

      const target = { value: 'abcde1234f' };
      upperHarness.onInput({ target } as unknown as Event);

      expect(target.value).toBe('ABCDE1234F');
      expect(changeSpy).toHaveBeenCalledWith('ABCDE1234F');
      expect(upperHarness.innerValue()).toBe('ABCDE1234F');
    });

    it('TXT-T2-03: maxlength signal defaults to null', () => {
      expect(cva.maxlength()).toBeNull();
    });

    it('TXT-T2-04: readonly signal defaults to false', () => {
      expect(cva.readonly()).toBe(false);
    });

    it('TXT-T2-05: handles Unicode, emoji, and special characters properly', () => {
      const unicodeString = '🏢 Head Office — #01-A';
      cva.writeValue(unicodeString);
      expect(harness.innerValue()).toBe(unicodeString);
    });

    it('TXT-T2-06: onFocus dispatches focus output event', () => {
      const focusSpy = vi.fn();
      cva.focus.subscribe(focusSpy);

      const focusEvent = new FocusEvent('focus');
      harness.onFocus(focusEvent);

      expect(focusSpy).toHaveBeenCalledWith(focusEvent);
    });
  });

  describe('Tier 3: Cross-Feature Interactions', () => {
    it('TXT-T3-01: uppercase mode transforms dynamic typing and preserves casing consistency', () => {
      const upperCva = TestBed.runInInjectionContext(() => {
        const comp = new TextInputComponent();
        const h = comp as unknown as TextInputTestHarness;
        h.uppercase = () => true;
        return comp;
      });
      const upperHarness = upperCva as unknown as TextInputTestHarness;

      const changeSpy = vi.fn();
      upperCva.registerOnChange(changeSpy);

      upperHarness.onInput({ target: { value: 'gstin123' } } as unknown as Event);
      expect(changeSpy).toHaveBeenCalledWith('GSTIN123');

      upperCva.writeValue('pan456');
      expect(upperHarness.innerValue()).toBe('PAN456');
    });

    it('TXT-T3-02: dynamic disabled state toggles preserve string value', () => {
      cva.writeValue('Stable Value');
      expect(harness.innerValue()).toBe('Stable Value');

      cva.setDisabledState(true);
      expect(harness.effectiveDisabled()).toBe(true);
      expect(harness.innerValue()).toBe('Stable Value');

      cva.setDisabledState(false);
      expect(harness.effectiveDisabled()).toBe(false);
      expect(harness.innerValue()).toBe('Stable Value');
    });
  });

  describe('Tier 4: Real-World Application Scenarios', () => {
    it('TXT-T4-01: GSTIN / PAN validation in Reactive FormGroup with uppercase transformation', () => {
      const gstinRegex = /^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$/;
      const form = new FormGroup({
        gstin: new FormControl<string>('', [Validators.required, Validators.pattern(gstinRegex)]),
      });

      const upperCva = TestBed.runInInjectionContext(() => {
        const comp = new TextInputComponent();
        const h = comp as unknown as TextInputTestHarness;
        h.uppercase = () => true;
        return comp;
      });
      const upperHarness = upperCva as unknown as TextInputTestHarness;

      const control = form.get('gstin')!;
      upperCva.registerOnChange((val) => control.setValue(val));

      // Lowercase input is auto-converted to uppercase matching the regex pattern
      upperHarness.onInput({ target: { value: '29abcde1234f1z5' } } as unknown as Event);

      expect(control.value).toBe('29ABCDE1234F1Z5');
      expect(control.valid).toBe(true);
    });

    it('TXT-T4-02: Form validation lifecycle with touched, dirty, required, and reset', () => {
      const form = new FormGroup({
        name: new FormControl<string>('', [Validators.required]),
      });

      const control = form.get('name')!;
      cva.registerOnChange((val) => control.setValue(val));
      cva.registerOnTouched(() => control.markAsTouched());

      expect(control.valid).toBe(false);
      expect(control.touched).toBe(false);

      // Blur without typing -> touched and invalid
      harness.onBlur(new FocusEvent('blur'));
      expect(control.touched).toBe(true);
      expect(control.valid).toBe(false);

      // User types value -> valid
      harness.onInput({ target: { value: 'Main Office' } } as unknown as Event);
      expect(control.value).toBe('Main Office');
      expect(control.valid).toBe(true);

      // Reset
      form.reset({ name: '' });
      cva.writeValue(control.value);
      expect(harness.innerValue()).toBe('');
      expect(control.valid).toBe(false);
      expect(control.touched).toBe(false);
    });
  });
});
