import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { CheckboxComponent } from './checkbox/checkbox.component';
import { DateTimeInputComponent } from './datetime/datetime-input.component';
import { FileInputComponent } from './file/file-input.component';
import { RadioGroupComponent } from './radio/radio-group.component';
import { SelectComponent } from './select/select.component';
import { TextareaComponent } from './textarea/textarea.component';
import { BbRadioOption, BbSelectOption } from './field.model';

function build<T>(make: () => T): T {
  return TestBed.runInInjectionContext(make);
}

function set(component: unknown, name: string, value: unknown): void {
  (component as Record<string, unknown>)[name] = () => value;
}

interface CheckboxHarness {
  checked: () => boolean;
  boxText: () => string;
  groupLabel: () => string;
  effectiveDisabled: () => boolean;
  onChangeEvent: (event: Event) => void;
}

interface RadioHarness {
  selected: () => string | number | null;
  groupName: () => string;
  optionId: (index: number) => string;
  optionDescriptionId: (index: number) => string;
  onSelect: (option: BbRadioOption) => void;
  isSelected: (option: BbRadioOption) => boolean;
}

interface SelectHarness {
  selected: () => string | number | null;
  selectedIndex: () => string;
  isEmpty: () => boolean;
  groups: () => string[];
  ungrouped: () => readonly BbSelectOption[];
  optionsIn: (group: string) => readonly BbSelectOption[];
  indexOf: (option: BbSelectOption) => string;
  onSelect: (event: Event) => void;
  onBlur: () => void;
}

interface TextareaHarness {
  innerValue: () => string;
  used: () => number;
  counterVisible: () => boolean;
  counterText: () => string;
  onInput: (event: Event) => void;
  onBlur: (event: FocusEvent) => void;
}

interface FileHarness {
  files: () => readonly File[];
  rejected: () => readonly { name: string; reason: string }[];
  summary: () => string;
  onPicked: (event: Event) => void;
  remove: (index: number) => void;
  clearAll: () => void;
  reasonOf: (entry: { name: string; size: number; reason: string }) => string;
}

interface DateHarness {
  innerValue: () => string;
  stepAttr: string | null;
  onInput: (event: Event) => void;
}

/** A picker event carrying files, as the DOM would deliver it. */
function pick(files: File[]): Event {
  return { target: { files } } as unknown as Event;
}

