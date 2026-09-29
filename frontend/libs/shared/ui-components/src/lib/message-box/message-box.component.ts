import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { boxTone, MessageTone, toneLabel, toneLiveness, UiMessage } from './message-box.model';

/**
 * The one place a business rule speaks to the user.
 *
 * **The split it exists to enforce.** A constraint on a field — required, too
 * long, out of range — belongs on that field, where the person's eye already is
 * and where the fix is one keystroke away. A rule about the *document* — this
 * quote is not approved yet, there is not enough stock to reserve, the credit
 * limit is exceeded — belongs nowhere near a single input, because no single
 * input is wrong. Scattered into field errors those rules either land on an
 * arbitrary field or vanish into a toast that is gone before it is read.
 *
 * **It renders what the server said, and does not paraphrase it.** Every refusal
 * in this product carries its own words — `DocumentLifecycle` writes one set for
 * all nine document types, and `SalesOrderService` names the items that were
 * short. A screen that rewrote those into "operation failed" would be throwing
 * away the only part the user can act on.
 *
 * Empty by default and it renders nothing at all, so a page can bind it
 * unconditionally rather than wrapping it in a condition of its own.
 *
 * **Two ways to fill it.** `messages` for what the server or a rule said — plain
 * text, one line each. `tone` plus projected content for a message the page
 * writes itself and wants markup in: a posted document's "this can no longer be
 * changed" with the operative words in bold, or a count of failed movements
 * with a link beside it.
 *
 * ```html
 * <bb-message-box [messages]="[{ tone: 'error', text: failure }]" />
 * <bb-message-box tone="info">This bill has been <strong>posted</strong>.</bb-message-box>
 * ```
 */
@Component({
  selector: 'bb-message-box',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './message-box.component.html',
  styleUrl: './message-box.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MessageBoxComponent {
  readonly messages = input<readonly UiMessage[]>([]);

  /**
   * Show the projected content, in this tone.
   *
   * Null — the default — means the box has no content of its own and shows
   * only `messages`. When both are present the louder tone wins, the same way
   * it does between messages.
   */
  readonly tone = input<MessageTone | null>(null);

  /** Whether the user may dismiss it. Off for anything that blocks a save. */
  readonly dismissible = input(false);

  readonly dismiss = output<void>();

  protected readonly hasMessages = computed(() => this.messages().length > 0);

  protected readonly visible = computed(() => this.hasMessages() || this.tone() !== null);

  /**
   * The loudest tone present, which is what the box takes its border from.
   * The rule itself lives in the model beside the type, where it is testable
   * without a component fixture.
   */
  protected readonly boxTone = computed(() => boxTone(this.messages(), this.tone()));

  protected readonly label = computed(() => toneLabel(this.boxTone()));

  protected readonly liveness = computed(() => toneLiveness(this.boxTone()));
}
