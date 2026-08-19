import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';
import { CurrencyInputComponent } from './currency-input/currency-input.component';
import { DateInputComponent } from './date-input/date-input.component';
import { NumberInputComponent } from './number-input/number-input.component';
import { SearchInputComponent } from './search-input/search-input.component';
import { TextInputComponent } from './text-input/text-input.component';

describe('Adversarial Empirical Stress Tests - M1 UI Primitives', () => {
  describe('1. CurrencyInputComponent Precision & Edge Cases', () => {
    it('CURR-STRESS-01: Float drift paise conversion for notorious IEEE-754 numbers', () => {
      const cases = [
        { inputRupees: '1.14', expectedPaise: 114 }, // 1.14 * 100 = 113.99999999999999
        { inputRupees: '1.29', expectedPaise: 129 }, // 1.29 * 100 = 128.99999999999997
        { inputRupees: '29.99', expectedPaise: 2999 }, // 29.99 * 100 = 2999.0000000000005
        { inputRupees: '57.01', expectedPaise: 5701 }, // 57.01 * 100 = 5701.000000000001
        { inputRupees: '0.01', expectedPaise: 1 },
        { inputRupees: '0.00', expectedPaise: 0 },
        { inputRupees: '10000000.50', expectedPaise: 1000000050 }, // 1 Crore + 50 paise
        { inputRupees: '99999999.99', expectedPaise: 9999999999 },
      ];

      for (const { inputRupees, expectedPaise } of cases) {
        const comp = TestBed.runInInjectionContext(() => {
          const c = new CurrencyInputComponent();
          (c as any).inPaise = () => true;
          return c;
        });

        const changeSpy = vi.fn();
        const valSpy = vi.fn();
        comp.registerOnChange(changeSpy);
        comp.valueChange.subscribe(valSpy);

        (comp as any).onInput({ target: { value: inputRupees } } as unknown as Event);

        expect(changeSpy).toHaveBeenCalledWith(expectedPaise);
        expect(valSpy).toHaveBeenCalledWith(expectedPaise);
        expect((comp as any).rawNumericValue).toBe(expectedPaise);

        // Test blur reformatting from paise
        (comp as any).onBlur(new FocusEvent('blur'));
        expect((comp as any).displayValue()).toBe(Number(inputRupees).toFixed(2));
      }
    });

    it('CURR-STRESS-02: writeValue paise round-trip with zero, fractions, and large values', () => {
      const comp = TestBed.runInInjectionContext(() => {
        const c = new CurrencyInputComponent();
        (c as any).inPaise = () => true;
        return c;
      });

      // Zero paise
      comp.writeValue(0);
      expect((comp as any).displayValue()).toBe('0.00');
      expect((comp as any).rawNumericValue).toBe(0);

      // 1 paise
      comp.writeValue(1);
      expect((comp as any).displayValue()).toBe('0.01');
      expect((comp as any).rawNumericValue).toBe(1);

      // 2999 paise (29.99)
      comp.writeValue(2999);
      expect((comp as any).displayValue()).toBe('29.99');
      expect((comp as any).rawNumericValue).toBe(2999);

      // 1000000050 paise (1 Crore 50 paise)
      comp.writeValue(1000000050);
      expect((comp as any).displayValue()).toBe('10000000.50');
      expect((comp as any).rawNumericValue).toBe(1000000050);
    });

    it('CURR-STRESS-03: allowNegative true vs false adversarial test', () => {
      // Disallow negative
      const compPosOnly = TestBed.runInInjectionContext(() => new CurrencyInputComponent());
      const changeSpy1 = vi.fn();
      compPosOnly.registerOnChange(changeSpy1);

      (compPosOnly as any).onInput({ target: { value: '-29.99' } } as unknown as Event);
      expect(changeSpy1).toHaveBeenCalledWith(29.99);

      // Allow negative
      const compAllowNeg = TestBed.runInInjectionContext(() => {
        const c = new CurrencyInputComponent();
        (c as any).allowNegative = () => true;
        return c;
      });
      const changeSpy2 = vi.fn();
      compAllowNeg.registerOnChange(changeSpy2);

      (compAllowNeg as any).onInput({ target: { value: '-29.99' } } as unknown as Event);
      expect(changeSpy2).toHaveBeenCalledWith(-29.99);

      // Just a minus sign while typing
      const changeSpy3 = vi.fn();
      compAllowNeg.registerOnChange(changeSpy3);
      (compAllowNeg as any).onInput({ target: { value: '-' } } as unknown as Event);
      expect(changeSpy3).toHaveBeenCalledWith(null);
      expect((compAllowNeg as any).rawNumericValue).toBeNull();
    });

    it('CURR-STRESS-04: Non-paise normal rupees mode zero vs null handling', () => {
      const comp = TestBed.runInInjectionContext(() => new CurrencyInputComponent());

      comp.writeValue(0);
      expect((comp as any).displayValue()).toBe('0.00');
      expect((comp as any).rawNumericValue).toBe(0);

      comp.writeValue('0');
      expect((comp as any).displayValue()).toBe('0.00');
      expect((comp as any).rawNumericValue).toBe(0);

      comp.writeValue(null);
      expect((comp as any).displayValue()).toBe('');
      expect((comp as any).rawNumericValue).toBeNull();

      comp.writeValue(undefined);
      expect((comp as any).displayValue()).toBe('');
      expect((comp as any).rawNumericValue).toBeNull();

      comp.writeValue('');
      expect((comp as any).displayValue()).toBe('');
      expect((comp as any).rawNumericValue).toBeNull();
    });
  });

  describe('2. NumberInputComponent Decimals, Micro-steps & Bounds', () => {
    it('NUM-STRESS-01: writeValue and onInput with zero (0) must never be treated as empty/null', () => {
      const comp = TestBed.runInInjectionContext(() => new NumberInputComponent());

      comp.writeValue(0);
      expect((comp as any).displayValue()).toBe('0');
      expect((comp as any).rawNumericValue).toBe(0);

      comp.writeValue('0');
      expect((comp as any).displayValue()).toBe('0');
      expect((comp as any).rawNumericValue).toBe(0);

      const changeSpy = vi.fn();
      comp.registerOnChange(changeSpy);

      (comp as any).onInput({ target: { value: '0' } } as unknown as Event);
      expect(changeSpy).toHaveBeenCalledWith(0);
      expect((comp as any).rawNumericValue).toBe(0);

      // Empty should emit null
      (comp as any).onInput({ target: { value: '' } } as unknown as Event);
      expect(changeSpy).toHaveBeenCalledWith(null);
      expect((comp as any).rawNumericValue).toBeNull();
    });

    it('NUM-STRESS-02: Micro-step precision (0.0001, 0.001) for gold weights and ratios', () => {
      const comp = TestBed.runInInjectionContext(() => {
        const c = new NumberInputComponent();
        (c as any).decimals = () => 4;
        (c as any).step = () => 0.0001;
        return c;
      });

      comp.writeValue(0.9165);
      expect((comp as any).displayValue()).toBe('0.9165');
      expect((comp as any).rawNumericValue).toBe(0.9165);

      const changeSpy = vi.fn();
      comp.registerOnChange(changeSpy);

      (comp as any).onInput({ target: { value: '0.9999' } } as unknown as Event);
      expect(changeSpy).toHaveBeenCalledWith(0.9999);
      expect((comp as any).rawNumericValue).toBe(0.9999);

      (comp as any).onBlur(new FocusEvent('blur'));
      expect((comp as any).displayValue()).toBe('0.9999');
    });

    it('NUM-STRESS-03: Malformed numeric typing rejections', () => {
      const comp = TestBed.runInInjectionContext(() => new NumberInputComponent());
      const changeSpy = vi.fn();
      comp.registerOnChange(changeSpy);

      // Invalid text
      (comp as any).onInput({ target: { value: 'abc' } } as unknown as Event);
      expect(changeSpy).toHaveBeenCalledWith(null);
      expect((comp as any).rawNumericValue).toBeNull();

      // Whitespace
      (comp as any).onInput({ target: { value: '   ' } } as unknown as Event);
      expect(changeSpy).toHaveBeenCalledWith(null);
      expect((comp as any).rawNumericValue).toBeNull();

      // Minus only
      (comp as any).onInput({ target: { value: '-' } } as unknown as Event);
      expect(changeSpy).toHaveBeenCalledWith(null);
      expect((comp as any).rawNumericValue).toBeNull();
    });
  });

  describe('3. DateInputComponent ISO Trimming & Date Parsing', () => {
    it('DATE-STRESS-01: ISO timestamps with T...Z or timezones properly trimmed to YYYY-MM-DD', () => {
      const comp = TestBed.runInInjectionContext(() => new DateInputComponent());

      comp.writeValue('2026-08-18T00:00:00Z');
      expect((comp as any).innerValue()).toBe('2026-08-18');

      comp.writeValue('2026-12-31T23:59:59.999+05:30');
      expect((comp as any).innerValue()).toBe('2026-12-31');

      comp.writeValue('2028-02-29'); // Leap year
      expect((comp as any).innerValue()).toBe('2028-02-29');
    });

    it('DATE-STRESS-02: Date object instances formatted to YYYY-MM-DD with zero padding', () => {
      const comp = TestBed.runInInjectionContext(() => new DateInputComponent());

      // May 4th, 2026 (single-digit month and day)
      const d = new Date(2026, 4, 4);
      comp.writeValue(d);
      expect((comp as any).innerValue()).toBe('2026-05-04');

      // Invalid Date object
      const invalidDate = new Date('not-a-date');
      comp.writeValue(invalidDate);
      expect((comp as any).innerValue()).toBe('');
    });

    it('DATE-STRESS-03: Date clearing on user input emits null', () => {
      const comp = TestBed.runInInjectionContext(() => new DateInputComponent());
      const changeSpy = vi.fn();
      const valSpy = vi.fn();
      comp.registerOnChange(changeSpy);
      comp.valueChange.subscribe(valSpy);

      (comp as any).onInput({ target: { value: '2026-08-18' } } as unknown as Event);
      expect(changeSpy).toHaveBeenCalledWith('2026-08-18');
      expect(valSpy).toHaveBeenCalledWith('2026-08-18');

      (comp as any).onInput({ target: { value: '' } } as unknown as Event);
      expect(changeSpy).toHaveBeenCalledWith(null);
      expect(valSpy).toHaveBeenCalledWith(null);
    });
  });

  describe('4. TextInputComponent Uppercase & Event Dispatch', () => {
    it('TEXT-STRESS-01: uppercase: true transforms lowercase and mixed-case on write and input', () => {
      const comp = TestBed.runInInjectionContext(() => {
        const c = new TextInputComponent();
        (c as any).uppercase = () => true;
        return c;
      });

      comp.writeValue('29aaaaa0000a1z5');
      expect((comp as any).innerValue()).toBe('29AAAAA0000A1Z5');

      const changeSpy = vi.fn();
      const valSpy = vi.fn();
      comp.registerOnChange(changeSpy);
      comp.valueChange.subscribe(valSpy);

      const target = { value: 'sbin0001234' };
      (comp as any).onInput({ target } as unknown as Event);

      expect(target.value).toBe('SBIN0001234');
      expect(changeSpy).toHaveBeenCalledWith('SBIN0001234');
      expect(valSpy).toHaveBeenCalledWith('SBIN0001234');
      expect((comp as any).innerValue()).toBe('SBIN0001234');
    });

    it('TEXT-STRESS-02: Enter key emits current innerValue', () => {
      const comp = TestBed.runInInjectionContext(() => new TextInputComponent());
      comp.writeValue('Direct barcode entry');

      const enterSpy = vi.fn();
      comp.enter.subscribe(enterSpy);

      (comp as any).onKeyDown({ key: 'Enter' } as KeyboardEvent);
      expect(enterSpy).toHaveBeenCalledWith('Direct barcode entry');

      // Other keys should not emit
      (comp as any).onKeyDown({ key: 'Tab' } as KeyboardEvent);
      expect(enterSpy).toHaveBeenCalledTimes(1);
    });
  });

  describe('5. SearchInputComponent Debounce & Clear Lifecycle', () => {
    it('SEARCH-STRESS-01: Rapid typing debounces and emits only once after timer expires', () => {
      vi.useFakeTimers();
      const comp = TestBed.runInInjectionContext(() => new SearchInputComponent());
      const searchSpy = vi.fn();
      comp.search.subscribe(searchSpy);

      (comp as any).onInput({ target: { value: 'a' } } as unknown as Event);
      vi.advanceTimersByTime(100);
      (comp as any).onInput({ target: { value: 'ab' } } as unknown as Event);
      vi.advanceTimersByTime(100);
      (comp as any).onInput({ target: { value: 'abc' } } as unknown as Event);

      expect(searchSpy).not.toHaveBeenCalled();

      vi.advanceTimersByTime(300);
      expect(searchSpy).toHaveBeenCalledTimes(1);
      expect(searchSpy).toHaveBeenCalledWith('abc');

      comp.ngOnDestroy();
      vi.useRealTimers();
    });

    it('SEARCH-STRESS-02: Enter key cancels pending debounce and immediately emits', () => {
      vi.useFakeTimers();
      const comp = TestBed.runInInjectionContext(() => new SearchInputComponent());
      const searchSpy = vi.fn();
      comp.search.subscribe(searchSpy);

      (comp as any).onInput({ target: { value: 'urgent search' } } as unknown as Event);
      expect(searchSpy).not.toHaveBeenCalled();

      const enterEvt = { key: 'Enter', preventDefault: vi.fn() } as unknown as KeyboardEvent;
      (comp as any).onKeyDown(enterEvt);

      expect(enterEvt.preventDefault).toHaveBeenCalled();
      expect(searchSpy).toHaveBeenCalledTimes(1);
      expect(searchSpy).toHaveBeenCalledWith('urgent search');

      // Advancing timer should not emit a second time
      vi.advanceTimersByTime(500);
      expect(searchSpy).toHaveBeenCalledTimes(1);

      comp.ngOnDestroy();
      vi.useRealTimers();
    });

    it('SEARCH-STRESS-03: onClear and Escape key reset search query and emit clear/valueChange', () => {
      const comp = TestBed.runInInjectionContext(() => new SearchInputComponent());
      comp.writeValue('Active Query');

      const changeSpy = vi.fn();
      const valSpy = vi.fn();
      const clearSpy = vi.fn();

      comp.registerOnChange(changeSpy);
      comp.valueChange.subscribe(valSpy);
      comp.clear.subscribe(clearSpy);

      // Escape key clears
      const escEvt = { key: 'Escape', preventDefault: vi.fn() } as unknown as KeyboardEvent;
      (comp as any).onKeyDown(escEvt);

      expect(escEvt.preventDefault).toHaveBeenCalled();
      expect((comp as any).innerValue()).toBe('');
      expect(changeSpy).toHaveBeenCalledWith('');
      expect(valSpy).toHaveBeenCalledWith('');
      expect(clearSpy).toHaveBeenCalledTimes(1);
    });
  });
});
