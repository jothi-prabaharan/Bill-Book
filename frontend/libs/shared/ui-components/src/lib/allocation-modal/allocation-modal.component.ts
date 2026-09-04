import { ChangeDetectionStrategy, Component, computed, effect, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  DEFAULT_FORMAT_SETTINGS,
  FormatSettings,
  formatDate,
  formatMoney,
} from '@bill-book/currency-format';
import { AllocationGridComponent, AllocationRow } from '../allocation-grid/allocation-grid.component';
import { MessageBoxComponent } from '../message-box/message-box.component';
import { UiMessage } from '../message-box/message-box.model';
import { DateInputComponent } from '../date-input/date-input.component';
import { TextInputComponent } from '../text-input/text-input.component';
import {
  AllocationSubmission,
  AllocationTarget,
  allocationMessages,
  canSubmit,
  decisionsFrom,
  isOverAllocated,
  remainingFor,
  totalAllocatedOf,
} from './allocation-modal.model';

/**
 * Settle one document against the credits available to it — from any
 * transaction screen, in `sal` or `pur` alike.
 *
 * <b>It fetches nothing.</b> The host loads the open credits and posts the
 * result, the same contract `bb-lookup-dialog` and `bb-document-line-grid`
 * keep, and for the same two reasons: this stays testable without a server, and
 * `ui-components` stays free of anything Ionic cannot run.
 *
 * <b>The cap is the target's outstanding balance, never its total.</b> A
 * document part-settled last week can only take what is left of it; allocating
 * against the figure on its face would claim whatever has already been claimed
 * a second time. `bb-allocation-grid` enforces both halves of that — each row
 * capped by the credit's own remaining amount, the sum capped by what the
 * target still owes — so this adds no arithmetic beyond deciding when Save is
 * allowed.
 *
 * <b>Two kinds of error, two places.</b> A constraint on a field is shown on
 * the field; a rule about the document as a whole — nothing apportioned, more
 * claimed than is available, the server's refusal — goes in the shared message
 * box at the top, where it reads as a statement about the document rather than
 * about one input.
 *
 * <b>Every decision it makes lives in `allocation-modal.model.ts`</b>, as plain
 * functions. Signal inputs cannot be set from outside a component fixture and
 * this workspace's Vitest cannot compile a `templateUrl` component at all, so
 * logic kept in a method here would be logic nothing could assert.
 *
 * At ~360px it becomes a full-screen sheet, per the house rule for modals.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-allocation-modal',
  standalone: true,
  imports: [
    FormsModule,
    AllocationGridComponent,
    MessageBoxComponent,
    DateInputComponent,
    TextInputComponent,
  ],
  templateUrl: './allocation-modal.component.html',
  styleUrl: './allocation-modal.component.scss',
})
export class AllocationModalComponent {
  readonly open = input(false);

  /** The document being settled. Null while the host is still resolving it. */
  readonly target = input<AllocationTarget | null>(null);

  /** The unallocated credits available against the target, loaded by the host. */
  readonly rows = input<readonly AllocationRow[]>([]);

  readonly loading = input(false);

  /** True while the host's POST is in flight; Save is disabled and says so. */
  readonly saving = input(false);

  /**
   * Rule errors the host wants shown — a refusal from the server, most often.
   * Merged with the modal's own into one box, because one box with one edge
   * reads as one problem.
   */
  readonly messages = input<readonly UiMessage[]>([]);

  /**
   * The branch's formats. An input rather than an injected service so the
   * component stays pure: a caller renders it against a Western mask by passing
   * one, with no injector and no server involved.
   */
  readonly formats = input<FormatSettings>(DEFAULT_FORMAT_SETTINGS);

  readonly save = output<AllocationSubmission>();
  readonly dismiss = output<void>();

  /**
   * The working copy. Never the input array — a modal that edits its host's
   * rows in place cannot be cancelled.
   */
  protected readonly draft = signal<AllocationRow[]>([]);

  protected readonly allocationDate = signal<string | null>(null);
  protected readonly notes = signal<string>('');

  /** Set once Save has been attempted, so errors appear on submit rather than while typing. */
  protected readonly submitted = signal(false);

  constructor() {
    // Reopening starts clean. A modal that reopens holding the last attempt's
    // apportionment is one the user has to undo before it is useful — and the
    // rows behind it may have moved since.
    effect(() => {
      if (this.open()) {
        this.draft.set(this.rows().map((row) => ({ ...row, allocatedAmount: row.allocatedAmount || 0 })));
        this.allocationDate.set(null);
        this.notes.set('');
        this.submitted.set(false);
      }
    });
  }

  protected readonly outstanding = computed(() => this.target()?.outstandingAmount ?? 0);

  protected readonly totalAllocated = computed(() => totalAllocatedOf(this.draft()));

  protected readonly remaining = computed(() => remainingFor(this.outstanding(), this.draft()));

  protected readonly isOver = computed(() => isOverAllocated(this.outstanding(), this.draft()));

  protected readonly canSave = computed(() =>
    canSubmit(this.target(), this.draft(), this.saving() || this.loading()),
  );

  protected readonly allMessages = computed<readonly UiMessage[]>(() => [
    ...this.messages(),
    ...allocationMessages(this.target(), this.draft(), this.submitted(), this.formats()),
  ]);

  protected money(value: number | null | undefined): string {
    return formatMoney(value, this.formats());
  }

  protected date(value: string | null | undefined): string {
    return formatDate(value, this.formats().datePattern);
  }

  protected onRowsChange(rows: AllocationRow[]): void {
    this.draft.set(rows);
  }

  protected onDismiss(): void {
    this.dismiss.emit();
  }

  /** Escape closes, matching every other dialog in the product. */
  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      this.onDismiss();
    }
  }

  protected onSave(): void {
    this.submitted.set(true);

    const target = this.target();
    if (!target || !this.canSave()) {
      return;
    }

    this.save.emit({
      target,
      decisions: decisionsFrom(this.draft()),
      allocationDate: this.allocationDate(),
      notes: this.notes().trim() || null,
    });
  }
}
