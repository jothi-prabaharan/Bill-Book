import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { BranchDateInputComponent } from './branch-date-input.component';

/**
 * `bb-branch-date-input` (TK-23, TK-135): what the deleted native date input's spec
 * covered before that component was deleted — ISO in and out, null for empty,
 * disabled — plus what only this one does: typing in the branch's pattern.
 * The branch pattern here is the shipped default, `dd/MM/yyyy`.
 */
interface Harness {
  value: () => string | null;
  text: () => string;
  problem: () => string;
  open: () => boolean;
  effectiveDisabled: () => boolean;
  onInput: (event: Event) => void;
  onBlur: (event: FocusEvent) => void;
  openCalendar: () => void;
}

const typed = (value: string) => ({ target: { value } }) as unknown as Event;

describe('BranchDateInputComponent', () => {
  let input: BranchDateInputComponent;
  let h: Harness;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    input = TestBed.runInInjectionContext(() => new BranchDateInputComponent());
    h = input as unknown as Harness;
  });

  describe('ISO in', () => {
    it('takes an ISO date and shows it in the branch pattern', () => {
      input.writeValue('2026-08-18');
      expect(h.value()).toBe('2026-08-18');
      expect(h.text()).toBe('18/08/2026');
    });

    it('keeps only the date of an ISO date-time', () => {
      input.writeValue('2026-08-18T22:15:00Z');
      expect(h.value()).toBe('2026-08-18');
    });

    it('takes a Date as its local calendar date', () => {
      input.writeValue(new Date(2026, 0, 5));
      expect(h.value()).toBe('2026-01-05');
    });

    it('treats null, an empty string and anything else as no date', () => {
      for (const empty of [null, undefined, '', 42, 'not a date', new Date('nonsense')]) {
        input.writeValue(empty);
        expect(h.value()).toBeNull();
        expect(h.text()).toBe('');
      }
    });
  });

  describe('ISO out', () => {
    it('publishes what was typed in the branch pattern as ISO, on blur', () => {
      const onChange = vi.fn();
      const valueChange = vi.fn();
      const touched = vi.fn();
      input.registerOnChange(onChange);
      input.registerOnTouched(touched);
      input.valueChange.subscribe(valueChange);

      h.onInput(typed('24-09-2026'));
      expect(onChange).not.toHaveBeenCalled();

      h.onBlur(new FocusEvent('blur'));
      expect(onChange).toHaveBeenCalledWith('2026-09-24');
      expect(valueChange).toHaveBeenCalledWith('2026-09-24');
      expect(touched).toHaveBeenCalledTimes(1);
      expect(h.text()).toBe('24/09/2026');
    });

    it('publishes null when the field is cleared', () => {
      const onChange = vi.fn();
      input.writeValue('2026-08-18');
      input.registerOnChange(onChange);

      h.onInput(typed('  '));
      h.onBlur(new FocusEvent('blur'));

      expect(onChange).toHaveBeenCalledWith(null);
      expect(h.problem()).toBe('');
    });

    it('keeps text it cannot read on screen, says why, and gives the form null', () => {
      const onChange = vi.fn();
      input.registerOnChange(onChange);

      h.onInput(typed('31/02/2026'));
      h.onBlur(new FocusEvent('blur'));

      expect(onChange).toHaveBeenCalledWith(null);
      expect(h.text()).toBe('31/02/2026');
      expect(h.problem()).toContain('dd/mm/yyyy');
    });

    it('does not publish when nothing was typed', () => {
      const onChange = vi.fn();
      input.writeValue('2026-08-18');
      input.registerOnChange(onChange);

      h.onBlur(new FocusEvent('blur'));

      expect(onChange).not.toHaveBeenCalled();
    });
  });

  describe('disabled', () => {
    it('follows setDisabledState and keeps the calendar shut', () => {
      expect(h.effectiveDisabled()).toBe(false);

      input.setDisabledState(true);
      expect(h.effectiveDisabled()).toBe(true);
      h.openCalendar();
      expect(h.open()).toBe(false);

      input.setDisabledState(false);
      expect(h.effectiveDisabled()).toBe(false);
    });
  });
});
