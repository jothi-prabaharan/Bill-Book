import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  FormControl,
  FormGroup,
  Validators,
} from '@angular/forms';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { CurrencyInputComponent } from './currency-input/currency-input.component';
import { DateInputComponent } from './date-input/date-input.component';
import { NumberInputComponent } from './number-input/number-input.component';
import { SearchInputComponent } from './search-input/search-input.component';
import { TextInputComponent } from './text-input/text-input.component';

// Interfaces exposing internal protected / private state for rigorous testing
interface DateInputHarness {
  innerValue: () => string;
  effectiveDisabled: () => boolean;
  onInput: (event: Event) => void;
  onBlur: (event: FocusEvent) => void;
  onFocus: (event: FocusEvent) => void;
}

interface CurrencyInputHarness {
  displayValue: () => string;
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

interface NumberInputHarness {
  displayValue: () => string;
  rawNumericValue: number | null;
  effectiveDisabled: () => boolean;
  decimals: () => number | null;
  onInput: (event: Event) => void;
  onBlur: (event: FocusEvent) => void;
  onFocus: (event: FocusEvent) => void;
}

interface SearchInputHarness {
  innerValue: () => string;
  effectiveDisabled: () => boolean;
  debounceMs: () => number;
  onInput: (event: Event) => void;
  onKeyDown: (event: KeyboardEvent) => void;
  onClear: () => void;
  onBlur: () => void;
}

interface TextInputHarness {
  innerValue: () => string;
  effectiveDisabled: () => boolean;
  uppercase: () => boolean;
  onInput: (event: Event) => void;
  onKeyDown: (event: KeyboardEvent) => void;
  onBlur: (event: FocusEvent) => void;
  onFocus: (event: FocusEvent) => void;
}

describe('Empirical Form Lifecycle & CVA Stress Test Suite (Challenger 2)', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({});
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  // =========================================================================
  // SECTION 1: REACTIVE FORMS COMPLETE LIFECYCLE ACROSS ALL 5 COMPONENTS
  // =========================================================================
  describe('1. Reactive Forms Lifecycle & CVA Binding', () => {
    it('RF-01: FormGroup with all 5 controls binds initial values, setValue, and patchValue', () => {
      const form = new FormGroup({
        date: new FormControl<string | null>('2026-08-18', [Validators.required]),
        curr: new FormControl<number | null>(1500.5, [Validators.required, Validators.min(100)]),
        num: new FormControl<number | null>(42, [Validators.required, Validators.min(1), Validators.max(100)]),
        search: new FormControl<string>('initial query', [Validators.required]),
        text: new FormControl<string>('Acme Corp', [Validators.required, Validators.minLength(3)]),
      });

      const dateComp = TestBed.runInInjectionContext(() => new DateInputComponent());
      const currComp = TestBed.runInInjectionContext(() => new CurrencyInputComponent());
      const numComp = TestBed.runInInjectionContext(() => new NumberInputComponent());
      const searchComp = TestBed.runInInjectionContext(() => new SearchInputComponent());
      const textComp = TestBed.runInInjectionContext(() => new TextInputComponent());

      const dateH = dateComp as unknown as DateInputHarness;
      const currH = currComp as unknown as CurrencyInputHarness;
      const numH = numComp as unknown as NumberInputHarness;
      const searchH = searchComp as unknown as SearchInputHarness;
      const textH = textComp as unknown as TextInputHarness;

      // 1. Initial writeValue from form controls
      dateComp.writeValue(form.get('date')!.value);
      currComp.writeValue(form.get('curr')!.value);
      numComp.writeValue(form.get('num')!.value);
      searchComp.writeValue(form.get('search')!.value);
      textComp.writeValue(form.get('text')!.value);

      expect(dateH.innerValue()).toBe('2026-08-18');
      expect(currH.displayValue()).toBe('1500.50');
      expect(numH.displayValue()).toBe('42');
      expect(searchH.innerValue()).toBe('initial query');
      expect(textH.innerValue()).toBe('Acme Corp');
      expect(form.valid).toBe(true);

      // 2. form.setValue() populates all 5 components
      form.setValue({
        date: '2027-01-01',
        curr: 9999.99,
        num: 88,
        search: 'new search',
        text: 'Global Retail',
      });
      dateComp.writeValue(form.get('date')!.value);
      currComp.writeValue(form.get('curr')!.value);
      numComp.writeValue(form.get('num')!.value);
      searchComp.writeValue(form.get('search')!.value);
      textComp.writeValue(form.get('text')!.value);

      expect(dateH.innerValue()).toBe('2027-01-01');
      expect(currH.displayValue()).toBe('9999.99');
      expect(numH.displayValue()).toBe('88');
      expect(searchH.innerValue()).toBe('new search');
      expect(textH.innerValue()).toBe('Global Retail');

      // 3. form.patchValue() selectively updates individual components
      form.patchValue({
        curr: 250.75,
        text: 'Patched Name',
      });
      currComp.writeValue(form.get('curr')!.value);
      textComp.writeValue(form.get('text')!.value);

      expect(currH.displayValue()).toBe('250.75');
      expect(textH.innerValue()).toBe('Patched Name');
      expect(dateH.innerValue()).toBe('2027-01-01'); // Unmodified
    });

    it('RF-02: User input propagation, dirty state transition, and validator checking', () => {
      const form = new FormGroup({
        date: new FormControl<string | null>(null, [Validators.required]),
        curr: new FormControl<number | null>(null, [Validators.required, Validators.min(500)]),
        num: new FormControl<number | null>(null, [Validators.required, Validators.min(10), Validators.max(50)]),
        search: new FormControl<string>('', [Validators.required]),
        text: new FormControl<string>('', [Validators.required, Validators.minLength(5)]),
      });

      const dateComp = TestBed.runInInjectionContext(() => new DateInputComponent());
      const currComp = TestBed.runInInjectionContext(() => new CurrencyInputComponent());
      const numComp = TestBed.runInInjectionContext(() => new NumberInputComponent());
      const searchComp = TestBed.runInInjectionContext(() => new SearchInputComponent());
      const textComp = TestBed.runInInjectionContext(() => new TextInputComponent());

      // Wire CVA registered onChange
      dateComp.registerOnChange((val) => {
        form.get('date')!.setValue(val);
        form.get('date')!.markAsDirty();
      });
      currComp.registerOnChange((val) => {
        form.get('curr')!.setValue(val);
        form.get('curr')!.markAsDirty();
      });
      numComp.registerOnChange((val) => {
        form.get('num')!.setValue(val);
        form.get('num')!.markAsDirty();
      });
      searchComp.registerOnChange((val) => {
        form.get('search')!.setValue(val);
        form.get('search')!.markAsDirty();
      });
      textComp.registerOnChange((val) => {
        form.get('text')!.setValue(val);
        form.get('text')!.markAsDirty();
      });

      expect(form.pristine).toBe(true);
      expect(form.valid).toBe(false);

      // User types valid inputs
      (dateComp as unknown as DateInputHarness).onInput({ target: { value: '2026-11-20' } } as unknown as Event);
      (currComp as unknown as CurrencyInputHarness).onInput({ target: { value: '750' } } as unknown as Event);
      (numComp as unknown as NumberInputHarness).onInput({ target: { value: '25' } } as unknown as Event);
      (searchComp as unknown as SearchInputHarness).onInput({ target: { value: 'invoice query' } } as unknown as Event);
      (textComp as unknown as TextInputHarness).onInput({ target: { value: 'Valid Name' } } as unknown as Event);

      expect(form.dirty).toBe(true);
      expect(form.valid).toBe(true);
      expect(form.value).toEqual({
        date: '2026-11-20',
        curr: 750,
        num: 25,
        search: 'invoice query',
        text: 'Valid Name',
      });

      // User inputs invalid value for curr (below min 500)
      (currComp as unknown as CurrencyInputHarness).onInput({ target: { value: '200' } } as unknown as Event);
      expect(form.get('curr')!.valid).toBe(false);
      expect(form.get('curr')!.hasError('min')).toBe(true);
      expect(form.valid).toBe(false);

      // User inputs invalid value for num (exceeds max 50)
      (numComp as unknown as NumberInputHarness).onInput({ target: { value: '75' } } as unknown as Event);
      expect(form.get('num')!.valid).toBe(false);
      expect(form.get('num')!.hasError('max')).toBe(true);
      expect(form.valid).toBe(false);
    });

    it('RF-03: Blur events trigger onTouched and set control.touched across all 5 components', () => {
      const form = new FormGroup({
        date: new FormControl<string | null>(null),
        curr: new FormControl<number | null>(null),
        num: new FormControl<number | null>(null),
        search: new FormControl<string>(''),
        text: new FormControl<string>(''),
      });

      const dateComp = TestBed.runInInjectionContext(() => new DateInputComponent());
      const currComp = TestBed.runInInjectionContext(() => new CurrencyInputComponent());
      const numComp = TestBed.runInInjectionContext(() => new NumberInputComponent());
      const searchComp = TestBed.runInInjectionContext(() => new SearchInputComponent());
      const textComp = TestBed.runInInjectionContext(() => new TextInputComponent());

      dateComp.registerOnTouched(() => form.get('date')!.markAsTouched());
      currComp.registerOnTouched(() => form.get('curr')!.markAsTouched());
      numComp.registerOnTouched(() => form.get('num')!.markAsTouched());
      searchComp.registerOnTouched(() => form.get('search')!.markAsTouched());
      textComp.registerOnTouched(() => form.get('text')!.markAsTouched());

      expect(form.touched).toBe(false);

      (dateComp as unknown as DateInputHarness).onBlur(new FocusEvent('blur'));
      (currComp as unknown as CurrencyInputHarness).onBlur(new FocusEvent('blur'));
      (numComp as unknown as NumberInputHarness).onBlur(new FocusEvent('blur'));
      (searchComp as unknown as SearchInputHarness).onBlur();
      (textComp as unknown as TextInputHarness).onBlur(new FocusEvent('blur'));

      expect(form.get('date')!.touched).toBe(true);
      expect(form.get('curr')!.touched).toBe(true);
      expect(form.get('num')!.touched).toBe(true);
      expect(form.get('search')!.touched).toBe(true);
      expect(form.get('text')!.touched).toBe(true);
      expect(form.touched).toBe(true);
    });

    it('RF-04: formControl.disable() / enable() and setDisabledState across all 5 components', () => {
      const dateComp = TestBed.runInInjectionContext(() => new DateInputComponent());
      const currComp = TestBed.runInInjectionContext(() => new CurrencyInputComponent());
      const numComp = TestBed.runInInjectionContext(() => new NumberInputComponent());
      const searchComp = TestBed.runInInjectionContext(() => new SearchInputComponent());
      const textComp = TestBed.runInInjectionContext(() => new TextInputComponent());

      const components = [dateComp, currComp, numComp, searchComp, textComp];

      // Initial state: not disabled
      for (const comp of components) {
        expect((comp as unknown as { effectiveDisabled: () => boolean }).effectiveDisabled()).toBe(false);
      }

      // Disable all via CVA
      for (const comp of components) {
        comp.setDisabledState(true);
        expect((comp as unknown as { effectiveDisabled: () => boolean }).effectiveDisabled()).toBe(true);
      }

      // Enable all via CVA
      for (const comp of components) {
        comp.setDisabledState(false);
        expect((comp as unknown as { effectiveDisabled: () => boolean }).effectiveDisabled()).toBe(false);
      }
    });

    it('RF-05: form.reset() resets values, dirty, and touched states cleanly', () => {
      const form = new FormGroup({
        date: new FormControl<string | null>('2026-08-18'),
        curr: new FormControl<number | null>(500),
        num: new FormControl<number | null>(10),
        search: new FormControl<string>('query'),
        text: new FormControl<string>('text'),
      });

      const dateComp = TestBed.runInInjectionContext(() => new DateInputComponent());
      const currComp = TestBed.runInInjectionContext(() => new CurrencyInputComponent());
      const numComp = TestBed.runInInjectionContext(() => new NumberInputComponent());
      const searchComp = TestBed.runInInjectionContext(() => new SearchInputComponent());
      const textComp = TestBed.runInInjectionContext(() => new TextInputComponent());

      // Mark form dirty & touched
      form.markAsDirty();
      form.markAsTouched();
      expect(form.dirty).toBe(true);
      expect(form.touched).toBe(true);

      // Reset form
      form.reset();
      dateComp.writeValue(form.get('date')!.value);
      currComp.writeValue(form.get('curr')!.value);
      numComp.writeValue(form.get('num')!.value);
      searchComp.writeValue(form.get('search')!.value);
      textComp.writeValue(form.get('text')!.value);

      expect(form.pristine).toBe(true);
      expect(form.touched).toBe(false);
      expect((dateComp as unknown as DateInputHarness).innerValue()).toBe('');
      expect((currComp as unknown as CurrencyInputHarness).displayValue()).toBe('');
      expect((numComp as unknown as NumberInputHarness).displayValue()).toBe('');
      expect((searchComp as unknown as SearchInputHarness).innerValue()).toBe('');
      expect((textComp as unknown as TextInputHarness).innerValue()).toBe('');
    });
  });

  // =========================================================================
  // SECTION 2: TEMPLATE-DRIVEN FORMS ([(ngModel)]) TWO-WAY BINDING SIMULATION
  // =========================================================================
  describe('2. Template-Driven Forms (ngModel) Lifecycle & Two-Way Sync', () => {
    it('TD-01: Simulates full [(ngModel)] two-way synchronization and dynamic updates', () => {
      let model = {
        date: '2026-05-10',
        curr: 1200.0,
        num: 15,
        search: 'supplier search',
        text: 'Initial Vendor',
      };

      const dateComp = TestBed.runInInjectionContext(() => new DateInputComponent());
      const currComp = TestBed.runInInjectionContext(() => new CurrencyInputComponent());
      const numComp = TestBed.runInInjectionContext(() => new NumberInputComponent());
      const searchComp = TestBed.runInInjectionContext(() => new SearchInputComponent());
      const textComp = TestBed.runInInjectionContext(() => new TextInputComponent());

      // Wire two-way ngModel binding: CVA onChange updates model, and valueChange emits
      dateComp.registerOnChange((val) => (model.date = val ?? ''));
      currComp.registerOnChange((val) => (model.curr = val ?? 0));
      numComp.registerOnChange((val) => (model.num = val ?? 0));
      searchComp.registerOnChange((val) => (model.search = val));
      textComp.registerOnChange((val) => (model.text = val));

      // Initial model push via writeValue
      dateComp.writeValue(model.date);
      currComp.writeValue(model.curr);
      numComp.writeValue(model.num);
      searchComp.writeValue(model.search);
      textComp.writeValue(model.text);

      expect((dateComp as unknown as DateInputHarness).innerValue()).toBe('2026-05-10');
      expect((currComp as unknown as CurrencyInputHarness).displayValue()).toBe('1200.00');
      expect((numComp as unknown as NumberInputHarness).displayValue()).toBe('15');
      expect((searchComp as unknown as SearchInputHarness).innerValue()).toBe('supplier search');
      expect((textComp as unknown as TextInputHarness).innerValue()).toBe('Initial Vendor');

      // User typing modifies model
      (textComp as unknown as TextInputHarness).onInput({ target: { value: 'Updated Vendor' } } as unknown as Event);
      expect(model.text).toBe('Updated Vendor');

      (currComp as unknown as CurrencyInputHarness).onInput({ target: { value: '3500.50' } } as unknown as Event);
      expect(model.curr).toBe(3500.50);

      // Model changes dynamically from host -> writeValue updates component
      model = {
        date: '2027-12-31',
        curr: 888.88,
        num: 99,
        search: 'fresh query',
        text: 'External Update',
      };
      dateComp.writeValue(model.date);
      currComp.writeValue(model.curr);
      numComp.writeValue(model.num);
      searchComp.writeValue(model.search);
      textComp.writeValue(model.text);

      expect((dateComp as unknown as DateInputHarness).innerValue()).toBe('2027-12-31');
      expect((currComp as unknown as CurrencyInputHarness).displayValue()).toBe('888.88');
      expect((numComp as unknown as NumberInputHarness).displayValue()).toBe('99');
      expect((searchComp as unknown as SearchInputHarness).innerValue()).toBe('fresh query');
      expect((textComp as unknown as TextInputHarness).innerValue()).toBe('External Update');
    });

    it('TD-02: [ngModel] unidirectional with (ngModelChange) handler', () => {
      const priceSignal = signal<number | null>(500);
      const discountSignal = signal<number | null>(50);
      const netTotal = computed(() => (priceSignal() ?? 0) - (discountSignal() ?? 0));

      const priceComp = TestBed.runInInjectionContext(() => new CurrencyInputComponent());
      const discountComp = TestBed.runInInjectionContext(() => new CurrencyInputComponent());

      priceComp.registerOnChange((val) => priceSignal.set(val));
      discountComp.registerOnChange((val) => discountSignal.set(val));

      priceComp.writeValue(priceSignal());
      discountComp.writeValue(discountSignal());

      expect(netTotal()).toBe(450);

      // Change price
      (priceComp as unknown as CurrencyInputHarness).onInput({ target: { value: '1000' } } as unknown as Event);
      expect(priceSignal()).toBe(1000);
      expect(netTotal()).toBe(950);

      // Change discount
      (discountComp as unknown as CurrencyInputHarness).onInput({ target: { value: '150' } } as unknown as Event);
      expect(discountSignal()).toBe(150);
      expect(netTotal()).toBe(850);
    });
  });

  // =========================================================================
  // SECTION 3: SIGNAL STATE & CHANGE DETECTION LOOP PREVENTION
  // =========================================================================
  describe('3. Signal State & Feedback Loop Guard', () => {
    it('SIG-01: High frequency typing (100 sequential events) executes cleanly without feedback loops', () => {
      const textComp = TestBed.runInInjectionContext(() => new TextInputComponent());
      const textH = textComp as unknown as TextInputHarness;

      let changeCount = 0;
      let emitCount = 0;

      textComp.registerOnChange((_val) => {
        changeCount++;
      });
      textComp.valueChange.subscribe(() => {
        emitCount++;
      });

      for (let i = 1; i <= 100; i++) {
        textH.onInput({ target: { value: `Query_${i}` } } as unknown as Event);
      }

      expect(changeCount).toBe(100);
      expect(emitCount).toBe(100);
      expect(textH.innerValue()).toBe('Query_100');
    });

    it('SIG-02: Recursive writeValue / onChange loop prevention', () => {
      const currComp = TestBed.runInInjectionContext(() => new CurrencyInputComponent());
      const currH = currComp as unknown as CurrencyInputHarness;

      let callCount = 0;
      currComp.registerOnChange((val) => {
        callCount++;
        // If a consumer incorrectly echoes writeValue inside onChange, verify it doesn't cause an infinite call loop
        if (callCount < 5) {
          currComp.writeValue(val);
        }
      });

      currH.onInput({ target: { value: '500' } } as unknown as Event);
      expect(callCount).toBe(1); // writeValue does NOT trigger onChange, so loop cannot occur!
      expect(currH.displayValue()).toBe('500.00');
    });
  });

  // =========================================================================
  // SECTION 4: DEEP ADVERSARIAL BOUNDARY TESTS FOR EACH COMPONENT
  // =========================================================================
  describe('4. Deep Adversarial Boundary Testing', () => {
    it('BOUND-01: CurrencyInput with inPaise=true float precision stability', () => {
      const comp = TestBed.runInInjectionContext(() => {
        const c = new CurrencyInputComponent();
        (c as unknown as { inPaise: () => boolean }).inPaise = () => true;
        return c;
      });
      const harness = comp as unknown as CurrencyInputHarness;

      const difficultFloats = [
        { rupeeStr: '1.14', expectedPaise: 114 },
        { rupeeStr: '1.29', expectedPaise: 129 },
        { rupeeStr: '29.99', expectedPaise: 2999 },
        { rupeeStr: '57.01', expectedPaise: 5701 },
        { rupeeStr: '0.01', expectedPaise: 1 },
        { rupeeStr: '10000000.50', expectedPaise: 1000000050 },
      ];

      for (const item of difficultFloats) {
        const spy = vi.fn();
        comp.registerOnChange(spy);
        harness.onInput({ target: { value: item.rupeeStr } } as unknown as Event);
        expect(spy).toHaveBeenCalledWith(item.expectedPaise);
        expect(harness.rawNumericValue).toBe(item.expectedPaise);

        // writeValue back
        comp.writeValue(item.expectedPaise);
        expect(harness.displayValue()).toBe(Number(item.rupeeStr).toFixed(2));
      }
    });

    it('BOUND-02: DateInput ISO strings, Date objects, nulls, and leap years', () => {
      const comp = TestBed.runInInjectionContext(() => new DateInputComponent());
      const harness = comp as unknown as DateInputHarness;

      // Leap year Feb 29
      comp.writeValue('2028-02-29');
      expect(harness.innerValue()).toBe('2028-02-29');

      // Date object
      const d = new Date(2026, 7, 18); // Month is 0-indexed (7 = Aug)
      comp.writeValue(d);
      expect(harness.innerValue()).toBe('2026-08-18');

      // Null, undefined, empty
      comp.writeValue(null);
      expect(harness.innerValue()).toBe('');
      comp.writeValue(undefined);
      expect(harness.innerValue()).toBe('');
      comp.writeValue('');
      expect(harness.innerValue()).toBe('');
    });

    it('BOUND-03: NumberInput zero (0) vs null vs empty string and step scaling', () => {
      const comp = TestBed.runInInjectionContext(() => new NumberInputComponent());
      const harness = comp as unknown as NumberInputHarness;

      // 0 is valid numeric zero
      comp.writeValue(0);
      expect(harness.displayValue()).toBe('0');
      expect(harness.rawNumericValue).toBe(0);

      // null clears
      comp.writeValue(null);
      expect(harness.displayValue()).toBe('');
      expect(harness.rawNumericValue).toBeNull();

      // Fractional stepping (0.005)
      const changeSpy = vi.fn();
      comp.registerOnChange(changeSpy);
      harness.onInput({ target: { value: '0.005' } } as unknown as Event);
      expect(changeSpy).toHaveBeenCalledWith(0.005);
      expect(harness.rawNumericValue).toBe(0.005);
    });

    it('BOUND-04: SearchInput debounce timer cancellation on clear and destroy', () => {
      vi.useFakeTimers();
      const comp = TestBed.runInInjectionContext(() => new SearchInputComponent());
      const harness = comp as unknown as SearchInputHarness;

      const searchSpy = vi.fn();
      comp.search.subscribe(searchSpy);

      harness.onInput({ target: { value: 'searching' } } as unknown as Event);
      expect(searchSpy).not.toHaveBeenCalled();

      // User clears before debounce fires
      harness.onClear();
      vi.advanceTimersByTime(300);

      // Debounce timer was cancelled by onClear
      expect(searchSpy).not.toHaveBeenCalled();
      expect(harness.innerValue()).toBe('');

      comp.ngOnDestroy();
    });

    it('BOUND-05: TextInput uppercase GSTIN / PAN transformations and enter key output', () => {
      const comp = TestBed.runInInjectionContext(() => {
        const c = new TextInputComponent();
        (c as unknown as { uppercase: () => boolean }).uppercase = () => true;
        return c;
      });
      const harness = comp as unknown as TextInputHarness;

      // writeValue with lowercase converts to uppercase
      comp.writeValue('33aaaaa0000a1z5');
      expect(harness.innerValue()).toBe('33AAAAA0000A1Z5');

      // typing with lowercase converts to uppercase
      const changeSpy = vi.fn();
      comp.registerOnChange(changeSpy);
      const target = { value: 'abcde1234f' };
      harness.onInput({ target } as unknown as Event);

      expect(target.value).toBe('ABCDE1234F');
      expect(changeSpy).toHaveBeenCalledWith('ABCDE1234F');
      expect(harness.innerValue()).toBe('ABCDE1234F');

      // Enter key trigger
      const enterSpy = vi.fn();
      comp.enter.subscribe(enterSpy);
      harness.onKeyDown({ key: 'Enter' } as unknown as KeyboardEvent);
      expect(enterSpy).toHaveBeenCalledWith('ABCDE1234F');
    });
  });
});
