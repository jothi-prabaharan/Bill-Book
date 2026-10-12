import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { InfoTooltipComponent } from '../info-tooltip/info-tooltip.component';
import { onCreateRequest } from './create-request';

/**
 * A transaction register's heading (TK-187): the title, the document's code and
 * how many records the register holds, and the button that starts a new one.
 *
 * The button goes to `newRoute` when one is given and emits `create` otherwise,
 * for a page that opens its editor in place. Anything else the heading needs —
 * a link to a related report — is projected beside it.
 *
 * The secondary menu's + asks for a new record with `?action=create`; the heading
 * answers it as if its own button had been pressed, so every register that uses
 * it opens its form from the menu with nothing of its own to wire (TK-189).
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-register-header',
  standalone: true,
  imports: [RouterLink, InfoTooltipComponent],
  templateUrl: './register-header.component.html',
  styleUrl: './register-header.component.scss',
})
export class RegisterHeaderComponent {
  readonly title = input.required<string>();

  /** An optional info tooltip text shown right next to the title. */
  readonly hint = input<string>('');

  /** The document's code — `POR`, `INV` — or empty for a register of several kinds. */
  readonly code = input<string>('');

  /** Records in the register. Null leaves the count out. */
  readonly count = input<number | null>(null);

  readonly newLabel = input<string>('');
  readonly newRoute = input<string>('');
  readonly newDisabled = input<boolean>(false);

  readonly create = output<void>();

  private readonly router = inject(Router);

  constructor() {
    // A register is disabled while it loads; a request from the menu waits for
    // the button rather than being dropped.
    onCreateRequest(
      () => {
        const route = this.newRoute();
        if (route) {
          void this.router.navigateByUrl(route);
        } else {
          this.create.emit();
        }
      },
      () => !!this.newLabel() && !this.newDisabled(),
    );
  }

  readonly countText = computed(() => {
    const count = this.count();
    if (count === null) return '';
    return `${count} ${count === 1 ? 'record' : 'records'}`;
  });
}
