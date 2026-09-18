import { Injectable, computed, signal } from '@angular/core';

/**
 * The Home board's controls live in the breadcrumb strip, not on the page —
 * the design puts module controls there and gives the page no heading of its
 * own. That splits the control from the thing it controls, so this service is
 * what joins them: the shell writes, the dashboard reads.
 */
@Injectable({ providedIn: 'root' })
export class ShellBoardService {
  /** Whether the board is in customize mode. */
  readonly editing = signal(false);

  /** False = accrual basis, true = cash basis. */
  readonly base = signal(false);

  readonly baseLabel = computed(() => (this.base() ? 'Cash basis' : 'Accrual basis'));

  /**
   * Bumped each time Reset is pressed. The dashboard watches the number rather
   * than subscribing to an event, so a reset that arrives before the board has
   * rendered is not lost.
   */
  readonly resetCount = signal(0);

  startEdit(): void {
    this.editing.set(true);
  }

  stopEdit(): void {
    this.editing.set(false);
  }

  toggleBase(): void {
    this.base.update((v) => !v);
  }

  requestReset(): void {
    this.resetCount.update((n) => n + 1);
  }
}
