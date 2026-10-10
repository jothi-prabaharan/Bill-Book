import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * A transaction register's heading (TK-187): the title, the document's code and
 * how many records the register holds, and the button that starts a new one.
 *
 * The button goes to `newRoute` when one is given and emits `create` otherwise,
 * for a page that opens its editor in place. Anything else the heading needs —
 * a link to a related report — is projected beside it.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-register-header',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './register-header.component.html',
  styleUrl: './register-header.component.scss',
})
export class RegisterHeaderComponent {
  readonly title = input.required<string>();

  /** The document's code — `POR`, `INV` — or empty for a register of several kinds. */
  readonly code = input<string>('');

  /** Records in the register. Null leaves the count out. */
  readonly count = input<number | null>(null);

  readonly newLabel = input<string>('');
  readonly newRoute = input<string>('');
  readonly newDisabled = input<boolean>(false);

  readonly create = output<void>();

  readonly countText = computed(() => {
    const count = this.count();
    if (count === null) return '';
    return `${count} ${count === 1 ? 'record' : 'records'}`;
  });
}
