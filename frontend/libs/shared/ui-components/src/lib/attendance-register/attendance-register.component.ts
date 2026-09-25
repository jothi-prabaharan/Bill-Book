import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  input,
  output,
  viewChildren,
} from '@angular/core';
import { RegisterEntry, RegisterStatus, countByStatus, moveFocus, nextStatus, statusForKey } from './attendance-register.model';

/**
 * A register of people marked one of a few statuses (TK-63): students present,
 * absent or late today, and later employees. Built because a grid of selects
 * is slow for thirty-five children at 8:45 in the morning.
 *
 * - **Tap a tile** to move it to the next mark, in the caller's order.
 * - **Press a mark's key** (P, A, L…) on a focused tile to set it; focus then
 *   moves to the next tile, so a register is taken from the keyboard in one pass.
 * - **Arrow keys, Home and End** move between tiles.
 * - The counts above the tiles say how many carry each mark.
 *
 * The component never saves: it emits each change and the page owns the rows.
 * A real `<button>` per tile, so it is reachable and announced without any
 * ARIA the browser does not already give it.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-attendance-register',
  standalone: true,
  templateUrl: './attendance-register.component.html',
  styleUrl: './attendance-register.component.scss',
})
export class AttendanceRegisterComponent<TValue extends string = string> {
  readonly entries = input<readonly RegisterEntry<TValue>[]>([]);

  readonly statuses = input<readonly RegisterStatus<TValue>[]>([]);

  /** Shows the marks without letting them change: a locked day. */
  readonly readonly = input<boolean, boolean | string>(false, {
    transform: (v: boolean | string) => v === '' || v === true || v === 'true',
  });

  /** A name for the whole register, read out before the tiles. */
  readonly caption = input<string>('Register');

  readonly statusChange = output<{ id: number; status: TValue }>();

  protected readonly counts = computed(() => countByStatus(this.entries(), this.statuses()));

  protected readonly keyHint = computed(() =>
    this.statuses().map((s) => `${s.key.toUpperCase()} ${s.label.toLowerCase()}`).join(' · '),
  );

  private readonly tiles = viewChildren<ElementRef<HTMLButtonElement>>('tile');

  protected statusOf(entry: RegisterEntry<TValue>): RegisterStatus<TValue> | undefined {
    return this.statuses().find((s) => s.value === entry.status);
  }

  protected tap(entry: RegisterEntry<TValue>): void {
    if (!this.readonly()) {
      this.statusChange.emit({ id: entry.id, status: nextStatus(entry.status, this.statuses()) });
    }
  }

  protected key(event: KeyboardEvent, entry: RegisterEntry<TValue>, index: number): void {
    const target = moveFocus(index, event.key, this.entries().length, this.columns());
    if (target !== null) {
      event.preventDefault();
      this.focus(target);
      return;
    }

    const status = statusForKey(event.key, this.statuses());
    if (status !== null && !this.readonly() && !event.ctrlKey && !event.metaKey && !event.altKey) {
      event.preventDefault();
      this.statusChange.emit({ id: entry.id, status });
      this.focus(Math.min(index + 1, this.entries().length - 1));
    }
  }

  protected label(entry: RegisterEntry<TValue>): string {
    const status = this.statusOf(entry)?.label ?? entry.status;
    return this.readonly()
      ? `${entry.label}, ${status}.`
      : `${entry.label}, ${status}. Press to change, or ${this.keyHint()}.`;
  }

  private focus(index: number): void {
    this.tiles()[index]?.nativeElement.focus();
  }

  /** How many tiles sit on a row, read from the layout so arrow keys follow what is on screen. */
  private columns(): number {
    const tiles = this.tiles();
    if (tiles.length < 2) {
      return 1;
    }

    const top = tiles[0].nativeElement.offsetTop;
    const onFirstRow = tiles.findIndex((t) => t.nativeElement.offsetTop !== top);
    return onFirstRow === -1 ? tiles.length : onFirstRow;
  }
}
