import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { StatusTab } from './register-format';

/**
 * The status tabs above a register (TK-187): All, then each status with how many
 * rows have it. Every status the document can be in is a tab, so `( 0 )` says
 * nothing is in it rather than hiding the status.
 * On mobile screens (<= 768px), it provides a button popup for clean mobile UX.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-status-tabs',
  standalone: true,
  templateUrl: './status-tabs.component.html',
  styleUrl: './status-tabs.component.scss',
})
export class StatusTabsComponent {
  readonly tabs = input.required<readonly StatusTab[]>();

  /** The selected status; empty is All. */
  readonly value = input<string>('');

  readonly label = input<string>('Status');

  readonly valueChange = output<string>();

  /** Modal popup open state for mobile view. */
  readonly modalOpen = signal(false);

  readonly activeTab = computed(
    () => this.tabs().find((t) => t.value === this.value()) ?? this.tabs()[0],
  );

  select(value: string): void {
    if (value !== this.value()) this.valueChange.emit(value);
  }

  selectAndClose(value: string): void {
    this.select(value);
    this.modalOpen.set(false);
  }

  openModal(): void {
    this.modalOpen.set(true);
  }

  closeModal(): void {
    this.modalOpen.set(false);
  }
}

