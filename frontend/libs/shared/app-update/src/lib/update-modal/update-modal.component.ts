import { CdkTrapFocus } from '@angular/cdk/a11y';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import {
  faCircleArrowUp,
  faDownload,
  faRotateRight,
  faStore,
  faTriangleExclamation,
} from '@fortawesome/free-solid-svg-icons';
import { AppUpdateService } from '../app-update.service';

/**
 * The update prompt (TK-184). Optional: Later or Update now, and a click
 * outside or Escape means Later. Forced: nothing dismisses it — no close
 * button, no outside click, no Escape, no back button (the service swallows
 * that one) — and the route gate refuses navigation behind it.
 *
 * Styled with Bootstrap's modal, button and progress rules, compiled into this
 * component's own stylesheet so they cannot reach the rest of the app, whose
 * `.btn` classes are its own.
 */
@Component({
  selector: 'bb-update-modal',
  standalone: true,
  imports: [CdkTrapFocus, FaIconComponent],
  templateUrl: './update-modal.component.html',
  styleUrl: './update-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UpdateModalComponent {
  private readonly updates = inject(AppUpdateService);

  protected readonly state = toSignal(this.updates.state$, { initialValue: this.updates.snapshot() });
  protected readonly forced = computed(() => this.state().decision.kind === 'forced');
  protected readonly mobile = computed(() => this.state().platform === 'android' || this.state().platform === 'ios');
  /** Bootstrap's "static backdrop" nudge, when a forced modal is clicked outside. */
  protected readonly nudge = signal(false);

  protected readonly icons = {
    update: faCircleArrowUp,
    required: faTriangleExclamation,
    download: faDownload,
    store: faStore,
    retry: faRotateRight,
  };

  protected later(): void {
    this.updates.later();
  }

  protected act(): void {
    if (this.state().phase === 'ready') {
      void this.updates.install();
    } else {
      void this.updates.updateNow();
    }
  }

  /**
   * Escape means Later, and nothing at all when forced. Handled on the dialog,
   * which keeps focus (the focus trap, and `tabindex` for a click outside), and
   * stopped there so no page shortcut sees it.
   */
  protected escape(event: Event): void {
    event.preventDefault();
    event.stopPropagation();

    if (this.forced()) {
      this.bump();
    } else {
      this.updates.later();
    }
  }

  protected outside(event: MouseEvent): void {
    if (event.target !== event.currentTarget) {
      return;
    }

    if (this.forced()) {
      this.bump();
    } else {
      this.updates.later();
    }
  }

  private bump(): void {
    this.nudge.set(true);
    setTimeout(() => this.nudge.set(false), 300);
  }
}
