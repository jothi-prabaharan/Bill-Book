import { TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { SearchInputComponent } from './search-input.component';

interface SearchInputTestHarness {
  innerValue: () => string;
  effectiveDisabled: () => boolean;
  onInput: (event: Event) => void;
  onKeyDown: (event: KeyboardEvent) => void;
  onClear: () => void;
  onBlur: () => void;
}

describe('SearchInputComponent', () => {
  let cva: SearchInputComponent;
  let harness: SearchInputTestHarness;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    cva = TestBed.runInInjectionContext(() => new SearchInputComponent());
    harness = cva as unknown as SearchInputTestHarness;
  });

  afterEach(() => {
    cva.ngOnDestroy();
    vi.useRealTimers();
  });

  describe('Tier 1: Feature / Contract Coverage', () => {
    it('SRCH-T1-01: writeValue updates search text signal', () => {
      cva.writeValue('invoice-101');
      expect(harness.innerValue()).toBe('invoice-101');
    });

    it('SRCH-T1-02: onInput invokes registered onChange callback and emits valueChange', () => {
      const changeSpy = vi.fn();
      const valueChangeSpy = vi.fn();

      cva.registerOnChange(changeSpy);
      cva.valueChange.subscribe(valueChangeSpy);

      const mockEvent = {
        target: { value: 'customer query' },
      } as unknown as Event;

      harness.onInput(mockEvent);

      expect(changeSpy).toHaveBeenCalledWith('customer query');
      expect(valueChangeSpy).toHaveBeenCalledWith('customer query');
      expect(harness.innerValue()).toBe('customer query');
    });

    it('SRCH-T1-03: onBlur invokes registered onTouched callback', () => {
      const touchSpy = vi.fn();
      cva.registerOnTouched(touchSpy);

      harness.onBlur();
      expect(touchSpy).toHaveBeenCalledTimes(1);
    });

    it('SRCH-T1-04: setDisabledState updates internal cvaDisabled and effectiveDisabled signal', () => {
      expect(harness.effectiveDisabled()).toBe(false);

      cva.setDisabledState(true);
      expect(harness.effectiveDisabled()).toBe(true);

      cva.setDisabledState(false);
      expect(harness.effectiveDisabled()).toBe(false);
    });

    it('SRCH-T1-05: default input signal attributes are correctly initialized', () => {
      expect(cva.id()).toBe('');
      expect(cva.name()).toBe('');
      expect(cva.placeholder()).toBe('Search...');
      expect(cva.ariaLabel()).toBe('Search');
      expect(cva.disabled()).toBe(false);
      expect(cva.debounceMs()).toBe(300);
    });

    it('SRCH-T1-06: onKeyDown Enter key emits search output immediately', () => {
      cva.writeValue('LEDGER-2026');

      const searchSpy = vi.fn();
      cva.search.subscribe(searchSpy);

      const enterEvent = {
        key: 'Enter',
        preventDefault: vi.fn(),
      } as unknown as KeyboardEvent;

      harness.onKeyDown(enterEvent);

      expect(enterEvent.preventDefault).toHaveBeenCalled();
      expect(searchSpy).toHaveBeenCalledWith('LEDGER-2026');
    });
  });

  describe('Tier 2: Boundary & Corner Cases', () => {
    it('SRCH-T2-01: writeValue normalizes null and undefined to empty string', () => {
      cva.writeValue('existing search');
      expect(harness.innerValue()).toBe('existing search');

      cva.writeValue(null);
      expect(harness.innerValue()).toBe('');

      cva.writeValue(undefined);
      expect(harness.innerValue()).toBe('');
    });

    it('SRCH-T2-02: onClear clears innerValue, invokes onChange, and emits clear and valueChange', () => {
      cva.writeValue('test term');

      const changeSpy = vi.fn();
      const valueChangeSpy = vi.fn();
      const clearSpy = vi.fn();

      cva.registerOnChange(changeSpy);
      cva.valueChange.subscribe(valueChangeSpy);
      cva.clear.subscribe(clearSpy);

      harness.onClear();

      expect(harness.innerValue()).toBe('');
      expect(changeSpy).toHaveBeenCalledWith('');
      expect(valueChangeSpy).toHaveBeenCalledWith('');
      expect(clearSpy).toHaveBeenCalledTimes(1);
    });

    it('SRCH-T2-03: Escape key in onKeyDown triggers onClear when text is present', () => {
      cva.writeValue('text to clear');

      const clearSpy = vi.fn();
      cva.clear.subscribe(clearSpy);

      const escapeEvent = {
        key: 'Escape',
        preventDefault: vi.fn(),
      } as unknown as KeyboardEvent;

      harness.onKeyDown(escapeEvent);

      expect(escapeEvent.preventDefault).toHaveBeenCalled();
      expect(clearSpy).toHaveBeenCalledTimes(1);
      expect(harness.innerValue()).toBe('');
    });

    it('SRCH-T2-04: handles special characters and symbols in query string', () => {
      const specialQuery = 'GST/2026-27/001 & #@!';
      cva.writeValue(specialQuery);
      expect(harness.innerValue()).toBe(specialQuery);
    });

    it('SRCH-T2-05: handles whitespace-only search queries gracefully', () => {
      const changeSpy = vi.fn();
      cva.registerOnChange(changeSpy);

      harness.onInput({ target: { value: '   ' } } as unknown as Event);

      expect(changeSpy).toHaveBeenCalledWith('   ');
      expect(harness.innerValue()).toBe('   ');
    });

    it('SRCH-T2-06: debounce timer triggers search output after delay', () => {
      vi.useFakeTimers();

      const searchSpy = vi.fn();
      cva.search.subscribe(searchSpy);

      harness.onInput({ target: { value: 'debounced search' } } as unknown as Event);

      expect(searchSpy).not.toHaveBeenCalled();

      vi.advanceTimersByTime(300);

      expect(searchSpy).toHaveBeenCalledWith('debounced search');
    });
  });

  describe('Tier 3: Cross-Feature Interactions', () => {
    it('SRCH-T3-01: sequential Type -> Clear -> Re-type interaction lifecycle', () => {
      const changeSpy = vi.fn();
      const clearSpy = vi.fn();
      cva.registerOnChange(changeSpy);
      cva.clear.subscribe(clearSpy);

      // 1. Type "first"
      harness.onInput({ target: { value: 'first' } } as unknown as Event);
      expect(harness.innerValue()).toBe('first');
      expect(changeSpy).toHaveBeenCalledWith('first');

      // 2. Clear
      harness.onClear();
      expect(harness.innerValue()).toBe('');
      expect(clearSpy).toHaveBeenCalledTimes(1);

      // 3. Re-type "second"
      harness.onInput({ target: { value: 'second' } } as unknown as Event);
      expect(harness.innerValue()).toBe('second');
      expect(changeSpy).toHaveBeenCalledWith('second');
    });

    it('SRCH-T3-02: onClear is a no-op when component is disabled', () => {
      cva.writeValue('active text');
      cva.setDisabledState(true);

      const clearSpy = vi.fn();
      cva.clear.subscribe(clearSpy);

      harness.onClear();

      expect(clearSpy).not.toHaveBeenCalled();
      expect(harness.innerValue()).toBe('active text');
    });
  });

  describe('Tier 4: Real-World Application Scenarios', () => {
    it('SRCH-T4-01: List filter query pipeline integration', () => {
      const items = ['Apple', 'Banana', 'Avocado', 'Cherry', 'Apricot'];
      let filteredItems = [...items];

      cva.registerOnChange((query) => {
        filteredItems = items.filter((item) =>
          item.toLowerCase().includes(query.toLowerCase())
        );
      });

      harness.onInput({ target: { value: 'ap' } } as unknown as Event);
      expect(filteredItems).toEqual(['Apple', 'Apricot']);

      harness.onClear();
      expect(filteredItems).toEqual(['Apple', 'Banana', 'Avocado', 'Cherry', 'Apricot']);
    });

    it('SRCH-T4-02: Reactive search control with form reset and ngOnDestroy cleanup', () => {
      const form = new FormGroup({
        filter: new FormControl<string>('initial query'),
      });

      const control = form.get('filter')!;
      cva.writeValue(control.value);
      cva.registerOnChange((val) => control.setValue(val));

      expect(harness.innerValue()).toBe('initial query');

      form.reset({ filter: '' });
      cva.writeValue(control.value);
      expect(harness.innerValue()).toBe('');

      expect(() => cva.ngOnDestroy()).not.toThrow();
    });
  });
});