describe('Selection and long-form inputs', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({});
  });

  describe('CheckboxComponent', () => {
    it('CHK-01: checked and unchecked round-trip through the form', () => {
      const component = build(() => new CheckboxComponent());
      const harness = component as unknown as CheckboxHarness;
      const changed = vi.fn();
      component.registerOnChange(changed);

      component.writeValue(true);
      expect(harness.checked()).toBe(true);

      component.writeValue(false);
      expect(harness.checked()).toBe(false);

      harness.onChangeEvent({ target: { checked: true } } as unknown as Event);
      expect(changed).toHaveBeenCalledWith(true);
      expect(harness.checked()).toBe(true);
    });

    it('CHK-02: anything that is not true reads as unchecked', () => {
      const component = build(() => new CheckboxComponent());
      const harness = component as unknown as CheckboxHarness;

      component.writeValue(null);
      expect(harness.checked()).toBe(false);
      component.writeValue(undefined);
      expect(harness.checked()).toBe(false);
      component.writeValue('yes');
      expect(harness.checked()).toBe(false);
    });

    it('CHK-03: changing marks the control touched', () => {
      const component = build(() => new CheckboxComponent());
      const harness = component as unknown as CheckboxHarness;
      const touched = vi.fn();

      component.registerOnTouched(touched);
      harness.onChangeEvent({ target: { checked: true } } as unknown as Event);
      expect(touched).toHaveBeenCalledTimes(1);
    });

    it('CHK-04: disabled by the attribute or by the form', () => {
      const component = build(() => new CheckboxComponent());
      const harness = component as unknown as CheckboxHarness;

      expect(harness.effectiveDisabled()).toBe(false);
      component.setDisabledState(true);
      expect(harness.effectiveDisabled()).toBe(true);
    });

    it('CHK-05: a lone label becomes the text beside the box', () => {
      const only = build(() => {
        const made = new CheckboxComponent();
        set(made, 'label', 'Active');
        return made;
      });
      const onlyHarness = only as unknown as CheckboxHarness;
      expect(onlyHarness.boxText()).toBe('Active');
      expect(onlyHarness.groupLabel()).toBe('');

      const both = build(() => {
        const made = new CheckboxComponent();
        set(made, 'label', 'Status');
        set(made, 'text', 'Active');
        return made;
      });
      const bothHarness = both as unknown as CheckboxHarness;
      expect(bothHarness.boxText()).toBe('Active');
      expect(bothHarness.groupLabel()).toBe('Status');
    });
  });

  describe('RadioGroupComponent', () => {
    const options: BbRadioOption<string>[] = [
      { value: 'courier', label: 'By courier' },
      { value: 'collect', label: 'Collected', description: 'The customer comes to the branch.' },
      { value: 'post', label: 'By post', disabled: true },
    ];

    function group() {
      const component = build(() => {
        const made = new RadioGroupComponent<string>();
        set(made, 'options', options);
        return made;
      });
      return { component, harness: component as unknown as RadioHarness };
    }

    it('RAD-01: choosing an option publishes its value', () => {
      const { component, harness } = group();
      const changed = vi.fn();
      component.registerOnChange(changed);

      harness.onSelect(options[0]);
      expect(changed).toHaveBeenCalledWith('courier');
      expect(harness.isSelected(options[0])).toBe(true);
      expect(harness.isSelected(options[1])).toBe(false);
    });

    it('RAD-02: a disabled option cannot be chosen', () => {
      const { component, harness } = group();
      const changed = vi.fn();
      component.registerOnChange(changed);

      harness.onSelect(options[2]);
      expect(changed).not.toHaveBeenCalled();
      expect(harness.selected()).toBeNull();
    });

    it('RAD-03: a readonly group refuses a change but keeps its value', () => {
      const component = build(() => {
        const made = new RadioGroupComponent<string>();
        set(made, 'options', options);
        set(made, 'readonly', true);
        return made;
      });
      const harness = component as unknown as RadioHarness;
      const changed = vi.fn();

      component.writeValue('courier');
      component.registerOnChange(changed);
      harness.onSelect(options[1]);

      expect(changed).not.toHaveBeenCalled();
      expect(harness.selected()).toBe('courier');
    });

    it('RAD-04: two groups never share a name, which is what makes them separate', () => {
      const first = group();
      const second = group();
      expect(first.harness.groupName()).not.toBe(second.harness.groupName());
    });

    it('RAD-05: an option with a description points at it with aria-describedby', () => {
      const { harness } = group();
      expect(harness.optionDescriptionId(1)).toBe(`${harness.optionId(1)}-description`);
    });

    it('RAD-06: choosing marks the group touched', () => {
      const { component, harness } = group();
      const touched = vi.fn();
      component.registerOnTouched(touched);
      harness.onSelect(options[0]);
      expect(touched).toHaveBeenCalledTimes(1);
    });
  });

  describe('SelectComponent', () => {
    const options: BbSelectOption<number>[] = [
      { value: 11, label: 'Cash' },
      { value: 22, label: 'Bank', group: 'Assets' },
      { value: 33, label: 'Sales revenue', group: 'Income', disabled: true },
    ];

    function select() {
      const component = build(() => {
        const made = new SelectComponent<number>();
        set(made, 'options', options);
        return made;
      });
      return { component, harness: component as unknown as SelectHarness };
    }

    it('SEL-01: the original value is published, never a stringified one', () => {
      // The element's value is always a string, so options are addressed by
      // index; round-tripping through String() is how a numeric id reaches the
      // API quoted.
      const { component, harness } = select();
      const changed = vi.fn();
      component.registerOnChange(changed);

      harness.onSelect({ target: { value: '0' } } as unknown as Event);
      expect(changed).toHaveBeenCalledWith(11);
      expect(typeof changed.mock.calls[0][0]).toBe('number');
    });

    it('SEL-02: the placeholder row clears the value', () => {
      const { component, harness } = select();
      const changed = vi.fn();

      component.writeValue(11);
      component.registerOnChange(changed);
      harness.onSelect({ target: { value: '' } } as unknown as Event);

      expect(changed).toHaveBeenCalledWith(null);
      expect(harness.selected()).toBeNull();
    });

    it('SEL-03: the selected index follows the value, and is empty for none', () => {
      const { component, harness } = select();

      expect(harness.selectedIndex()).toBe('');
      component.writeValue(22);
      expect(harness.selectedIndex()).toBe('1');

      // A value that is not among the options shows as nothing chosen rather
      // than as the first option.
      component.writeValue(99);
      expect(harness.selectedIndex()).toBe('');
    });

    it('SEL-04: groups appear in the order they first occur', () => {
      const { harness } = select();
      expect(harness.groups()).toEqual(['Assets', 'Income']);
      expect(harness.ungrouped().map((option) => option.value)).toEqual([11]);
      expect(harness.optionsIn('Assets').map((option) => option.value)).toEqual([22]);
    });

    it('SEL-05: an empty list says so instead of showing a placeholder', () => {
      const component = build(() => new SelectComponent<number>());
      expect((component as unknown as SelectHarness).isEmpty()).toBe(true);
    });

    it('SEL-06: loading is not the same as empty', () => {
      const component = build(() => {
        const made = new SelectComponent<number>();
        set(made, 'loading', true);
        return made;
      });
      expect((component as unknown as SelectHarness).isEmpty()).toBe(false);
    });

    it('SEL-07: blur marks the control touched', () => {
      const { component, harness } = select();
      const touched = vi.fn();
      component.registerOnTouched(touched);
      harness.onBlur();
      expect(touched).toHaveBeenCalledTimes(1);
    });
  });

  describe('TextareaComponent', () => {
    it('TXA-01: value binding and required', () => {
      const component = build(() => {
        const made = new TextareaComponent();
        set(made, 'required', true);
        return made;
      });
      const harness = component as unknown as TextareaHarness;

      component.writeValue('Raised against the wrong customer');
      expect(harness.innerValue()).toBe('Raised against the wrong customer');
      expect(component.required()).toBe(true);
    });

    it('TXA-02: readonly is stated on the element and the value still submits', () => {
      const component = build(() => {
        const made = new TextareaComponent();
        set(made, 'readonly', true);
        return made;
      });
      component.writeValue('Terms as agreed');
      expect(component.readonly()).toBe(true);
      expect((component as unknown as TextareaHarness).innerValue()).toBe('Terms as agreed');
    });

    it('TXA-03: the counter appears near the limit, not from the first keystroke', () => {
      const component = build(() => {
        const made = new TextareaComponent();
        set(made, 'maxlength', 10);
        return made;
      });
      const harness = component as unknown as TextareaHarness;

      component.writeValue('abc');
      expect(harness.counterVisible()).toBe(false);

      component.writeValue('abcdefghi');
      expect(harness.counterVisible()).toBe(true);
      expect(harness.counterText()).toBe('9 / 10');
    });

    it('TXA-04: no limit means no counter at all', () => {
      const component = build(() => new TextareaComponent());
      component.writeValue('x'.repeat(500));
      expect((component as unknown as TextareaHarness).counterVisible()).toBe(false);
    });
  });

  describe('FileInputComponent', () => {
    function file(name: string, size: number, type = ''): File {
      // jsdom's File honours a size only through its parts, so the blob is
      // built at the length the test needs.
      return new File(['x'.repeat(size)], name, { type });
    }

    it('FIL-01: an accepted file becomes the value', () => {
      const component = build(() => new FileInputComponent());
      const harness = component as unknown as FileHarness;
      const changed = vi.fn();
      component.registerOnChange(changed);

      const chosen = file('statement.csv', 10, 'text/csv');
      harness.onPicked(pick([chosen]));

      expect(harness.files()).toEqual([chosen]);
      expect(changed).toHaveBeenCalledWith([chosen]);
      expect(harness.summary()).toBe('statement.csv');
    });

    it('FIL-02: a file of the wrong kind is refused with a reason', () => {
      // `accept` only filters the browser's dialogue — a file dragged in or
      // chosen through "All files" still arrives.
      const component = build(() => {
        const made = new FileInputComponent();
        set(made, 'accept', '.csv,.xlsx');
        return made;
      });
      const harness = component as unknown as FileHarness;

      harness.onPicked(pick([file('photo.png', 10, 'image/png')]));

      expect(harness.files()).toEqual([]);
      expect(harness.rejected()).toEqual([
        { name: 'photo.png', size: 10, reason: 'type' },
      ]);
    });

    it('FIL-03: a media type rule with a wildcard is honoured', () => {
      const component = build(() => {
        const made = new FileInputComponent();
        set(made, 'accept', 'image/*');
        return made;
      });
      const harness = component as unknown as FileHarness;

      harness.onPicked(pick([file('photo.png', 10, 'image/png')]));
      expect(harness.files()).toHaveLength(1);
    });

    it('FIL-04: a file over the size limit is refused', () => {
      const component = build(() => {
        const made = new FileInputComponent();
        set(made, 'maxSizeMb', 1);
        return made;
      });
      const harness = component as unknown as FileHarness;

      const large = file('big.csv', 2 * 1024 * 1024, 'text/csv');
      harness.onPicked(pick([large]));

      expect(harness.files()).toEqual([]);
      expect(harness.rejected()[0].reason).toBe('size');
    });

    it('FIL-05: a single-file field keeps only the first', () => {
      const component = build(() => new FileInputComponent());
      const harness = component as unknown as FileHarness;

      harness.onPicked(pick([file('a.csv', 1), file('b.csv', 1)]));
      expect(harness.files()).toHaveLength(1);
    });

    it('FIL-06: a multiple field respects its count limit and says what it dropped', () => {
      const component = build(() => {
        const made = new FileInputComponent();
        set(made, 'multiple', true);
        set(made, 'maxFiles', 2);
        return made;
      });
      const harness = component as unknown as FileHarness;

      harness.onPicked(pick([file('a.csv', 1), file('b.csv', 1), file('c.csv', 1)]));
      expect(harness.files()).toHaveLength(2);
      expect(harness.rejected()[0]).toMatchObject({ name: 'c.csv', reason: 'count' });
    });

    it('FIL-07: removing and clearing both publish the new list', () => {
      const component = build(() => {
        const made = new FileInputComponent();
        set(made, 'multiple', true);
        return made;
      });
      const harness = component as unknown as FileHarness;
      const changed = vi.fn();

      harness.onPicked(pick([file('a.csv', 1), file('b.csv', 1)]));
      component.registerOnChange(changed);

      harness.remove(0);
      expect(harness.files().map((entry) => entry.name)).toEqual(['b.csv']);
      expect(changed).toHaveBeenLastCalledWith(harness.files());

      harness.clearAll();
      expect(harness.files()).toEqual([]);
      expect(harness.summary()).toBe('No file chosen');
    });

    it('FIL-08: writeValue ignores anything that is not a file', () => {
      const component = build(() => new FileInputComponent());
      const harness = component as unknown as FileHarness;

      component.writeValue('statement.csv');
      expect(harness.files()).toEqual([]);

      const one = file('a.csv', 1);
      component.writeValue([one, 'nonsense']);
      expect(harness.files()).toEqual([one]);
    });
  });

  describe('DateTimeInputComponent', () => {
    it('DTM-01: a date and time round-trip without a timezone shift', () => {
      // `new Date('2026-09-07')` is midnight UTC, so west of Greenwich it
      // renders as the sixth. Nothing here parses a string into a Date.
      const component = build(() => new DateTimeInputComponent());
      const harness = component as unknown as DateHarness;

      component.writeValue('2026-09-07T14:30');
      expect(harness.innerValue()).toBe('2026-09-07T14:30');

      component.writeValue('2026-09-07T14:30:59.123Z');
      expect(harness.innerValue()).toBe('2026-09-07T14:30');
    });

    it('DTM-02: a bare date gains midnight rather than being refused', () => {
      const component = build(() => new DateTimeInputComponent());
      component.writeValue('2026-09-07');
      expect((component as unknown as DateHarness).innerValue()).toBe('2026-09-07T00:00');
    });

    it('DTM-03: a Date object is read through its local accessors', () => {
      const component = build(() => new DateTimeInputComponent());
      // Month is zero-indexed: 8 is September.
      component.writeValue(new Date(2026, 8, 7, 14, 30));
      expect((component as unknown as DateHarness).innerValue()).toBe('2026-09-07T14:30');
    });

    it('DTM-04: an invalid Date clears the field rather than rendering NaN', () => {
      const component = build(() => new DateTimeInputComponent());
      component.writeValue(new Date('nonsense'));
      expect((component as unknown as DateHarness).innerValue()).toBe('');
    });

    it('DTM-05: what the element holds is published verbatim', () => {
      const component = build(() => new DateTimeInputComponent());
      const harness = component as unknown as DateHarness;
      const changed = vi.fn();
      component.registerOnChange(changed);

      harness.onInput({ target: { value: '2026-09-07T09:15' } } as unknown as Event);
      expect(changed).toHaveBeenCalledWith('2026-09-07T09:15');

      harness.onInput({ target: { value: '' } } as unknown as Event);
      expect(changed).toHaveBeenLastCalledWith(null);
    });

    it('DTM-06: the picker offers minutes, not seconds', () => {
      const component = build(() => new DateTimeInputComponent());
      expect((component as unknown as DateHarness).stepAttr).toBe('60');
    });
  });
});
