import { ChangeDetectionStrategy, Component, EventEmitter, Output, input, output } from '@angular/core';
import { MessageBoxComponent } from '../message-box/message-box.component';
import { UiMessage } from '../message-box/message-box.model';

/**
 * A create page's frame, as `Shell.dc.html` draws it (TK-192): the page's
 * buttons top right, then its cards. There is no heading — the breadcrumb ends
 * in "New invoice" and names the page — and no Cancel: the breadcrumb's module
 * crumb is the way back.
 *
 * **Save draft** is offered only where the document keeps drafts (`canDraft`).
 * The primary button says what saving does: "Save" for a master or a document
 * that is confirmed rather than posted, "Save & post" where saving posts.
 *
 * The cards are projected. A form that wraps them puts `create-stack` on its
 * `<form>` for the same spacing, and two cards side by side sit in a
 * `card-row`. Anything else the actions row needs — a print preview — is
 * projected with the `createActions` attribute and sits before the buttons.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-create-page',
  standalone: true,
  imports: [MessageBoxComponent],
  templateUrl: './create-page.component.html',
  styleUrl: './create-page.component.scss',
})
export class CreatePageComponent {
  /** "Save", or "Save & post" where saving posts. */
  readonly primaryLabel = input<string>('Save');

  /** Whether the document keeps drafts; only then is Save draft offered. */
  readonly canDraft = input<boolean>(false);

  readonly draftLabel = input<string>('Save draft');

  /** A save in flight: both buttons wait. */
  readonly busy = input<boolean>(false);

  /** An incomplete form: the primary button waits, Save draft does not. */
  readonly primaryDisabled = input<boolean>(false);

  readonly messages = input<readonly UiMessage[]>([]);

  /** Whether to show the Cancel button on the top right. Defaults to true. */
  readonly showCancel = input<boolean>(true);

  readonly save = output<void>();
  readonly saveDraft = output<void>();
  @Output() readonly cancel = new EventEmitter<void>();

  protected onCancel(): void {
    if (this.cancel.observed) {
      this.cancel.emit();
    } else {
      window.history.back();
    }
  }
}
