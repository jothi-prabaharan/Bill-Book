import { ChangeDetectionStrategy, Component, Input, OnChanges, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { FormatSettingsService } from '@bill-book/currency-format';
import { MessageBoxComponent } from '../message-box/message-box.component';
import {
  EditChangeRow,
  EditLogEntry,
  changeRows,
  editHistoryPath,
  entrySummary,
  isLineEntry,
} from './edit-history.model';

/**
 * A document's or master's History (TK-118): every change the edit log holds
 * against it and its lines, newest first — who, when, and each column's old
 * and new value. Read-only; the log itself refuses to be edited.
 *
 * Any service can host it: `service` names the gateway cluster
 * (`/api/edit-log/{service}`), `entity` the table the row lives in, `key` its id.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-edit-history',
  standalone: true,
  imports: [MessageBoxComponent],
  templateUrl: './edit-history.component.html',
  styleUrl: './edit-history.component.scss',
})
export class EditHistoryComponent implements OnChanges {
  private readonly http = inject(HttpClient);
  protected readonly formats = inject(FormatSettingsService);

  /** The gateway cluster, e.g. `sales`, `purchase`, `accounting`, `master`. */
  @Input({ required: true }) service!: string;

  /** The table the document lives in, e.g. `Invoices`. */
  @Input({ required: true }) entity!: string;

  /** The document's id. Nothing is loaded while it is empty. */
  @Input() key: string | number | null | undefined;

  @Input() heading = 'History';

  protected readonly entries = signal<EditLogEntry[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly entrySummary = entrySummary;
  protected readonly isLineEntry = isLineEntry;

  ngOnChanges(): void {
    void this.load();
  }

  protected rows(entry: EditLogEntry): EditChangeRow[] {
    return changeRows(entry);
  }

  protected when(value: string): string {
    const time = new Date(value);
    const hh = String(time.getHours()).padStart(2, '0');
    const mm = String(time.getMinutes()).padStart(2, '0');
    return `${this.formats.formatDate(value)} ${hh}:${mm}`;
  }

  async load(): Promise<void> {
    if (!this.service || !this.entity || this.key === null || this.key === undefined || this.key === '') {
      this.entries.set([]);
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    try {
      this.entries.set(await firstValueFrom(this.http.get<EditLogEntry[]>(editHistoryPath(this.service, this.entity, this.key))));
    } catch {
      this.entries.set([]);
      this.error.set('The history could not be loaded. Try again in a moment.');
    } finally {
      this.loading.set(false);
    }
  }
}
