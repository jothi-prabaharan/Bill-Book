import { ChangeDetectionStrategy } from '@angular/core';
import { Component, effect, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LookupRow } from './lookup-row.model';

/**
 * The picker every document screen needs: search a master, choose a row.
 *
 * <b>It fetches nothing</b>, for the same reason `bb-document-line-grid` does
 * not: the host owns the HTTP, so this stays testable without a server and
 * `ui-components` stays Ionic-safe. Rows come in, a chosen row goes out, and
 * the typing is reported as a `search` event for the host to answer.
 *
 * <b>Debouncing is the host's job too.</b> This emits on every keystroke; what
 * to do about that depends on the endpoint behind it, and a component that
 * decided would be wrong for half its callers.
 *
 * At ~360px the dialog becomes a full-screen sheet, per the house rule.
 */
changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-lookup-dialog',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './lookup-dialog.component.html',
  styleUrl: './lookup-dialog.component.scss',
})
export class LookupDialogComponent {
  readonly open = input(false);
  readonly title = input('Choose');
  readonly placeholder = input('Search');
  readonly rows = input<readonly LookupRow[]>([]);
  readonly loading = input(false);

  /** Shown when there is nothing to choose from and nothing is loading. */
  readonly emptyText = input('Nothing matches.');

  /**
   * The term as it is typed.
   *
   * Named `searchChange` rather than `search` because `search` is a native DOM
   * event, and an output that shadows one is ambiguous at the call site — the
   * host cannot tell whether it is binding the component's or the element's.
   */
  readonly searchChange = output<string>();
  readonly choose = output<LookupRow>();
  readonly dismiss = output<void>();

  protected readonly term = signal('');

  constructor() {
    // Reopening starts clean: a dialog that reopens showing the last search is
    // one the user has to clear before it is useful.
    effect(() => {
      if (this.open()) {
        this.term.set('');
      }
    });
  }

  protected onTerm(value: string): void {
    this.term.set(value);
    this.searchChange.emit(value);
  }

  protected onChoose(row: LookupRow): void {
    this.choose.emit(row);
  }

  protected onDismiss(): void {
    this.dismiss.emit();
  }

  /** Escape closes, which is what every dialog in the product should do. */
  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      this.onDismiss();
    }
  }
}

