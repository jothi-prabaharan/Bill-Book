import { Component, signal, input, output, forwardRef } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR, FormsModule, ReactiveFormsModule, FormControl } from '@angular/forms';
import { describe, it, expect } from 'vitest';

@Component({
  selector: 'bb-test-cva',
  standalone: true,
  template: `<input [value]="value()" (input)="onInput($event)" [disabled]="disabled()" />`,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => TestCvaComponent),
      multi: true,
    },
  ],
})
class TestCvaComponent implements ControlValueAccessor {
  readonly value = signal<string>('');
  readonly disabled = signal<boolean>(false);
  private onChange: (val: string) => void = () => {};
  private onTouched: () => void = () => {};

  writeValue(val: string | null): void {
    this.value.set(val ?? '');
  }
  registerOnChange(fn: (val: string) => void): void {
    this.onChange = fn;
  }
  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }
  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }
  onInput(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.value.set(val);
    this.onChange(val);
  }
}

describe('TestCvaComponent direct contract tests', () => {
  it('implements CVA writeValue and setDisabledState', () => {
    const cva = new TestCvaComponent();
    expect(cva.value()).toBe('');
    expect(cva.disabled()).toBe(false);

    cva.writeValue('hello');
    expect(cva.value()).toBe('hello');

    cva.setDisabledState(true);
    expect(cva.disabled()).toBe(true);

    let changedVal = '';
    cva.registerOnChange((val) => {
      changedVal = val;
    });

    const mockInputEvent = { target: { value: 'world' } } as unknown as Event;
    cva.onInput(mockInputEvent);
    expect(cva.value()).toBe('world');
    expect(changedVal).toBe('world');
  });
});
